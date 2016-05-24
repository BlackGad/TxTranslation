using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializedTranslation
    {
        #region Properties

        public SerializedCulture[] Cultures { get; set; }
        public bool IsTemplate { get; set; }

        public XmlElement XmlElement { get; set; }
        #endregion
    }
}