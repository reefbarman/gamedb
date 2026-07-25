using GameDBLibrary;
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

        private readonly List<string> m_gameDBPaths = new List<string>();
        private readonly List<string> m_importedEnums = new List<string>();
        private readonly Dictionary<string, GoogleSheetsSettings> m_googleSheets = new Dictionary<string, GoogleSheetsSettings>();

        public List<string> GameDBPaths
        {
            get { return m_gameDBPaths; }
            set
            {
                var paths = value?.ToArray() ?? Array.Empty<string>();
                m_gameDBPaths.Clear();
                m_gameDBPaths.AddRange(paths);
            }
        }

        public List<string> ImportedEnums
        {
            get { return m_importedEnums; }
            set
            {
                var enums = value?.ToArray() ?? Array.Empty<string>();
                m_importedEnums.Clear();
                m_importedEnums.AddRange(enums);
            }
        }

        public Dictionary<string, GoogleSheetsSettings> GoogleSheets => m_googleSheets;

        public string ExportPath { get; set; } = string.Empty;
        public string BuildPath { get; set; } = string.Empty;

        public void Load()
        {
            Reset();

            var settingsPath = GetSettingsPath();
            if (!File.Exists(settingsPath))
            {
                Save();
                return;
            }

            try
            {
                if (!(JsonSerialization.Deserialize(File.ReadAllText(settingsPath)) is IDictionary<string, object> settings))
                {
                    throw new FormatException("GameDB settings must contain a JSON object.");
                }

                ReadStringList(settings, "gameDBPaths", m_gameDBPaths);
                m_gameDBPaths.RemoveAll(path => !File.Exists(Path.Combine(Application.dataPath, path)));

                ReadStringList(settings, "importedEnums", m_importedEnums);
                m_importedEnums.Sort(StringComparer.Ordinal);

                ExportPath = ReadString(settings, "exportPath");
                BuildPath = ReadString(settings, "buildPath");
                ReadGoogleSheets(settings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load GameDB settings from {settingsPath}. Defaults will be used.\n{exception}");
                Reset();
            }

            Save();
        }

        public void Save()
        {
            var googleSheets = new Dictionary<string, object>();
            foreach (var pair in m_googleSheets)
            {
                googleSheets[pair.Key] = new Dictionary<string, object>
                {
                    { "sheetID", pair.Value.SheetID },
                    { "webAppUrl", pair.Value.WebAppUrl }
                };
            }

            var settings = new Dictionary<string, object>
            {
                { "gameDBPaths", m_gameDBPaths },
                { "exportPath", ExportPath },
                { "importedEnums", m_importedEnums },
                { "buildPath", BuildPath },
                { "googleSheets", googleSheets }
            };

            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonHelper.FormatJson(JsonSerialization.Serialize(settings)));
        }

        private void Reset()
        {
            m_gameDBPaths.Clear();
            m_importedEnums.Clear();
            m_googleSheets.Clear();
            ExportPath = string.Empty;
            BuildPath = string.Empty;
        }

        private void ReadGoogleSheets(IDictionary<string, object> settings)
        {
            if (!settings.TryGetValue("googleSheets", out var value) || !(value is IDictionary<string, object> sheets))
            {
                return;
            }

            foreach (var pair in sheets)
            {
                if (!(pair.Value is IDictionary<string, object> sheet))
                {
                    continue;
                }

                m_googleSheets[pair.Key] = new GoogleSheetsSettings
                {
                    SheetID = ReadString(sheet, "sheetID"),
                    WebAppUrl = ReadString(sheet, "webAppUrl")
                };
            }
        }

        private static void ReadStringList(IDictionary<string, object> source, string key, ICollection<string> destination)
        {
            if (!source.TryGetValue(key, out var value) || !(value is IEnumerable<object> values))
            {
                return;
            }

            foreach (var item in values.OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                destination.Add(item);
            }
        }

        private static string ReadString(IDictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;
        }

        private static string GetSettingsPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "GameDBSettings.json"));
        }
    }
}
