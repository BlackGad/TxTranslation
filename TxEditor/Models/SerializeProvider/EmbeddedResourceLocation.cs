using System;
using System.Reflection;
using System.Windows;
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

        #region Override members

        public override string ToString()
        {
            return "Embedded resource: " + Name + ", Assembly: " + Assembly.GetName().Name;
        }

        #endregion

        #region ISerializeLocation Members

        public Exception CanLoad()
        {
            var templateStream = Assembly.GetManifestResourceStream(Name);
            if (templateStream == null) throw new ResourceReferenceKeyNotFoundException();
            return null;
        }

        public Exception CanSave()
        {
            return new NotSupportedException();
        }

        public XmlDocument Load()
        {
            var error = CanLoad();
            if (error != null) throw new Exception(string.Format("Resource {0} could not be loaded.", Name), error);

            var templateStream = Assembly.GetManifestResourceStream(Name);
            if (templateStream == null)
                throw new Exception(string.Format("The template dictionary {0} is not an embedded resource in {1} assembly. This is a build error.",
                                                  Name,
                                                  Assembly.GetName().Name));
            var document = new XmlDocument();
            document.Load(templateStream);
            return document;
        }

        public ISerializeLocationBackup QueryBackup()
        {
            return null;
        }

        public void Save(XmlDocument document)
        {
            var error = CanSave();
            if (error != null) throw new Exception(string.Format("Resource {0} could not be saved.", Name), error);
        }

        #endregion
    }
}