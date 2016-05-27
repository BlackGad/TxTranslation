using System;
using System.IO;
using System.Text;
using System.Xml;
using Unclassified.TxLib;

namespace Unclassified.TxEditor.Models
{
    public class FileLocation : ISerializeLocation
    {
        #region Constructors

        public FileLocation(string filename)
        {
            if (string.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            Filename = filename;
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

        #endregion

        #region ISerializeLocation Members

        public void Backup()
        {
            if (!CanBackup()) throw new Exception(string.Format("File {0} could not be backed up.", Filename));
            File.Copy(Filename, Filename + ".bak", true);
        }

        public bool CanBackup()
        {
            return CanLoad();
        }

        public bool CanCleanBackup()
        {
            return File.Exists(Filename + ".bak");
        }

        public bool CanLoad()
        {
            return File.Exists(Filename);
        }

        public bool CanRestore()
        {
            return File.Exists(Filename + ".bak");
        }

        public bool CanSave()
        {
            var fi = new FileInfo(Filename);
            if (fi.Exists && fi.IsReadOnly) return false;
            return true;
        }

        public void CleanBackup()
        {
            if (!CanCleanBackup()) throw new Exception(string.Format("File {0} backup could not be cleaned.", Filename));
            File.Delete(Filename + ".bak");
        }

        public XmlDocument Load()
        {
            if (!CanLoad()) throw new Exception(string.Format("File {0} could not be loaded.", Filename));

            var document = new XmlDocument();
            document.Load(Filename);
            return document;
        }

        public void Restore()
        {
            if (!CanRestore()) throw new Exception(string.Format("File {0} could not be restored.", Filename));
            File.Copy(Filename + ".bak", Filename, true);
        }

        public void Save(XmlDocument document)
        {
            if (!CanSave()) throw new Exception(string.Format("File {0} could not be saved.", Filename));

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

        #endregion
    }
}