using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Unclassified.TxEditor.Models.Versions;
using Unclassified.Util;

namespace Unclassified.TxEditor.Models
{
    public class SerializeProvider
    {
        #region Static members

        public static SerializeProvider Instance { get; }

        static SerializeProvider()
        {
            Instance = new SerializeProvider();
        }

        #endregion

        #region Constructors

        private SerializeProvider()
        {
            Version1 = new Version1Serializer();
            Version2 = new Version2Serializer();

            AvailableVersions = new[]
            {
                Version1,
                Version2
            };
        }

        #endregion

        #region Properties

        public IVersionSerializerDescription[] AvailableVersions { get; }
        public IVersionSerializerDescription Version1 { get; }
        public IVersionSerializerDescription Version2 { get; }

        #endregion

        #region Members

        public SerializedTranslation LoadFromEmbeddedResources(Assembly assembly, string resourceName)
        {
            var templateStream = assembly.GetManifestResourceStream(resourceName);
            if (templateStream == null)
                throw new Exception("The template dictionary is not an embedded resource in this assembly. This is a build error.");

            var document = new XmlDocument();
            document.Load(templateStream);

            var location = new EmbeddedResourceLocation(assembly, resourceName);
            var version = DetectVersion(location, document);
            if (version == null) throw new Exception("Unknown tx version");

            return version.Deserialize(location, document);
        }

        public SerializedTranslation LoadFromFile(string filename)
        {
            var document = new XmlDocument();
            document.Load(filename);
            var location = new FileLocation(filename);
            var version = DetectVersion(location, document);
            if (version == null) throw new Exception("Unknown tx version");

            return version.Deserialize(location, document);
        }

        public void SaveToFile(string filename, SerializedTranslation translation, IVersionSerializerDescription versionDescription)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (translation == null) throw new ArgumentNullException(nameof(translation));
            var version = versionDescription as IVersionSerializer;
            if (version == null) throw new ArgumentNullException(nameof(versionDescription));

            var baseLocation = new FileLocation(filename);
            var serializedResult = version.Serialize(baseLocation, translation);

            foreach (var instructionFragment in serializedResult.Fragments)
            {
                var actualLocation = instructionFragment.Location as FileLocation;
                if (actualLocation == null) continue;

                var xws = new XmlWriterSettings();
                xws.Encoding = Encoding.UTF8;
                xws.Indent = true;
                xws.IndentChars = "\t";
                xws.OmitXmlDeclaration = false;

                using (XmlWriter xw = XmlWriter.Create(actualLocation.Filename + ".tmp", xws))
                {
                    instructionFragment.Document.Save(xw);
                }

                File.Delete(actualLocation.Filename);
                File.Move(actualLocation.Filename + ".tmp", actualLocation.Filename);
            }
        }

        private IVersionSerializer DetectVersion(ISerializeLocation location, XmlDocument document)
        {
            return AvailableVersions.Enumerate<IVersionSerializer>().FirstOrDefault(r => r.IsValid(location, document));
        }

        #endregion
    }
}