using System;

namespace Unclassified.TxEditor.Models
{
    public class DetectedTranslation
    {
        #region Constructors

        public DetectedTranslation(string name, DeserializeInstruction[] instructions)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (instructions == null) throw new ArgumentNullException(nameof(instructions));
            Name = name;
            DeserializeInstructions = instructions;
        }

        #endregion

        #region Properties

        public DeserializeInstruction[] DeserializeInstructions { get; }
        public string Name { get; }

        #endregion
    }
}