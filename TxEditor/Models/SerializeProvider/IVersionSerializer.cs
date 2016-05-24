namespace Unclassified.TxEditor.Models
{
    public interface IVersionSerializer : IVersionSerializerDescription
    {
        #region Members

        SerializedTranslation Deserialize(ISerializeLocation location);

        ISerializeLocation[] GetRelatedLocations(ISerializeLocation location);

        string GetUniqueName(ISerializeLocation location);

        bool IsValid(ISerializeLocation location);

        SerializeInstruction QuerySerializeInstructions(ISerializeLocation location, SerializedTranslation translation);

        #endregion
    }
}