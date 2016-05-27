using System;
using System.Collections.Generic;
using Unclassified.TxEditor.ViewModels;
using Unclassified.Util;

namespace Unclassified.TxEditor.UI
{
    public static class TreeViewHelper
    {
        #region Static members

        internal static TreeViewItemViewModel FindAncestor(this TreeViewItemViewModel vm,
                                                           Func<TreeViewItemViewModel, bool> predicate)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var model = vm.Parent;
            while (model != null)
            {
                if (predicate(model)) return model;
                model = model.Parent;
            }

            return null;
        }

        internal static IEnumerable<TreeViewItemViewModel> FindViewModels(this TreeViewItemViewModel vm,
                                                                          Action<ItemSearchEventArgs<TreeViewItemViewModel>> searchArgs)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (searchArgs == null) throw new ArgumentNullException("searchArgs");

            var result = new List<TreeViewItemViewModel>();
            foreach (var child in vm.Children)
            {
                var args = new ItemSearchEventArgs<TreeViewItemViewModel>(child);
                searchArgs(args);

                if (args.IncludeInResult) result.Add(child);
                if (args.MarkForDeeperSearch) result.AddRange(FindViewModels(child, searchArgs));
                if (args.BreakCurrentDepthSearch) break;
            }

            return result;
        }

        #endregion
    }
}