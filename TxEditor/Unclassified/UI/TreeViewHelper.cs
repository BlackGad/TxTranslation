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
            if (vm == null) return null;
            predicate = predicate ?? (a => true);

            var model = vm.Parent;
            while (model != null)
            {
                if (predicate(model)) return model;
                model = model.Parent;
            }

            return null;
        }

        internal static RootKeyViewModel FindRoot(this TreeViewItemViewModel key)
        {
            Func<TreeViewItemViewModel, bool> predicate = vm => vm is RootKeyViewModel;
            if (predicate(key)) return key as RootKeyViewModel;
            return key.FindAncestor(predicate) as RootKeyViewModel;
        }

        internal static IEnumerable<TreeViewItemViewModel> FindViewModels(this TreeViewItemViewModel vm,
                                                                          Action<ItemSearchEventArgs<TreeViewItemViewModel>> searchArgs)
        {
            var result = new List<TreeViewItemViewModel>();
            searchArgs = searchArgs ?? (args => { });
            if (vm == null) return result;

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