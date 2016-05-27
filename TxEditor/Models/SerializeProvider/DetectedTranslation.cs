using System;

namespace Unclassified.TxEditor.Models
{
    public class DetectedTranslation
    {
        #region Constructors

        public DetectedTranslation(ISerializeDescription description, DeserializeInstruction[] instructions)
        {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (instructions == null) throw new ArgumentNullException(nameof(instructions));
            Description = description;
            DeserializeInstructions = instructions;
        }

        #endregion

        #region Properties

        public ISerializeDescription Description { get; }

        public DeserializeInstruction[] DeserializeInstructions { get; }

        #endregion
    }
}