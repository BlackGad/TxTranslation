using System.Linq;
using System.Windows.Forms;
using TaskDialogInterop;
using Unclassified.TxEditor.Models;
using Unclassified.TxEditor.Views;
using Unclassified.TxLib;

namespace Unclassified.TxEditor
{
    public static class DialogHelper
    {
        #region Static members

        public static DialogResult LoadMissedRelatedLocations(DetectedTranslation detectedTranslation)
        {
            if (!detectedTranslation.RelatedMissedInstructions.Any()) return DialogResult.Ignore;

            var result = TaskDialog.Show(
                owner: MainWindow.Instance,
                title: "TxEditor",
                mainInstruction: Tx.T("msg.load location.related locations available"),
                content: Tx.T("msg.load location.related locations available.desc", "list", string.Join(", ", detectedTranslation.RelatedMissedInstructions.Select(l=>l.Location.ToString()))),
                customButtons: new[] { Tx.T("task dialog.button.load all"), Tx.T("task dialog.button.load one"), Tx.T("task dialog.button.cancel") },
                allowDialogCancellation: true);
            switch (result.CustomButtonResult)
            {
                case 0:
                    return DialogResult.OK;
                case 1:
                    return DialogResult.Ignore;
                default:
                    return DialogResult.Cancel;
            }
        }

        #endregion
    }
}