using System;

namespace Unclassified.TxEditor.Models
{
    public class DeserializeInstruction
    {
        private readonly Func<SerializedTranslation> _deserializeFunc;

        #region Constructors

        public DeserializeInstruction(ISerializeLocation location, IVersionSerializer serializer, Func<SerializedTranslation> deserializeFunc)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (deserializeFunc == null) throw new ArgumentNullException(nameof(deserializeFunc));

            Location = location;
            Serializer = serializer;
            _deserializeFunc = deserializeFunc;
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription Serializer { get; }

        public ISerializeLocation Location { get; }

        #endregion

        #region Members

        public SerializedTranslation Deserialize()
        {
            return _deserializeFunc();
        }

        #endregion
    }
}