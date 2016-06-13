using System;

namespace Unclassified.TxEditor.Models
{
    public class SerializeDescription : ISerializeDescription
    {
        #region Constructors

        public SerializeDescription(string name, string shortName, ISerializeLocation location, IVersionSerializerDescription serializer)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (shortName == null) throw new ArgumentNullException(nameof(shortName));
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            Name = name;
            ShortName = shortName;
            Location = location;
            Serializer = serializer;
        }

        #endregion

        #region Properties

        public ISerializeLocation Location { get; }
        public string Name { get; }
        public IVersionSerializerDescription Serializer { get; }
        public string ShortName { get; }

        #endregion
    }
}