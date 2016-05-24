using System;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializeInstructionFragment
    {
        private readonly Func<XmlDocument> _serializeAction;

        #region Constructors

        public SerializeInstructionFragment(ISerializeLocation location, IVersionSerializerDescription descriptor, Func<XmlDocument> serializeFunc)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (serializeFunc == null) throw new ArgumentNullException(nameof(serializeFunc));

            _serializeAction = serializeFunc;
            Descriptor = descriptor;
            Location = location;
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription Descriptor { get; }

        public ISerializeLocation Location { get; }

        #endregion

        #region Members

        public XmlDocument Serialize()
        {
            return _serializeAction();
        }

        #endregion
    }
}