using System.Collections.Generic;

namespace Unclassified.TxEditor.Models
{
    public class SerializedCulture
    {
        #region Constructors

        public SerializedCulture()
        {
            Keys = new List<SerializedKey>();
        }

        #endregion

        #region Properties

        public bool IsPrimary { get; set; }
        public List<SerializedKey> Keys { get; set; }

        public string Name { get; set; }

        #endregion
    }
}