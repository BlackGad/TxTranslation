using System;
using System.Collections.Generic;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models
{
    public static class SerializeInstructionExtensions
    {
        #region Static members

        public static IEnumerable<SerializeInstructionException> BatchOperation(this IEnumerable<SerializeInstruction> instructions,
                                                                                Action<SerializeInstruction> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var instruction in instructions.Enumerate())
            {
                Exception error = null;
                try
                {
                    action(instruction);
                }
                catch (Exception e)
                {
                    error = e;
                }
                if (error != null) yield return new SerializeInstructionException(instruction, error);
            }
        }

        #endregion
    }
}