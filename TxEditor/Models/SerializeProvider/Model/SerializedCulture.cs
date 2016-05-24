using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializedCulture
    {
        #region Properties

        public string Name { get; set; }
        public bool IsPrimary { get; set; }
        public SerializedKey[] Keys { get; set; }
        public XmlElement XmlElement { get; set; }
        #endregion
    }
}