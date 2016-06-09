using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Unclassified.TxEditor.Util;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models.Versions
{
    public class Version1Serializer : IVersionSerializer
    {
        #region Static members

        public static string RecombineStringWithCultureName(string source, string culture)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (culture == null) throw new ArgumentNullException(nameof(culture));

            var parts = ParseString(source);
            return parts.Item1 + "." + culture + parts.Item3;
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

        private static Tuple<string, string, string> ParseString(string source)
        {
            var extension = Path.GetExtension(source)?.ToLower() ?? string.Empty;
            if (extension == ".txd" || extension == ".xml") source = source.Substring(0, source.Length - extension.Length);
            else extension = string.Empty;

            const string regexPattern = @"^(?<prefix>(.+?))\.(?<culture>([a-z]{2})([-][a-z]{2})?)$";

            var m = Regex.Match(source ?? string.Empty, regexPattern, RegexOptions.IgnoreCase);
            if (!m.Success) return new Tuple<string, string, string>(source, null, extension);
            return new Tuple<string, string, string>(m.Groups["prefix"].Value, m.Groups["culture"].Value, extension);
        }

        private static CultureInfo ParseStringForCultureName(string name)
        {
            var parts = ParseString(name);
            if (parts.Item2 == null) return null;
            return CultureInfo.GetCultureInfo(parts.Item2);
        }

        #endregion

        #region Properties

        public string Name
        {
            get { return "v1"; }
        }

        #endregion

        #region IVersionSerializer Members

        public ISerializeLocation[] DetectRelatedLocations(ISerializeLocation location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            var fileLocation = location as FileLocation;
            if (fileLocation != null)
            {
                var parts = ParseString(fileLocation.Filename);
                if (parts.Item2 != null)
                {
                    return PathHelper.EnumerateFiles(parts.Item1 + ".*" + parts.Item3)
                                     .Select(l => new FileLocation(l))
                                     .Where(l => ParseLocationForCulture(l) != null)
                                     .Where(IsValid)
                                     .Cast<ISerializeLocation>()
                                     .ToArray();
                }
            }

            return new[] { location };
        }

        public ISerializeDescription DescribeLocation(ISerializeLocation location)
        {
            string name = null;
            string shortName = null;
            var fileSource = location as FileLocation;
            if (fileSource != null)
            {
                var parsedString = ParseString(fileSource.Filename);
                name = parsedString.Item1;
                shortName = Path.GetFileName(parsedString.Item1);
            }

            var embeddedSource = location as EmbeddedResourceLocation;
            if (embeddedSource != null)
            {
                name = embeddedSource.ToString();
                shortName = ParseString(embeddedSource.Name).Item1;
            }

            return new SerializeDescription(name, shortName);
        }

        public DeserializeInstruction Deserialize(ISerializeLocation location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            var document = location.Load();

            var ci = ParseLocationForCulture(location);
            if (ci == null) throw new NotSupportedException("Version {0} does not support {1} location");

            var externalCultureName = ci.Name;
            if (string.IsNullOrEmpty(externalCultureName)) throw new InvalidDataException("external name is not set");

            Func<SerializedTranslation> deserializeFunc = () => new SerializedTranslation
            {
                IsTemplate = document.DocumentElement?.Attributes["template"]?.Value == "true",
                Name = document.DocumentElement?.Attributes["name"]?.Value,
                Cultures = new[]
                {
                    new SerializedCulture
                    {
                        Keys = document.DocumentElement?
                                       .SelectNodes("text[@key]")
                                       .Enumerate<XmlElement>()
                                       .Select(DeserializeKey)
                                       .Where(k => k != null)
                                       .ToList(),
                        IsPrimary = document.Attributes?["primary"]?.Value?.ToLower() == "true",
                        Name = externalCultureName
                    }
                }.ToList()
            };
            return new DeserializeInstruction(location, this, deserializeFunc);
        }

        public bool IsValid(ISerializeLocation location)
        {
            try
            {
                var document = location.Load();
                if (document.DocumentElement?.Name != "translation") return false;
                if (ParseLocationForCulture(location) == null) return false;
                return document.DocumentElement.SelectNodes("text[@key]").Enumerate<XmlNode>().Any();
            }
            catch
            {
                return false;
            }
        }

        public SerializeInstruction[] Serialize(ISerializeLocation location, SerializedTranslation translation)
        {
            var result = new List<SerializeInstruction>();
            foreach (var culture in translation.Cultures)
            {
                var fileLocation = location as FileLocation;
                if (fileLocation != null) fileLocation = new FileLocation(RecombineStringWithCultureName(fileLocation.Filename, culture.Name));
                if (fileLocation == null) throw new NotSupportedException("Location {0} not supported");

                Action serializeAction = () =>
                {
                    var cultureTranslation = new SerializedTranslation
                    {
                        IsTemplate = translation.IsTemplate,
                        Cultures = new []{culture}.ToList()
                    };
                    fileLocation.Save(SerializeTranslation(cultureTranslation));
                };
                result.Add(new SerializeInstruction(fileLocation, this, serializeAction));
            }

            return result.ToArray();
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
                AcceptPunctuation = textNode.Attributes["acceptpunctuation"]?.Value?.ToLower() == "true"
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

            if (key.Count > -1)
            {
                var countAttr = document.CreateAttribute("count");
                countAttr.Value = key.Count.ToString();
                textElement.Attributes.Append(countAttr);
            }

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
            document.AppendChild(document.CreateComment(" TxTranslation dictionary file. Use TxEditor to edit this file. http://unclassified.software/txtranslation "));
            var translationElement = document.CreateElement("translation");
            document.AppendChild(translationElement);
            var spaceAttr = document.CreateAttribute("xml:space");
            spaceAttr.Value = "preserve";
            translationElement.Attributes.Append(spaceAttr);

            if (!string.IsNullOrEmpty(translation.Name))
            {
                var nameAttr = document.CreateAttribute("name");
                nameAttr.Value = translation.Name;
                translationElement.Attributes.Append(nameAttr);
            }

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