using System.Windows;
using Unclassified.UI;

namespace Unclassified.TxEditor.Views
{
    public partial class TranslationSelectWindow
    {
        #region Constructors

        public TranslationSelectWindow()
        {
            InitializeComponent();
            this.HideIcon();
        }

        #endregion

        #region Event handlers

        private void CancelButton_Click(object sender, RoutedEventArgs args)
        {
            DialogResult = false;
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs args)
        {
            DialogResult = true;
            Close();
        }

        #endregion
    }
}