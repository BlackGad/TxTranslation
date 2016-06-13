using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unclassified.TxEditor.Models.Versions;
using Unclassified.TxEditor.Util;
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

        public IVersionSerializerDescription DetectSerializer(ISerializeLocation location)
        {
            return AvailableVersions.Enumerate<IVersionSerializer>().FirstOrDefault(r => r.IsValid(location));
        }

        public IEnumerable<DetectedTranslation> DetectUniqueTranslations(string folder)
        {
            var locations = new List<ISerializeLocation>();
            foreach (var file in PathHelper.EnumerateFiles(folder.TrimEnd('\\') + "\\"))
            {
                var localFile = file.ToLowerInvariant();
                var extension = Path.GetExtension(localFile).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension)) continue;
                if (extension.EndsWith(".xml") || extension.EndsWith(".txd")) locations.Add(new FileLocation(localFile));
            }

            return DetectUniqueTranslations(locations.ToArray());
        }

        public IEnumerable<DetectedTranslation> DetectUniqueTranslations(params ISerializeLocation[] locations)
        {
            var processed = new HashSet<ISerializeLocation>();
            var available = new HashSet<ISerializeLocation>(locations);

            foreach (var location in locations)
            {
                if (processed.Contains(location)) continue;

                var serializer = (IVersionSerializer)DetectSerializer(location);
                if (serializer == null) continue;

                var detectedRelatedLocations = serializer.DetectRelatedLocations(location).ToList();
                var relatedFromAvailable = detectedRelatedLocations.Where(l => available.Contains(l)).ToList();

                relatedFromAvailable.ForEach(l => processed.Add(l));

                var relatedMissedInstructions = detectedRelatedLocations.Except(relatedFromAvailable).Select(l => serializer.Deserialize(l)).ToArray();
                var instructions = relatedFromAvailable.Select(l => serializer.Deserialize(l)).ToArray();

                yield return new DetectedTranslation(serializer.DescribeLocation(location), instructions, relatedMissedInstructions);
            }
        }

        public DeserializeInstruction LoadFrom(ISerializeLocation location, IVersionSerializerDescription serializerDescription = null)
        {
            var serializer = (serializerDescription ?? DetectSerializer(location)) as IVersionSerializer;
            if (serializer == null) throw new NotSupportedException("Unknown serializer");
            return serializer.Deserialize(location);
        }

        public SerializeInstruction[] SaveTo(SerializedTranslation translation,
                                             ISerializeLocation location,
                                             IVersionSerializerDescription serializerDescription)
        {
            if (translation == null) throw new ArgumentNullException(nameof(translation));
            if (location == null) throw new ArgumentNullException(nameof(location));
            var serializer = serializerDescription as IVersionSerializer;
            if (serializer == null) throw new NotSupportedException("Unknown serializer");

            return serializer.Serialize(location, translation);
        }

        #endregion
    }
}