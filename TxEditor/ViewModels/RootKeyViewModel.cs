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
        public bool IsTemplate { get; set; }

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
            IsTemplate = false;
            Location = null;
            Serializer = null;
            HasUnsavedChanges = false;
        }

        #endregion
    }
}