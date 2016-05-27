namespace Unclassified.TxEditor.Models
{
    public interface IVersionSerializer : IVersionSerializerDescription
    {
        #region Members

        DeserializeInstruction Deserialize(ISerializeLocation location);

        ISerializeLocation[] DetectRelatedLocations(ISerializeLocation location);

        bool IsValid(ISerializeLocation location);

        SerializeInstruction[] Serialize(ISerializeLocation location, SerializedTranslation translation);

        #endregion
    }
}