using System;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocation
    {
        #region Members

        Exception CanLoad();
        Exception CanSave();

        XmlDocument Load();

        ISerializeLocationBackup QueryBackup();

        void Save(XmlDocument document);

        #endregion
    }
}