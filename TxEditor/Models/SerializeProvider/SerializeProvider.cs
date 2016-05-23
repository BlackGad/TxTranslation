using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializeProvider
    {
        #region Static members

        public static SerializeProvider Instance { get; }

        static SerializeProvider()
        {
            Instance = new SerializeProvider();
        }

        #endregion

        #region Constructors

        private SerializeProvider()
        {
        }

        #endregion

        #region Members

        public SerializedKey DeserializeKey(XmlNode textNode)
        {
            int count;
            if (int.TryParse(textNode.Attributes?["count"]?.Value, out count))
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
            if (int.TryParse(textNode.Attributes?["mod"]?.Value, out modulo))
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
                Key = textNode.Attributes?["key"]?.Value,
                Text = textNode.InnerText,
                Comment = textNode.Attributes?["comment"]?.Value,
                Count = count,
                Modulo = modulo,
                AcceptMissing = textNode.Attributes?["acceptmissing"]?.Value?.ToLower() == "true",
                AcceptPlaceholders = textNode.Attributes?["acceptplaceholders"]?.Value?.ToLower() == "true",
                AcceptPunctuation = textNode.Attributes?["acceptpunctuation"]?.Value?.ToLower() == "true"
            };
        }

        #endregion
    }
}