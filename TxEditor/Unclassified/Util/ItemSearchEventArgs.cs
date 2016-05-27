using System;

namespace Unclassified.Util
{
    public class ItemSearchEventArgs : ItemSearchEventArgs<object>
    {
        #region Constructors

        public ItemSearchEventArgs(object item) : base(item)
        {
        }

        #endregion
    }

    public class ItemSearchEventArgs<T> : SearchEventArgs
    {
        #region Constructors

        public ItemSearchEventArgs(T item)
        {
            if (item == null) throw new ArgumentNullException("item");
            Item = item;
        }

        #endregion

        #region Properties

        public T Item { get; }

        #endregion
    }
}