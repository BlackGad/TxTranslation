namespace Unclassified.TxEditor.Models
{
    public interface ISerializeDescription
    {
        #region Properties

        ISerializeLocation Location { get; }
        string Name { get; }
        IVersionSerializerDescription Serializer { get; }
        string ShortName { get; }

        #endregion
    }
}