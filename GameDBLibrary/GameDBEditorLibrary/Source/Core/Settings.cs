using GameDBLibrary;
using GameDBLibrary.MiniJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class Settings : Singleton<Settings>
    {
        public struct GoogleSheetsSettings
        {
            public string WebAppUrl;
            public string SheetID;
        }

        private List<string> m_gameDBPaths = new List<string>();
        private List<string> m_importedEnums = new List<string>();
        private Dictionary<string, GoogleSheetsSettings> m_googleSheets = new Dictionary<string, GoogleSheetsSettings>();

        public List<string> GameDBPaths
        {
            get { return m_gameDBPaths; }
            set { m_gameDBPaths = value; }
        }

        public List<string> ImportedEnums
        {
            get { return m_importedEnums; }
            set { m_importedEnums = value; }
        }

        public Dictionary<string, GoogleSheetsSettings> GoogleSheets
        {
            get { return m_googleSheets; }
            set { m_googleSheets = value; }
        }

        public string ExportPath { get; set; }
        public string BuildPath { get; set; }
        public string GameDBServer { get; set; }
        public string DownloadServer { get; set; }

        public Settings()
        {
            BuildPath = string.Empty;
        }

        public void Load()
        {
            try
            {
                m_gameDBPaths.Clear();

                string settingsJSON = File.ReadAllText(GetSettingsPath());

                var settings = Json.Deserialize(settingsJSON) as IDictionary<string, object>;

                if (settings != null)
                {
                    if (settings.ContainsKey("gameDBPaths")) {

                        List<object> gameDBPathList = settings["gameDBPaths"] as List<object>;

                        foreach (object pathObj in gameDBPathList) {
                            string path = pathObj as string;

                            if (path != null) {
                                if (File.Exists(Path.Combine(Application.dataPath, path))) {
                                    m_gameDBPaths.Add(path);
                                }
                            }
                        }
                    }

                    if (settings.ContainsKey("exportPath")) {
                        ExportPath = Convert.ToString(settings["exportPath"]);
                    }

                    if (settings.ContainsKey("importedEnums"))
                    {
                        List<object> importedEnums = settings["importedEnums"] as List<object>;
                        m_importedEnums = importedEnums.Select(s => (string)s).ToList();
                        m_importedEnums.Sort();
                    }

                    m_googleSheets.Clear();

                    if (settings.ContainsKey("googleSheets"))
                    {
                        var sheetsSettings = settings["googleSheets"] as IDictionary<string, object>;

                        if (sheetsSettings != null)
                        {
                            foreach (var pair in sheetsSettings)
                            {
                                var gsSettingsDic = pair.Value as IDictionary<string, object>;

                                if (gsSettingsDic != null)
                                {
                                    var gsSettings = new GoogleSheetsSettings();
                                    gsSettings.SheetID = gsSettingsDic["sheetID"] as string;
                                    gsSettings.WebAppUrl = gsSettingsDic["webAppUrl"] as string;

                                    m_googleSheets[pair.Key] = gsSettings;
                                }
                            }
                        }
                    }

                    if (settings.ContainsKey("buildPath"))
                    {
                        BuildPath = settings["buildPath"] as string;
                    }

                    if (settings.ContainsKey("gameDBServer"))
                    {
                        GameDBServer = settings["gameDBServer"] as string;
                    }

                    if (settings.ContainsKey("downloadServer"))
                    {
                        DownloadServer = settings["downloadServer"] as string;
                    }
                }
            }
            catch (Exception) { } //For now if we get an exception the settings will just be reset

            Save();
        }

        public void Save()
        {
            Dictionary<string, object> settingsDic = new Dictionary<string, object>();

            settingsDic.Add("gameDBPaths", m_gameDBPaths);
            settingsDic.Add("exportPath", ExportPath);
            settingsDic.Add("importedEnums", m_importedEnums);
            settingsDic.Add("buildPath", BuildPath);
            settingsDic.Add("gameDBServer", GameDBServer);
            settingsDic.Add("downloadServer", DownloadServer);

            Dictionary<string, object> gsSettingsDic = new Dictionary<string, object>();

            foreach (var pair in m_googleSheets)
            {
                gsSettingsDic.Add(pair.Key, new Dictionary<string, object> {
                    { "sheetID", pair.Value.SheetID },
                    { "webAppUrl", pair.Value.WebAppUrl },
                });
            }

            settingsDic.Add("googleSheets", gsSettingsDic);

            try
            {
                string json = Json.Serialize(settingsDic);
                json = JsonHelper.FormatJson(json);

                File.WriteAllText(GetSettingsPath(), json);
            }
            catch (Exception)
            {
                throw new Exception("DEAL WITH THIS!");
            }
        }

        private string GetSettingsPath()
        {
            return $"{Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(GameDBEditor)).Location)}/GameDBEditor.settings";
        }
    }
}
