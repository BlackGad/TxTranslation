using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializedKey
    {
        #region Properties

        public bool AcceptMissing { get; set; }
        public bool AcceptPlaceholders { get; set; }
        public bool AcceptPunctuation { get; set; }
        public string Comment { get; set; }
        public int Count { get; set; }
        public string Key { get; set; }
        public int Modulo { get; set; }
        public string Text { get; set; }

        public XmlElement XmlElement { get; set; }
        #endregion
    }
}