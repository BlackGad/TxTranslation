using System.Collections.Generic;

namespace Unclassified.TxEditor.Models
{
    public class SerializedTranslation
    {
        #region Constructors

        public SerializedTranslation()
        {
            Cultures = new List<SerializedCulture>();
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public List<SerializedCulture> Cultures { get; set; }
        public bool IsTemplate { get; set; }

        #endregion
    }
}