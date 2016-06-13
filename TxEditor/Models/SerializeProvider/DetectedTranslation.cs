using System;

namespace Unclassified.TxEditor.Models
{
    public class DetectedTranslation
    {
        #region Constructors

        public DetectedTranslation(ISerializeDescription description,
                                   DeserializeInstruction[] instructions,
                                   DeserializeInstruction[] relatedMissedInstructions)
        {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (instructions == null) throw new ArgumentNullException(nameof(instructions));
            if (relatedMissedInstructions == null) throw new ArgumentNullException(nameof(relatedMissedInstructions));

            Description = description;
            DeserializeInstructions = instructions;
            RelatedMissedInstructions = relatedMissedInstructions;
        }

        #endregion

        #region Properties

        public ISerializeDescription Description { get; }
        public DeserializeInstruction[] DeserializeInstructions { get; }
        public DeserializeInstruction[] RelatedMissedInstructions { get; }

        #endregion
    }
}