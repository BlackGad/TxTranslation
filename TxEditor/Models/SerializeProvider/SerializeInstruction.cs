using System;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class SerializeInstruction
    {
        #region Constructors

        public SerializeInstruction(SerializeInstructionFragment fragment)
        {
            if (fragment == null) throw new ArgumentNullException(nameof(fragment));
            Fragments = new[] { fragment };
        }

        public SerializeInstruction(SerializeInstructionFragment[] fragments)
        {
            if (fragments == null) throw new ArgumentNullException(nameof(fragments));
            Fragments = fragments;
        }

        #endregion

        #region Properties

        public SerializeInstructionFragment[] Fragments { get; }

        #endregion
    }

    public class SerializeInstructionFragment
    {
        #region Constructors

        public SerializeInstructionFragment(XmlDocument document, ISerializeLocation location)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (location == null) throw new ArgumentNullException(nameof(location));
            Document = document;
            Location = location;
        }

        #endregion

        #region Properties

        public XmlDocument Document { get; }
        public ISerializeLocation Location { get; }

        #endregion
    }
}