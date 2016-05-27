using System.Linq;
using System.Text;
using Unclassified.TxEditor.Models;
using Unclassified.TxLib;

namespace Unclassified.TxEditor.ViewModels
{
    class RootKeyViewModel : TextKeyViewModel
    {
        #region Constructors

        public RootKeyViewModel(MainViewModel mainWindowVm)
            : base(null, false, null, mainWindowVm)
        {
        }

        #endregion

        #region Properties

        public bool HasUnsavedChanges { get; set; }
        public bool IsTemplateFile { get; set; }

        public ISerializeLocation Location { get; set; }

        public IVersionSerializerDescription Serializer { get; set; }

        #endregion

        #region Members

        public string FormatTitle()
        {
            if (Location == null) return null;

            var serializer = Serializer ?? SerializeProvider.Instance.Version2;
            var locationDescription = serializer.DescribeLocation(Location);
            var builder = new StringBuilder();
            builder.Append(locationDescription.ShortName);
            builder.AppendFormat("({0})", serializer.Name);
            if (HasUnsavedChanges) builder.Append("*");
            builder.Append(" " + Tx.T("window.title.in path") + " " + locationDescription.Name);
            return builder.ToString();
        }

        public void Reset()
        {
            Children.Clear();
            IsTemplateFile = false;
            Location = null;
            Serializer = null;
            HasUnsavedChanges = false;
        }

        /// <summary>
        ///     Writes all loaded text keys to a location.
        /// </summary>
        /// <param name="location">Target location.</param>
        /// <returns>true, if the file was saved successfully, false otherwise.</returns>
        public bool SaveTo(ISerializeLocation location)
        {
            var translation = MainViewModel.DumpTranslation(this);
            var serializer = Serializer ?? SerializeProvider.Instance.Version2;
            SerializeInstruction[] instructions;
            try
            {
                instructions = SerializeProvider.Instance.SaveTo(translation, location, serializer);
            }
            catch
            {
                App.ErrorMessage(Tx.T("msg.cannot save unsupported file version", "ver", serializer.Name));
                return false;
            }

            var hasObsoleteBackups = instructions.Where(i => i.Location.CanCleanBackup());
            var failedObsoleteBackupCleaning = hasObsoleteBackups.BatchOperation(i => i.Location.CleanBackup());
            foreach (var exception in failedObsoleteBackupCleaning)
            {
                App.ErrorMessage(string.Format("Cannot remove obsolete backup for \"{0}\".", exception.Instruction.Location), exception, "Saving file");
                return false;
            }

            var willBeOverriden = instructions.Where(i => i.Location.CanLoad());
            var failedBackup = willBeOverriden.BatchOperation(i => i.Location.Backup());
            foreach (var exception in failedBackup)
            {
                App.ErrorMessage(string.Format("Cannot backup \"{0}\".", exception.Instruction.Location), exception, "Saving file");
                return false;
            }

            var serialized = instructions.BatchOperation(i => i.Serialize()).ToList();

            foreach (var exception in serialized)
            {
                //TODO: Show batch message
                App.ErrorMessage(string.Format("Cannot serialize \"{0}\". Rolling back.", exception.Instruction.Location), exception, "Saving file");
            }

            if (serialized.Any())
            {
                var restoreRequired = instructions.Where(i => i.Location.CanRestore());
                var failedRestore = restoreRequired.BatchOperation(i => i.Location.Restore()).ToList();
                foreach (var exception in failedRestore)
                {
                    //TODO: Show batch message
                    App.ErrorMessage(string.Format("Cannot restore \"{0}\".", exception.Instruction.Location), exception, "Saving file");
                }
            }

            var hasBackups = instructions.Where(i => i.Location.CanCleanBackup());
            var failedBackupCleaning = hasBackups.BatchOperation(i => i.Location.CleanBackup());
            foreach (var exception in failedBackupCleaning)
            {
                //TODO: Show batch message
                App.ErrorMessage(string.Format("Cannot remove backup for \"{0}\".", exception.Instruction.Location), exception, "Saving file");
            }

            return !serialized.Any();
        }

        #endregion
    }
}