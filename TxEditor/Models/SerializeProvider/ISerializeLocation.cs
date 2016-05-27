using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocation
    {
        #region Members

        void Backup();
        bool CanBackup();
        bool CanCleanBackup();
        bool CanLoad();
        bool CanRestore();
        bool CanSave();
        void CleanBackup();
        XmlDocument Load();
        void Restore();
        void Save(XmlDocument document);

        #endregion
    }
}