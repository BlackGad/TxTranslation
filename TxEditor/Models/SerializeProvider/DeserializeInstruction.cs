using System;

namespace Unclassified.TxEditor.Models
{
    public class DeserializeInstruction
    {
        #region Constructors

        public DeserializeInstruction(ISerializeLocation location, IVersionSerializer description)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (description == null) throw new ArgumentNullException(nameof(description));

            Location = location;
            Description = description;
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription Description { get; }

        public ISerializeLocation Location { get; }

        #endregion

        #region Members

        public SerializedTranslation Deserialize()
        {
            return ((IVersionSerializer)Description).Deserialize(Location);
        }

        #endregion
    }
}