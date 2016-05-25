using System;
using System.Reflection;
using System.Xml;

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

        #region ISerializeLocation Members

        public bool Exists()
        {
            var templateStream = Assembly.GetManifestResourceStream(Name);
            return templateStream != null;
        }

        public XmlDocument Load()
        {
            var templateStream = Assembly.GetManifestResourceStream(Name);
            if (templateStream == null)
                throw new Exception("The template dictionary is not an embedded resource in this assembly. This is a build error.");

            var document = new XmlDocument();
            document.Load(templateStream);
            return document;
        }

        public void Save(XmlDocument document)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}