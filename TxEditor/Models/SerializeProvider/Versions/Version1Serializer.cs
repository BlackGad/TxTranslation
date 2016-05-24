using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models.Versions
{
    public class Version1Serializer : IVersionSerializer
    {
        #region Static members

        public static string RecombineStringWithCultureName(string source, string culture)
        {
            var extension = Path.GetExtension(source)?.ToLower() ?? string.Empty;
            if (extension == ".txd" || extension == ".xml") source = source.Substring(0, source.Length - extension.Length);
            else extension = string.Empty;

            const string regexPattern = @"^(?<prefix>(.+?)\.)(?<culture>([a-z]{2})([-][a-z]{2})?)$";

            var m = Regex.Match(source ?? string.Empty, regexPattern, RegexOptions.IgnoreCase);
            if (!m.Success) return source + "." + culture + extension;

            var sourcePrefix = m.Groups["prefix"].Value;
            return sourcePrefix + culture + extension;
        }

        private static CultureInfo ParseLocationForCulture(ISerializeLocation location)
        {
            string name = null;
            var fileSource = location as FileLocation;
            if (fileSource != null) name = fileSource.Filename;

            var embeddedSource = location as EmbeddedResourceLocation;
            if (embeddedSource != null) name = embeddedSource.Name;

            if (string.IsNullOrEmpty(name)) return null;

            var ci = ParseStringForCultureName(name);
            return ci;
        }

        private static CultureInfo ParseStringForCultureName(string name)
        {
            const string regexPattern = @"(.+?)\.(([a-z]{2})([-][a-z]{2})?)\.(?:txd|xml)$";
            var m = Regex.Match(name ?? string.Empty, regexPattern, RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            var cultureName = m.Groups[2].Value;
            var ci = CultureInfo.GetCultureInfo(cultureName);
            return ci;
        }

        #endregion

        #region Properties

        public string Name
        {
            get { return "v1"; }
        }

        #endregion

        #region IVersionSerializer Members

        public SerializedTranslation Deserialize(ISerializeLocation location, XmlDocument document)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (document == null) throw new ArgumentNullException(nameof(document));

            var ci = ParseLocationForCulture(location);
            if (ci == null) throw new NotSupportedException("Version {0} does not support {1} location");

            var externalCultureName = ci.Name;

            if (string.IsNullOrEmpty(externalCultureName)) throw new InvalidDataException("external name is not set");

            return new SerializedTranslation
            {
                IsTemplate = document.DocumentElement?.Attributes["template"]?.Value == "true",
                Cultures = new[]
                {
                    new SerializedCulture
                    {
                        Keys = document.SelectNodes("text[@key]")
                                       .Enumerate<XmlElement>()
                                       .Select(DeserializeKey)
                                       .Where(k => k != null)
                                       .ToArray(),
                        IsPrimary = document.Attributes?["primary"]?.Value?.ToLower() == "true",
                        Name = externalCultureName,
                        XmlElement = null
                    }
                },
                XmlElement = document.DocumentElement
            };
        }

        public SerializeInstruction Serialize(ISerializeLocation location, SerializedTranslation translation)
        {
            var fragments = new List<SerializeInstructionFragment>();
            foreach (var culture in translation.Cultures)
            {
                var fileLocation = location as FileLocation;
                if (fileLocation != null) fileLocation = new FileLocation(RecombineStringWithCultureName(fileLocation.Filename, culture.Name));

                if (fileLocation == null) throw new NotSupportedException("Location {0} not supported");

                fragments.Add(new SerializeInstructionFragment(SerializeTranslation(translation), fileLocation));
            }

            return new SerializeInstruction(fragments.ToArray());
        }

        public bool IsValid(ISerializeLocation location, XmlDocument xmlDoc)
        {
            if (xmlDoc.DocumentElement?.Name != "translation") return false;
            if (ParseLocationForCulture(location) == null) return false;
            return xmlDoc.DocumentElement.SelectNodes("text[@key]").Enumerate<XmlNode>().Any();
        }

        #endregion

        #region Members

        private SerializedKey DeserializeKey(XmlElement textNode)
        {
            int count;
            if (int.TryParse(textNode.Attributes["count"]?.Value, out count))
            {
                if (count < 0 || count > ushort.MaxValue)
                {
                    // Count value out of range. Skip invalid entries
                    //Log("Load XML: Count attribute value of key {0} is out of range. Ignoring definition.", key);
                    return null;
                }
            }
            else count = -1;

            int modulo;
            if (int.TryParse(textNode.Attributes["mod"]?.Value, out modulo))
            {
                if (modulo < 2 || modulo > 1000)
                {
                    // Count value out of range. Skip invalid entries
                    //Log("Load XML: Count attribute value of key {0} is out of range. Ignoring definition.", key);
                    return null;
                }
            }
            else modulo = 0;

            return new SerializedKey
            {
                Key = textNode.Attributes["key"]?.Value,
                Text = textNode.InnerText,
                Comment = textNode.Attributes["comment"]?.Value,
                Count = count,
                Modulo = modulo,
                AcceptMissing = textNode.Attributes["acceptmissing"]?.Value?.ToLower() == "true",
                AcceptPlaceholders = textNode.Attributes["acceptplaceholders"]?.Value?.ToLower() == "true",
                AcceptPunctuation = textNode.Attributes["acceptpunctuation"]?.Value?.ToLower() == "true",
                XmlElement = textNode
            };
        }

        private void SerializeKey(XmlElement translationElement, SerializedKey key)
        {
            var document = translationElement.OwnerDocument;
            if (document == null) throw new InvalidOperationException();
            var textElement = document.CreateElement("text");
            translationElement.AppendChild(textElement);
            var keyAttr = document.CreateAttribute("key");
            keyAttr.Value = key.Key;
            textElement.Attributes.Append(keyAttr);
            if (!string.IsNullOrEmpty(key.Text)) textElement.InnerText = key.Text;

            if (key.AcceptMissing)
            {
                var acceptMissingAttr = document.CreateAttribute("acceptmissing");
                acceptMissingAttr.Value = "true";
                textElement.Attributes.Append(acceptMissingAttr);
            }
            if (key.AcceptPlaceholders)
            {
                var acceptPlaceholdersAttr = document.CreateAttribute("acceptplaceholders");
                acceptPlaceholdersAttr.Value = "true";
                textElement.Attributes.Append(acceptPlaceholdersAttr);
            }
            if (key.AcceptPunctuation)
            {
                var acceptPunctuationAttr = document.CreateAttribute("acceptpunctuation");
                acceptPunctuationAttr.Value = "true";
                textElement.Attributes.Append(acceptPunctuationAttr);
            }

            // Add the text key comment to the primary culture
            // (If no primary culture is set, the first-displayed is used to save the comments)
            if (!string.IsNullOrWhiteSpace(key.Comment))
            {
                var commentAttr = document.CreateAttribute("comment");
                commentAttr.Value = key.Comment;
                textElement.Attributes.Append(commentAttr);
            }

            var countAttr = document.CreateAttribute("count");
            countAttr.Value = key.Count.ToString();
            textElement.Attributes.Append(countAttr);

            if (key.Modulo != 0 && key.Modulo < 2 && key.Modulo > 1000)
                throw new Exception("Invalid modulo value " + key.Modulo + " set for text key " + key.Key + ", count " + key.Count);

            if (key.Modulo > 1)
            {
                var modAttr = document.CreateAttribute("mod");
                modAttr.Value = key.Modulo.ToString();
                textElement.Attributes.Append(modAttr);
            }
        }

        private XmlDocument SerializeTranslation(SerializedTranslation translation)
        {
            var document = new XmlDocument();
            var translationElement = document.CreateElement("translation");
            document.AppendChild(translationElement);
            var spaceAttr = document.CreateAttribute("xml:space");
            spaceAttr.Value = "preserve";
            translationElement.Attributes.Append(spaceAttr);

            var culture = translation.Cultures[0];
            if (culture.IsPrimary)
            {
                var primaryAttr = document.CreateAttribute("primary");
                primaryAttr.Value = "true";
                translationElement.Attributes.Append(primaryAttr);
            }

            if (translation.IsTemplate)
            {
                var templateAttr = document.CreateAttribute("template");
                templateAttr.Value = "true";
                translationElement.Attributes.Append(templateAttr);
            }

            foreach (var key in culture.Keys)
            {
                SerializeKey(translationElement, key);
            }

            return document;
        }

        #endregion
    }
}