using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocation
    {
        #region Members

        bool Exists();
        XmlDocument Load();
        void Save(XmlDocument document);

        #endregion
    }
}