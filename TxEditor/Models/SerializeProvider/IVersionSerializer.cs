using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public interface IVersionSerializer : IVersionSerializerDescription
    {
        #region Members

        SerializedTranslation Deserialize(ISerializeLocation location, XmlDocument document);

        bool IsValid(ISerializeLocation location, XmlDocument document);

        SerializeInstruction Serialize(ISerializeLocation location, SerializedTranslation translation);

        #endregion
    }
}