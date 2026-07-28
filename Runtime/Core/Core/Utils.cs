using System;

namespace GameDBLibrary
{
    /// <summary>
    /// Provide utility methods for working with GameDBs
    /// </summary>
    public
        static class Utils
    {
        internal static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        /// <summary>
        /// Combines two URL sub paths.
        /// </summary>
        /// <param name="url1">The first path of the url to combine.</param>
        /// <param name="url2">The second path of the url to combine.</param>
        /// <returns></returns>
        public static string UrlCombine(string url1, string url2)
        {
            if (url1.Length == 0)
            {
                return url2;
            }

            if (url2.Length == 0)
            {
                return url1;
            }

            url1 = url1.TrimEnd('/', '\\');
            url2 = url2.TrimStart('/', '\\');

            return $"{url1}/{url2}";
        }
    }
}