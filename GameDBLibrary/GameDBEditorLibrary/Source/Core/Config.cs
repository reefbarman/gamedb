using System;
using System.Collections.Generic;
using System.IO;
using GameDBLibrary;
using GameDBLibrary.MiniJSON;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class Config : Singleton<Config>
    {
        public string EncryptionKey { get; set; }
        public string EncryptionSalt { get; set; }

        public void Load()
        {
            try
            {
                if (File.Exists(GetConfigPath()))
                {
                    string configJSON = File.ReadAllText(GetConfigPath());

                    var config = Json.Deserialize(configJSON) as IDictionary<string, object>;

                    if (config != null)
                    {
                        if (config.ContainsKey("encryption"))
                        {
                            var encryption = config["encryption"] as IDictionary<string, object>;

                            EncryptionKey = encryption["key"] as string;
                            EncryptionSalt = encryption["salt"] as string;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private string GetConfigPath()
        {
            return $"{Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(GameDBEditor)).Location)}/GameDBEditor.config";
        }
    }
}
