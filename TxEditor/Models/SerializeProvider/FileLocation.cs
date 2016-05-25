using System;
using System.IO;
using System.Text;
using System.Xml;

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

        #region ISerializeLocation Members

        public bool Exists()
        {
            return File.Exists(Filename);
        }

        public XmlDocument Load()
        {
            var document = new XmlDocument();
            document.Load(Filename);
            return document;
        }

        public void Save(XmlDocument document)
        {
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