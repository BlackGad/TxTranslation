using System;
using System.Linq;
using System.Xml;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models.Versions
{
    class Version2Serializer : IVersionSerializer
    {
        #region Properties

        public string Name
        {
            get { return "v2"; }
        }

        #endregion

        #region IVersionSerializer Members

        public SerializeInstruction Serialize(ISerializeLocation location, SerializedTranslation translation)
        {
            return new SerializeInstruction(new SerializeInstructionFragment(SerializeTranslation(translation), location));
        }

        public SerializedTranslation Deserialize(ISerializeLocation location, XmlDocument document)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (document == null) throw new ArgumentNullException(nameof(document));
            var result = new SerializedTranslation
            {
                IsTemplate = document.DocumentElement?.Attributes["template"]?.Value == "true",
                Cultures = document.DocumentElement?.SelectNodes("culture")
                                   .Enumerate<XmlElement>()
                                   .Select(DeserializedCulture)
                                   .Where(k => k != null)
                                   .ToArray(),
                XmlElement = document.DocumentElement
            };

            return result;
        }

        public bool IsValid(ISerializeLocation location, XmlDocument document)
        {
            if (document.DocumentElement?.Name != "translation") return false;
            return document.DocumentElement.SelectNodes("culture").Enumerate<XmlNode>().Any();
        }

        #endregion

        #region Members

        private SerializedCulture DeserializedCulture(XmlElement cultureNode)
        {
            return new SerializedCulture
            {
                Keys = cultureNode?.SelectNodes("text[@key]")
                                   .Enumerate<XmlElement>()
                                   .Select(DeserializeKey)
                                   .Where(k => k != null)
                                   .ToArray(),
                IsPrimary = cultureNode?.Attributes["primary"]?.Value?.ToLower() == "true",
                Name = cultureNode?.Attributes["name"]?.Value,
                XmlElement = cultureNode
            };
        }

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

        private void SerializeCulture(XmlElement translationElement, SerializedCulture culture)
        {
            var document = translationElement.OwnerDocument;
            if (document == null) throw new InvalidOperationException();

            if (culture.IsPrimary)
            {
                var primaryAttr = document.CreateAttribute("primary");
                primaryAttr.Value = "true";
                translationElement.Attributes.Append(primaryAttr);
            }

            var cultureElement = document.CreateElement("culture");
            translationElement.AppendChild(cultureElement);
            var nameAttr = document.CreateAttribute("name");
            nameAttr.Value = culture.Name;
            cultureElement.Attributes.Append(nameAttr);
            if (culture.IsPrimary)
            {
                var primaryAttr = document.CreateAttribute("primary");
                primaryAttr.Value = "true";
                cultureElement.Attributes.Append(primaryAttr);
            }

            foreach (var key in culture.Keys)
            {
                SerializeKey(cultureElement, key);
            }
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

            if (translation.IsTemplate)
            {
                var templateAttr = document.CreateAttribute("template");
                templateAttr.Value = "true";
                translationElement.Attributes.Append(templateAttr);
            }

            foreach (var culture in translation.Cultures)
            {
                SerializeCulture(translationElement, culture);
            }

            return document;
        }

        #endregion
    }
}