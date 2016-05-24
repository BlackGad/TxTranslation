using System;
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

        public XmlDocument GetDocument()
        {
            var document = new XmlDocument();
            document.Load(Filename);
            return document;
        }

        #endregion
    }
}