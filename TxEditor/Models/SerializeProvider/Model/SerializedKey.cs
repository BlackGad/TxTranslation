namespace Unclassified.TxEditor.Models
{
    public class SerializedKey
    {
        #region Constructors

        public SerializedKey()
        {
            Count = -1;
        }

        #endregion

        #region Properties

        public bool AcceptMissing { get; set; }
        public bool AcceptPlaceholders { get; set; }
        public bool AcceptPunctuation { get; set; }
        public string Comment { get; set; }
        public int Count { get; set; }
        public string Key { get; set; }
        public int Modulo { get; set; }
        public string Text { get; set; }

        #endregion

        #region Members

        public int GetUniqueHash()
        {
            unchecked
            {
                var hashCode = Count;
                hashCode = (hashCode*397) ^ (Key != null ? Key.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Modulo;
                return hashCode;
            }
        }

        #endregion
    }
}