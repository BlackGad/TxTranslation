using System;

namespace Unclassified.TxEditor.Models
{
    public interface ISerializeLocationBackup
    {
        #region Members

        void Backup();
        Exception CanBackup();
        Exception CanCleanBackup();
        Exception CanRestore();
        void CleanBackup();
        void Restore();

        #endregion
    }
}