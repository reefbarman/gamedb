using System;
using System.IO;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal static class Utils
    {
        public static string GetRelativeDataPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var assetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(fullPath, assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return ".";
            }

            var assetsPrefix = assetsPath + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath.Substring(assetsPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        public static T[] Concat<T>(this T[] x, T[] y)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (y == null) throw new ArgumentNullException(nameof(y));

            int oldLen = x.Length;
            Array.Resize(ref x, x.Length + y.Length);
            Array.Copy(y, 0, x, oldLen, y.Length);
            return x;
        }
    }
}
