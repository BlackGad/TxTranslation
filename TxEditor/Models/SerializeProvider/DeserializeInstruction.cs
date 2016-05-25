using System;

namespace Unclassified.TxEditor.Models
{
    public class DeserializeInstruction
    {
        private readonly Func<SerializedTranslation> _deserializeFunc;

        #region Constructors

        public DeserializeInstruction(ISerializeLocation location, IVersionSerializer description, Func<SerializedTranslation> deserializeFunc)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (deserializeFunc == null) throw new ArgumentNullException(nameof(deserializeFunc));

            Location = location;
            Description = description;
            _deserializeFunc = deserializeFunc;
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription Description { get; }

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