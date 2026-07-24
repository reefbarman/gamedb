using System;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal static class Utils
    {
        public static string GetRelativeDataPath(string path) {
            path = path.Replace(Application.dataPath, "");

            if (path.Length > 0) {
                if (path[0] == '/')
                {
                    path = path.Substring(1);
                }
            }
            else {
                path = ".";
            }

            return path;
        }

        public static T[] Concat<T>(this T[] x, T[] y)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (y == null) throw new ArgumentNullException(nameof(y));

            int oldLen = x.Length;
            Array.Resize<T>(ref x, x.Length + y.Length);
            Array.Copy(y, 0, x, oldLen, y.Length);
            return x;
        }
    }
}
