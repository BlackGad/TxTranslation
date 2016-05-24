using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocation
    {
        #region Members

        XmlDocument GetDocument();

        #endregion
    }
}