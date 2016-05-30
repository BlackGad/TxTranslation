using System;
using System.Reflection;
using System.Windows;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class EmbeddedResourceLocation : ISerializeLocation
    {
        #region Static members

        public static bool operator ==(EmbeddedResourceLocation left, EmbeddedResourceLocation right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EmbeddedResourceLocation left, EmbeddedResourceLocation right)
        {
            return !Equals(left, right);
        }

        #endregion

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

        /// <summary>
        ///     Determines whether the specified <see cref="T:System.Object" /> is equal to the current
        ///     <see cref="T:System.Object" />.
        /// </summary>
        /// <returns>
        ///     true if the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:System.Object" />;
        ///     otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EmbeddedResourceLocation)obj);
        }

        /// <summary>
        ///     Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        ///     A hash code for the current <see cref="T:System.Object" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Assembly != null ? Assembly.GetHashCode() : 0)*397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }

        #endregion

        #region ISerializeLocation Members

        public bool Equals(ISerializeLocation other)
        {
            return Equals(other as EmbeddedResourceLocation);
        }

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

        #region Members

        protected bool Equals(EmbeddedResourceLocation other)
        {
            return Equals(Assembly, other.Assembly) && string.Equals(Name, other.Name);
        }

        #endregion
    }
}