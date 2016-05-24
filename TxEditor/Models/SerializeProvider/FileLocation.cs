using System;

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
    }
}