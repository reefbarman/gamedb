using System;
using System.IO;
using System.Text;
using GameDBLibrary;
using GameDBLibrary.LZF;
using UnityEngine;

namespace GameDBEditorLibrary
{
    public static class Builder
    {
        public static bool BuildGameDB(string json, string outputPath, string encryptionKey, string encryptionSalt)
        {
            var success = false;

            try
            {
                var compressed = CLZF2.Compress(Encoding.UTF8.GetBytes(json));

                if (compressed != null)
                {
                    var encrypted = Encryption.Encrypt(compressed, encryptionKey, encryptionSalt);

                    if (encrypted != null)
                    {
                        File.WriteAllBytes(outputPath, encrypted);
                        success = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return success;
        }
    }
}
