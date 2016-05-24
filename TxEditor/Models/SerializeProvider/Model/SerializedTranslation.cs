namespace Unclassified.TxEditor.Models
{
    public class SerializedTranslation
    {
        #region Properties

        public SerializedCulture[] Cultures { get; set; }
        public bool IsTemplate { get; set; }

        #endregion

        #region Members

        public void Compose(SerializedTranslation translation)
        {
            foreach (var culture in translation.Cultures)
            {
            }
        }

        #endregion
    }
}