using System;
using System.Security.Cryptography;
using System.Text;

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
        /// Gets the MD5 checksum of a byte array.
        /// </summary>
        /// <param name="input">The byte array to generate a checksum for.</param>
        /// <returns>A MD5 checksum string</returns>
        [Obsolete(LegacyRemoteApi.Message)]
        public static string GetChecksum(byte[] input)
        {
            var sb = new StringBuilder();

            MD5 md5 = new MD5CryptoServiceProvider();
            var hashBytes = md5.ComputeHash(input);

            foreach (byte bt in hashBytes)
            {
                sb.Append(bt.ToString("x2"));
            }

            return sb.ToString();
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