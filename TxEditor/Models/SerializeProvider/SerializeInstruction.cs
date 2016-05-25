using System;

namespace Unclassified.TxEditor.Models
{
    public class SerializeInstruction
    {
        private readonly Action _serializeAction;

        #region Constructors

        public SerializeInstruction(ISerializeLocation location, IVersionSerializerDescription descriptor, Action serializeAction)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (serializeAction == null) throw new ArgumentNullException(nameof(serializeAction));

            _serializeAction = serializeAction;
            Descriptor = descriptor;
            Location = location;
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription Descriptor { get; }

        public ISerializeLocation Location { get; }

        #endregion

        #region Members

        public void Serialize()
        {
            _serializeAction();
        }

        #endregion
    }
}