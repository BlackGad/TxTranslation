using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Unclassified.TxEditor.Models
{
    public class FileLocation : ISerializeLocation
    {
        #region Static members

        public static bool operator ==(FileLocation left, FileLocation right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FileLocation left, FileLocation right)
        {
            return !Equals(left, right);
        }

        #endregion

        #region Constructors

        public FileLocation(string filename)
        {
            if (string.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            Filename = filename.ToLowerInvariant();
        }

        #endregion

        #region Properties

        public string Filename { get; }

        #endregion

        #region Override members

        public override string ToString()
        {
            return Filename;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FileLocation)obj);
        }

        public override int GetHashCode()
        {
            return Filename != null ? Filename.GetHashCode() : 0;
        }

        #endregion

        #region ISerializeLocation Members

        public bool Equals(ISerializeLocation other)
        {
            return Equals(other as FileLocation);
        }

        public XmlDocument Load()
        {
            var error = CanLoad();
            if (error != null) throw new Exception(string.Format("File {0} could not be loaded.", Filename), error);

            var document = new XmlDocument();
            document.Load(Filename);
            return document;
        }

        public ISerializeLocationBackup QueryBackup()
        {
            return new LocationBackup(this);
        }

        public void Save(XmlDocument document)
        {
            var error = CanSave();
            if (error != null) throw new Exception(string.Format("File could not be saved.", Filename), error);

            if (document == null) throw new ArgumentNullException(nameof(document));
            var xws = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t",
                OmitXmlDeclaration = false
            };

            using (XmlWriter xw = XmlWriter.Create(Filename + ".tmp", xws))
            {
                document.Save(xw);
            }

            File.Delete(Filename);
            File.Move(Filename + ".tmp", Filename);
        }

        public Exception CanLoad()
        {
            if (!File.Exists(Filename)) return new FileNotFoundException(null, Filename);
            return null;
        }

        public Exception CanSave()
        {
            var fi = new FileInfo(Filename);
            if (fi.Exists && fi.IsReadOnly) return new Exception(string.Format("Target file {0} is read only.", Filename));
            return null;
        }

        #endregion

        #region Members

        protected bool Equals(FileLocation other)
        {
            return string.Equals(Filename, other.Filename);
        }

        #endregion

        #region Nested type: LocationBackup

        class LocationBackup : ISerializeLocationBackup
        {
            private readonly FileLocation _location;

            #region Constructors

            public LocationBackup(FileLocation location)
            {
                _location = location;
            }

            #endregion

            #region ISerializeLocationBackup Members

            public void Backup()
            {
                var error = CanBackup();
                if (error != null) throw new Exception(string.Format("File {0} could not be backed up.", _location.Filename), error);
                File.Copy(_location.Filename, _location.Filename + ".bak", true);
            }

            public Exception CanBackup()
            {
                return _location.CanLoad();
            }

            public Exception CanCleanBackup()
            {
                var filename = _location.Filename + ".bak";
                if (!File.Exists(filename)) return new FileNotFoundException(null, filename);
                return null;
            }

            public Exception CanRestore()
            {
                var filename = _location.Filename + ".bak";
                if (!File.Exists(filename)) return new FileNotFoundException(null, filename);
                return null;
            }

            public void CleanBackup()
            {
                var error = CanCleanBackup();
                if (error != null) throw new Exception(string.Format("File {0} backup could not be cleaned.", _location.Filename), error);
                File.Delete(_location.Filename + ".bak");
            }

            public void Restore()
            {
                var error = CanRestore();
                if (error != null) throw new Exception(string.Format("File {0} could not be restored.", _location.Filename), error);

                File.Copy(_location.Filename + ".bak", _location.Filename, true);
            }

            #endregion
        }

        #endregion
    }
}