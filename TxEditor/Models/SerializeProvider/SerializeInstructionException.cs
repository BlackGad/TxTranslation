using System;

namespace Unclassified.TxEditor.Models
{
    public class SerializeInstructionException : Exception
    {
        #region Constructors

        public SerializeInstructionException(SerializeInstruction instruction, Exception innerException)
            : base(null, innerException)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            Instruction = instruction;
        }

        #endregion

        #region Properties

        public SerializeInstruction Instruction { get; }

        #endregion
    }
}