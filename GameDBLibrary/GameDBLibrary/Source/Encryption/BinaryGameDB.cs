using GameDBLibrary.LZF;
using System.Text;

namespace GameDBLibrary
{
    /// <summary>
    /// BinaryGameDB provides utility methods for Serializing and Deserializing 
    /// GameDBs to and from binary representations that are encrypted and compressed.
    /// </summary>
#if FREE_VERSION
    internal
#else
    public
#endif
    class BinaryGameDB
    {
        /// <summary>
        /// Serializes the specified json into a binary blob.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="encryptionKey">The encryption key.</param>
        /// <param name="encryptionSalt">The encryption salt.</param>
        /// <returns>A binary blob encrypted and compressed of the passed in JSON</returns>
        /// <exception cref="System.Exception">Throws an exception if it fails to encrypt or compress the json</exception>
        public static byte[] Serialize(string json, string encryptionKey, string encryptionSalt)
        {
            byte[] output = null;

            var compressed = CLZF2.Compress(Encoding.UTF8.GetBytes(json));

            if (compressed != null)
            {
                var encrypted = AESEncryption.Encrypt(compressed, encryptionKey, encryptionSalt);

                if (encrypted != null)
                {
                    output = encrypted;
                }
            }

            return output;
        }

        /// <summary>
        /// Deserializes the specified data in a JSON string.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="encryptionKey">The encryption key.</param>
        /// <param name="encryptionSalt">The encryption salt.</param>
        /// <returns>A JSON string decompressed and decrypted from the data passed in.</returns>
        /// <exception cref="System.Exception">Throws an exception if it fails to decrypt or decompress the json</exception>
        public static string Deserialize(byte[] data, string encryptionKey, string encryptionSalt)
        {
            string json = null;

            var decrypted = AESEncryption.Decrypt(data, encryptionKey, encryptionSalt);

            if (decrypted != null)
            {
                var decompressed = CLZF2.Decompress(decrypted);
                json = Encoding.UTF8.GetString(decompressed);
            }

            return json;
        }
    }
}
