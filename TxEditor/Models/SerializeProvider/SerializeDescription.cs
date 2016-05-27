namespace Unclassified.TxEditor.Models
{
    public class SerializeDescription : ISerializeDescription
    {
        #region Constructors

        public SerializeDescription(string name, string shortName)
        {
            Name = name;
            ShortName = shortName;
        }

        #endregion

        #region Properties

        public string Name { get; }
        public string ShortName { get; }

        #endregion
    }
}