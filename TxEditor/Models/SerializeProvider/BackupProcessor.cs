using System;
using System.Collections.Generic;
using System.Linq;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models
{
    public class BackupProcessor : IDisposable
    {
        private readonly Dictionary<SerializeInstruction, ISerializeLocationBackup> _instructions;

        #region Constructors

        public BackupProcessor(params SerializeInstruction[] instructions)
        {
            if (instructions == null) throw new ArgumentNullException(nameof(instructions));
            _instructions = instructions.Enumerate().Where(i => i.Location.QueryBackup() != null).ToDictionary(i => i, i => i.Location.QueryBackup());

            var hasObsoleteBackups = _instructions.Where(p => p.Value.CanCleanBackup() == null);
            var failedObsoleteBackupCleaning = hasObsoleteBackups
                .BatchOperation((i, b) => b.CleanBackup())
                .Select(e => new Exception(string.Format("Cannot remove obsolete backup for \"{0}\".", e.Instruction.Location), e))
                .ToList();

            if (failedObsoleteBackupCleaning.Any()) throw new AggregateException(failedObsoleteBackupCleaning);

            var willBeOverriden = _instructions.Where(p => p.Key.Location.CanLoad() == null);
            var failedBackup = willBeOverriden.BatchOperation((i, b) => b.Backup())
                                              .Select(e => new Exception(string.Format("Cannot backup \"{0}\".", e.Instruction.Location), e))
                                              .ToList();
            if (failedBackup.Any()) throw new AggregateException(failedBackup);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            var hasBackups = _instructions.Where(p => p.Value.CanCleanBackup() == null);
            var failedCleaning = hasBackups.BatchOperation((i, b) => b.CleanBackup())
                                           .Select(e => new Exception(string.Format("Cannot remove backup for \"{0}\".", e.Instruction.Location), e))
                                           .ToList();
            if (failedCleaning.Any()) throw new AggregateException(failedCleaning);
        }

        #endregion

        #region Members

        public void Restore()
        {
            var restoreRequired = _instructions.Where(p => p.Value.CanRestore() == null);
            var failedRestore = restoreRequired.BatchOperation((i, b) => b.Restore())
                                               .Select(e => new Exception(string.Format("Cannot restore \"{0}\".", e.Instruction.Location), e))
                                               .ToList();
            if (failedRestore.Any()) throw new AggregateException(failedRestore);
        }

        #endregion
    }
}