using System;
using System.Collections;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocation : IEquatable<ISerializeLocation>
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