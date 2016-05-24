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

        public IVersionSerializerDescription Serializer
        {
            get { return _serializerDescription ?? SerializeProvider.Instance.Version2; }
            set { _serializerDescription = value; }
        }

        #endregion
    }
}