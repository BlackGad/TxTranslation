using System;

namespace Unclassified.Util
{
    public class SearchEventArgs : EventArgs
    {
        #region Constructors

        public SearchEventArgs()
        {
            BreakCurrentDepthSearch = false;
            IncludeInResult = false;
            MarkForDeeperSearch = true;
        }

        #endregion

        #region Properties

        public bool BreakCurrentDepthSearch { get; set; }
        public bool IncludeInResult { get; set; }
        public bool MarkForDeeperSearch { get; set; }

        #endregion
    }
}