using System.Reflection;

namespace Unclassified.TxEditor.Models
{
    public class EmbeddedResourceLocation : ISerializeLocation
    {
        #region Constructors

        public EmbeddedResourceLocation(Assembly assembly, string name)
        {
            Assembly = assembly;
            Name = name;
        }

        #endregion

        #region Properties

        public Assembly Assembly { get; }

        public string Name { get; }

        #endregion
    }
}