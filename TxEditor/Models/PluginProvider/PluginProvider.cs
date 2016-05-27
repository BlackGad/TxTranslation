using System.ComponentModel.Composition.Hosting;

namespace Unclassified.TxEditor.Models
{
    public class PluginProvider
    {
        #region Static members

        public static PluginProvider Instance { get; }

        static PluginProvider()
        {
            Instance = new PluginProvider();
        }

        #endregion

        #region Constructors

        private PluginProvider()
        {
            Catalog = new AggregateCatalog();
            Container = new CompositionContainer(Catalog, true);
            Catalog.Catalogs.Add(new DirectoryCatalog(".", "*.Plugin.dll"));
        }

        #endregion

        #region Properties

        private AggregateCatalog Catalog { get; }
        private CompositionContainer Container { get; }

        #endregion
    }
}