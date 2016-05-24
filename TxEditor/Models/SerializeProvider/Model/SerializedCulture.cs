namespace Unclassified.TxEditor.Models
{
    public class SerializedCulture
    {
        #region Properties

        public bool IsPrimary { get; set; }
        public SerializedKey[] Keys { get; set; }

        public string Name { get; set; }

        #endregion
    }
}