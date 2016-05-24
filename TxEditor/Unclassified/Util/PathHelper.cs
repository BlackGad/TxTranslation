using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unclassified.TxEditor.Util
{
    public static class PathHelper
    {
        #region Static members

        /// <summary>
        ///     Enumerates directories by pattern.
        /// </summary>
        /// <param name="pattern">Search pattern.</param>
        /// <returns>Existing directories.</returns>
        public static IEnumerable<string> EnumerateDirectories(string pattern)
        {
            var wildcardChars = new[] { '*', '?' };

            if (!Path.IsPathRooted(pattern)) throw new ArgumentException("Path is not absolute");
            if (pattern == null) throw new InvalidOperationException();
            var containsWildcardChars = pattern.IndexOfAny(wildcardChars) >= 0;
            if (!containsWildcardChars && Directory.Exists(pattern)) return new[] { pattern };

            var pathRoot = Path.GetPathRoot(pattern);

            var subPathParts = pattern.Substring(pathRoot.Length).Split('\\', '/').Where(p => !string.IsNullOrEmpty(p)).ToList();
            var wildcardPartIndex = subPathParts.FindIndex(p => p.Any(s => wildcardChars.Contains(s)));
            if (wildcardPartIndex == -1) throw new InvalidDataException();

            var currentPath = Path.Combine(pathRoot, subPathParts.Take(wildcardPartIndex).Aggregate(string.Empty, Path.Combine));
            if (subPathParts[wildcardPartIndex] == "**")
            {
                var pathPostfix = string.Empty;
                var searchPattern = "*";

                if (subPathParts.Count > wildcardPartIndex + 1) searchPattern = subPathParts[wildcardPartIndex + 1];
                if (subPathParts.Count > wildcardPartIndex + 2)
                    pathPostfix = subPathParts.Skip(wildcardPartIndex + 2).Aggregate(string.Empty, Path.Combine);
                var directoriesEnumeration = Directory.EnumerateDirectories(currentPath, searchPattern, SearchOption.AllDirectories)
                                                      .SelectMany(d => EnumerateDirectories(Path.Combine(d, pathPostfix)));
                if (!string.IsNullOrEmpty(searchPattern)) directoriesEnumeration = directoriesEnumeration.Union(new[] { currentPath });
                return directoriesEnumeration;
            }
            else
            {
                var pathPostfix = string.Empty;
                if (subPathParts.Count > wildcardPartIndex + 1)
                    pathPostfix = subPathParts.Skip(wildcardPartIndex + 1).Aggregate(string.Empty, Path.Combine);

                var directories = Directory.EnumerateDirectories(currentPath, subPathParts[wildcardPartIndex], SearchOption.TopDirectoryOnly);
                return directories.SelectMany(d => EnumerateDirectories(Path.Combine(d, pathPostfix)));
            }
        }

        /// <summary>
        ///     Enumerates files by pattern.
        /// </summary>
        /// <param name="pattern">Search pattern.</param>
        /// <returns>Existing files.</returns>
        public static IEnumerable<string> EnumerateFiles(string pattern)
        {
            try
            {
                if (!Path.IsPathRooted(pattern)) throw new ArgumentException("Path is not absolute");
                var patternFilename = Path.GetFileName(pattern);
                var searchPattern = string.IsNullOrEmpty(patternFilename) ? "*" : patternFilename;
                var searchPath = Path.GetDirectoryName(pattern);
                if (searchPath == null) throw new InvalidOperationException();

                var directories = EnumerateDirectories(searchPath);
                return directories.SelectMany(d => Directory.EnumerateFiles(d, searchPattern));
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }

        #endregion
    }
}