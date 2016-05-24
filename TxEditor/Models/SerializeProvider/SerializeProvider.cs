using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IVersionSerializer DetectSerializer(ISerializeLocation location)
        {
            return AvailableVersions.Enumerate<IVersionSerializer>().FirstOrDefault(r => r.IsValid(location));
        }

        public IEnumerable<UniqueTranslation> GetUniqueTranslationsFromFolder(string folder)
        {
            var processedFiles = new HashSet<string>();
            foreach (var file in Util.PathHelper.EnumerateFiles(folder.TrimEnd('\\') + "\\"))
            {
                var localFile = file.ToLowerInvariant();
                if (processedFiles.Contains(localFile)) continue;

                var extension = Path.GetExtension(localFile).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension)) continue;
                if (extension.EndsWith(".xml") || extension.EndsWith(".txd"))
                {
                    var location = new FileLocation(localFile);
                    var serializer = DetectSerializer(location);
                    if (serializer == null) continue;

                    var instructions = serializer.GetRelatedLocations(location)
                                                 .OfType<FileLocation>()
                                                 .Select(fileLocation => new DeserializeInstruction(fileLocation, serializer))
                                                 .ToArray();

                    foreach (var instruction in instructions)
                    {
                        processedFiles.Add(((FileLocation)instruction.Location).Filename);
                    }

                    yield return new UniqueTranslation(serializer.GetUniqueName(location), instructions);
                }
            }
        }

        //public IEnumerable<FolderLocation> ScanFolderForUniqueSets(string folder)
        //{

        //}

        public DeserializeInstruction LoadFrom(ISerializeLocation location, IVersionSerializerDescription serializerDescription = null)
        {
            var serializer = (serializerDescription ?? DetectSerializer(location)) as IVersionSerializer;
            if (serializer == null) throw new NotSupportedException("Unknown serializer");
            return new DeserializeInstruction(location, serializer);
        }

        public void SaveToFile(string filename, SerializedTranslation translation, IVersionSerializerDescription versionDescription)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (translation == null) throw new ArgumentNullException(nameof(translation));
            var version = versionDescription as IVersionSerializer;
            if (version == null) throw new ArgumentNullException(nameof(versionDescription));

            var baseLocation = new FileLocation(filename);
            var serializedResult = version.QuerySerializeInstructions(baseLocation, translation);

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
                    instructionFragment.Serialize().Save(xw);
                }

                File.Delete(actualLocation.Filename);
                File.Move(actualLocation.Filename + ".tmp", actualLocation.Filename);
            }
        }

        #endregion
    }
}