namespace Unclassified.TxEditor.Models
{
    public interface IVersionSerializerDescription
    {
        #region Properties

        string Name { get; }

        #endregion

        #region Members

        ISerializeDescription DescribeLocation(ISerializeLocation location);

        #endregion
    }
}