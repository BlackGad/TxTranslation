using Unclassified.TxEditor.Models;

namespace Unclassified.TxEditor.ViewModels
{
    class RootKeyViewModel : TextKeyViewModel
    {
        IVersionSerializerDescription _serializerDescription;

        #region Constructors

        public RootKeyViewModel(MainViewModel mainWindowVm)
            : base(null, false, null, mainWindowVm)
        {
        }

        #endregion

        #region Properties

        public string LoadedFilePath { get; set; }

        public string LoadedFilePrefix { get; set; }

        public IVersionSerializerDescription Serializer
        {
            get { return _serializerDescription ?? SerializeProvider.Instance.Version2; }
            set { _serializerDescription = value; }
        }

        #endregion

        #region Members

        public void Reset()
        {
            Children.Clear();
            LoadedFilePath = null;
            LoadedFilePrefix = null;
        }

        #endregion
    }
}