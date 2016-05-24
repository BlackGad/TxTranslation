using System;

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
}