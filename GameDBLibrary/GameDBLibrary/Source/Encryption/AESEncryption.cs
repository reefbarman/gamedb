using System.IO;
using System.Security.Cryptography;
using System.Text;
using Random = System.Random;

namespace GameDBLibrary
{
    internal class AESEncryption
    {
        private const int AESBlockSize = 16;
        private static int KeySize = 256;

        public static byte[] Encrypt(byte[] data, string key, string salt)
        {
            Random rnd = new Random();
            byte[] initialVectorBytes = new byte[AESBlockSize];
            rnd.NextBytes(initialVectorBytes);

            byte[] saltValueBytes = Encoding.UTF8.GetBytes(salt);
            byte[] keyBytes = new Rfc2898DeriveBytes(key, saltValueBytes).GetBytes(KeySize / 8);

            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                symmetricKey.Mode = CipherMode.CBC;

                using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initialVectorBytes))
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(data, 0, data.Length);
                            cryptoStream.FlushFinalBlock();

                            using (MemoryStream finalStream = new MemoryStream())
                            {
                                finalStream.Write(initialVectorBytes, 0, initialVectorBytes.Length);

                                var encrypted = memStream.ToArray();
                                finalStream.Write(encrypted, 0, encrypted.Length);

                                var outBytes = finalStream.ToArray();
                                return outBytes;
                            }
                        }
                    }
                }
            }
        }

        public static byte[] Decrypt(byte[] inData, string key, string salt)
        {
            byte[] initialVectorBytes = new byte[AESBlockSize];
            byte[] saltValueBytes = Encoding.UTF8.GetBytes(salt);
            byte[] keyBytes = new Rfc2898DeriveBytes(key, saltValueBytes).GetBytes(KeySize / 8);
            byte[] encryptedData = new byte[inData.Length - AESBlockSize];
            byte[] plainTextBytes = new byte[encryptedData.Length];

            using (MemoryStream inStream = new MemoryStream(inData))
            {
                inStream.Read(initialVectorBytes, 0, AESBlockSize);
                inStream.Read(encryptedData, 0, encryptedData.Length);
            }

            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                symmetricKey.Mode = CipherMode.CBC;

                using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initialVectorBytes))
                {
                    using (MemoryStream memStream = new MemoryStream(encryptedData))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
                        {
                            int byteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

                            using (MemoryStream outStream = new MemoryStream(plainTextBytes, 0, byteCount))
                            {
                                return outStream.ToArray();
                            }
                        }
                    }
                }
            }
        }
    }
}
