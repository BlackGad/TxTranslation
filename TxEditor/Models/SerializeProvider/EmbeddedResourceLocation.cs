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

        #region Override members

        public override string ToString()
        {
            return "Embedded resource: " + Name + ", Assembly: " + Assembly.GetName().Name;
        }

        #endregion

        #region ISerializeLocation Members

        public void Backup()
        {
            if (!CanBackup()) throw new Exception(string.Format("Resource {0} could not be backed up.", Name));
        }

        public bool CanBackup()
        {
            return false;
        }

        public bool CanCleanBackup()
        {
            return false;
        }

        public bool CanLoad()
        {
            var templateStream = Assembly.GetManifestResourceStream(Name);
            return templateStream != null;
        }

        public bool CanRestore()
        {
            return false;
        }

        public bool CanSave()
        {
            return false;
        }

        public void CleanBackup()
        {
            if (!CanCleanBackup()) throw new Exception(string.Format("Resource backup {0} could not be cleaned.", Name));
        }

        public XmlDocument Load()
        {
            if (!CanLoad()) throw new Exception(string.Format("Resource {0} could not be loaded.", Name));
            var templateStream = Assembly.GetManifestResourceStream(Name);
            if (templateStream == null)
                throw new Exception(string.Format("The template dictionary {0} is not an embedded resource in {1} assembly. This is a build error.",
                                                  Name,
                                                  Assembly.GetName().Name));
            var document = new XmlDocument();
            document.Load(templateStream);
            return document;
        }

        public void Restore()
        {
            if (!CanRestore()) throw new Exception(string.Format("Resource {0} could not be restored.", Name));
        }

        public void Save(XmlDocument document)
        {
            if (!CanSave()) throw new Exception(string.Format("Resource {0} could not be saved.", Name));
            throw new NotSupportedException();
        }

        #endregion
    }
}