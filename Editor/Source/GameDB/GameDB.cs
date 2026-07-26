using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal sealed class GameDBSchemaFormatException : FormatException
    {
        internal int? FoundVersion { get; }
        internal int SupportedVersion { get; }

        internal GameDBSchemaFormatException(string message, int? foundVersion = null)
            : base(message)
        {
            FoundVersion = foundVersion;
            SupportedVersion = GameDBSchemaFormat.CurrentVersion;
        }
    }

    internal static class GameDBSchemaFormat
    {
        internal const int CurrentVersion = 4;

        internal static IDictionary<string, object> ParseAndValidate(string schemaJson)
        {
            if (!(JsonSerialization.Deserialize(schemaJson) is IDictionary<string, object> schema))
            {
                throw new FormatException("top level object not a dictionary");
            }

            if (!schema.TryGetValue("formatVersion", out var value))
            {
                throw new GameDBSchemaFormatException(
                    $"Schema is missing required 'formatVersion'. Regenerate or recreate it with this GameDB package (supported format version: {CurrentVersion}).");
            }

            if (!(value is long versionValue) || versionValue <= 0 || versionValue > int.MaxValue)
            {
                throw new GameDBSchemaFormatException(
                    $"Schema 'formatVersion' must be a positive 32-bit JSON integer (supported format version: {CurrentVersion}).");
            }

            var version = (int)versionValue;
            if (version > CurrentVersion)
            {
                throw new GameDBSchemaFormatException(
                    $"Schema format version {version} is newer than the supported version {CurrentVersion}. Open this project with a newer GameDB package.",
                    version);
            }

            if (version < CurrentVersion)
            {
                throw new GameDBSchemaFormatException(
                    $"Schema format version {version} is older than the supported version {CurrentVersion}. Recreate it with this GameDB package.",
                    version);
            }

            return schema;
        }
    }

    internal class GameDB : Singleton<GameDB>, IGameDB
    {
        internal static List<GameDBInternal> RuntimeDBs = new List<GameDBInternal>();

        private Dictionary<string, TableBase> m_tables = null;
        private GameDBDocument m_persistenceDocument;

        private int m_loadedInGameDB = -1;

        public Dictionary<string, TableBase> Tables => m_tables;

        public string ScopeName { get; set; }
        public string LoadedPath { get; set; }
        public bool LocalizationDB { get; set; }

        public bool Load(string gameDBPath)
        {
            m_persistenceDocument = null;
            try
            {
                var document = GameDBDocument.Load(ToAssetPath(gameDBPath));
                var state = document.SerializeCurrent();
                ImportOrThrow(state.DataJson, state.SchemaJson);
                LoadedPath = gameDBPath;
                m_persistenceDocument = document;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB: " + Path.Combine(Application.dataPath, gameDBPath));
                Debug.LogException(e);
                return false;
            }
        }

        public bool LoadRuntimeDB(int selectedInGameDBIndex, string gameDBPath)
        {
            try
            {
                var schemaJSON = File.ReadAllText(Path.Combine(Application.dataPath, GetSchemaPath(gameDBPath)));
                var imported = DeserializeSchema(schemaJSON);
                ImportFromRuntimeDB(imported.Tables, RuntimeDBs[selectedInGameDBIndex]);
                ApplyImportedState(imported);
                LoadedPath = gameDBPath;
                m_loadedInGameDB = selectedInGameDBIndex;
                m_persistenceDocument = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB: " + gameDBPath);
                Debug.LogException(e);
                return false;
            }
        }

        public bool Import(string jsonData, string jsonSchema)
        {
            try
            {
                ImportOrThrow(jsonData, jsonSchema);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load imported gameDB");
                Debug.LogException(e);
                return false;
            }
        }

        internal void ImportOrThrow(string jsonData, string jsonSchema)
        {
            var imported = DeserializeSchema(jsonSchema);
            GameDBSerializer.DeserializeData(imported.Tables, jsonData);
            ApplyImportedState(imported);
            m_loadedInGameDB = -1;
        }

        public bool AddTable(string tableName, KeyType type, object typeArg = null)
        {
            var success = false;

            if (!m_tables.ContainsKey(tableName))
            {
                m_tables.Add(tableName, new TableModel(tableName, type, typeArg));
                success = true;
            }

            return success;
        }

        public bool RemoveTable(string tableName)
        {
            return m_tables.Remove(tableName);
        }

        public bool RenameTable(string oldName, string newName)
        {
            if (!m_tables.TryGetValue(oldName, out var table) || m_tables.ContainsKey(newName))
            {
                return false;
            }

            m_tables.Remove(oldName);
            ((TableModel)table).Rename(newName);
            m_tables.Add(newName, table);
            return true;
        }

        public void Create(string gameDBPath)
        {
            CreateInMemory(gameDBPath);
            ScopeName = Path.GetFileNameWithoutExtension(gameDBPath);
            try
            {
                m_persistenceDocument = GameDBDocument.CreateNew(
                    ToAssetPath(gameDBPath), ScopeName, LocalizationDB);
            }
            catch (Exception e)
            {
                Debug.LogError("failed to create gameDB: " + gameDBPath);
                Debug.LogException(e);
                return;
            }

            Save();
        }

        internal void CreateInMemory(string gameDBPath)
        {
            ScopeName = string.Empty;
            LocalizationDB = false;
            m_loadedInGameDB = -1;
            m_persistenceDocument = null;
            LoadedPath = gameDBPath;
            m_tables = new Dictionary<string, TableBase>();
        }

        public bool Save()
        {
            try
            {
                if (m_persistenceDocument != null && m_persistenceDocument.HasPendingPostSaveWork)
                {
                    var pending = m_persistenceDocument.Save();
                    if (!pending.Success)
                    {
                        Debug.LogError("failed to save gameDB: " + LoadedPath + ". " + pending.Message);
                        return false;
                    }

                    if (GameDBModelCodec.ComputeRevision(this) == m_persistenceDocument.CurrentRevision)
                    {
                        return true;
                    }
                }

                var assetPath = ToAssetPath(LoadedPath);
                var currentRevision = GameDBModelCodec.ComputeRevision(this);
                var document = m_persistenceDocument;
                if (document == null)
                {
                    document = GameDBDocument.CreateReplacement(
                        assetPath, SerializeData(), SerializeSchema());
                }
                else if (currentRevision != document.CurrentRevision)
                {
                    document = document.CreateReplacement(SerializeData(), SerializeSchema());
                }

                var result = document.Save(new GameDBSaveOptions { ForceWrite = true });
                if (result.Success || result.FilesCommitted
                    || result.Status == GameDBSaveStatus.PersistenceStateUnknown)
                {
                    m_persistenceDocument = document;
                }

                if (result.FilesCommitted)
                {
                    AdoptDocumentState(document);
                }

                if (!result.Success)
                {
                    Debug.LogError("failed to save gameDB: " + LoadedPath + ". " + result.Message);
                }

                return result.Success;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to save gameDB: " + LoadedPath);
                Debug.LogException(e);
                return false;
            }
        }

        private static string ToAssetPath(string path)
        {
            var assetPath = path.Replace('\\', '/').TrimStart('/');
            return assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? assetPath
                : "Assets/" + assetPath;
        }

        public bool GetRawDataJSON(out string gameDBJSON)
        {
            var loaded = false;

            gameDBJSON = string.Empty;

            try
            {
                gameDBJSON = File.ReadAllText(Path.Combine(Application.dataPath, LoadedPath));

                loaded = true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB: " + LoadedPath);
                Debug.LogException(e);
            }

            return loaded;
        }

        public bool ImportRawDataJSON(string gameDBJSON)
        {
            var saved = false;

            try
            {
                var schemaJSON = File.ReadAllText(Path.Combine(Application.dataPath, GetSchemaPath(LoadedPath)));
                saved = Import(gameDBJSON, schemaJSON) && Save();

                if (!saved)
                {
                    Load(LoadedPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("failed to save gameDB: " + LoadedPath);
                Debug.LogException(e);
            }

            return saved;
        }

        public bool GetRawSchemaJSON(out string gameDBSchemaJSON)
        {
            var loaded = false;

            gameDBSchemaJSON = string.Empty;

            try
            {
                gameDBSchemaJSON = File.ReadAllText(Path.Combine(Application.dataPath, GetSchemaPath(LoadedPath)));

                loaded = true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB schema: " + LoadedPath);
                Debug.LogException(e);
            }

            return loaded;
        }

        public bool ReloadRuntimeDB()
        {
            var loaded = false;

            try
            {
                var dataJSON = SerializeData();

                loaded = RuntimeDBs[m_loadedInGameDB].Import(dataJSON) == null;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to reload gameDB");
                Debug.LogException(e);
            }

            return loaded;
        }

        public void AddRowToTable(string table, string key, Dictionary<string, object> data)
        {
            if (m_tables.ContainsKey(table))
            {
                var tableObj = m_tables[table] as TableModel;

                if (!tableObj.Data.ContainsKey(key))
                {
                    foreach (var fieldName in data.Keys)
                    {
                        if (!tableObj.Fields.ContainsKey(fieldName))
                        {
                            throw new ArgumentOutOfRangeException("Field", fieldName, $"No field exists in {table} Table");
                        }

                        if (!tableObj.Fields[fieldName].IsValueValid(data[fieldName]))
                        {
                            throw new InvalidCastException($"Data provided for field {fieldName} invalid. Expected type {tableObj.Fields[fieldName].Type} Got: {data[fieldName]}");
                        }
                    }

                    var row = new RowModel(key);

                    foreach (var fieldPair in tableObj.Fields)
                    {
                        if (data.ContainsKey(fieldPair.Key))
                        {
                            row.SetValue(fieldPair.Value.Name, row.DeserializeValue(data[fieldPair.Key], fieldPair.Value));
                        }
                        else
                        {
                            row.SetValue(fieldPair.Value.Name, tableObj.GetDefaultValue(fieldPair.Value));
                        }
                    }

                    tableObj.Data.Add(key, row);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(key), key, $"Key already exists in {table} Table");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(table), table, "No table found in GameDB");
            }
        }

        private static ImportedState DeserializeSchema(string schemaJSON)
        {
            var schema = GameDBSchemaFormat.ParseAndValidate(schemaJSON);
            var imported = new ImportedState();

            if (schema.ContainsKey("scope"))
            {
                imported.ScopeName = schema["scope"] as string;
            }

            if (schema.ContainsKey("localizationDB"))
            {
                imported.LocalizationDB = (bool)schema["localizationDB"];
            }

            if (!(schema["tables"] is IDictionary<string, object> tablesObjDic))
            {
                throw new FormatException("gamedb tables object not a dictionary");
            }

            foreach (var tablePair in tablesObjDic)
            {
                var tableModel = new TableModel(tablePair.Key);
                tableModel.DeserializeSchema(tablePair.Value);
                imported.Tables.Add(tablePair.Key, tableModel);
            }

            return imported;
        }

        private void ApplyImportedState(ImportedState imported)
        {
            ScopeName = imported.ScopeName;
            LocalizationDB = imported.LocalizationDB;
            m_tables = imported.Tables;
        }

        internal string SerializeSchema()
        {
            var tableSchemas = new Dictionary<string, object>();

            foreach (var tablePair in m_tables)
            {
                var table = (TableModel)tablePair.Value;
                tableSchemas.Add(tablePair.Key, table.SerializeSchema());
            }

            var json = JsonSerialization.Serialize(new Dictionary<string, object> {
                { "formatVersion", GameDBSchemaFormat.CurrentVersion },
                { "tables", tableSchemas },
                { "scope", ScopeName },
                { "localizationDB", LocalizationDB }
            });
            json = JsonHelper.FormatJson(json);

            return json;
        }

        internal string SerializeData()
        {
            var tables = new Dictionary<string, object>();

            foreach (var tablePair in m_tables)
            {
                var table = (TableModel)tablePair.Value;
                tables.Add(tablePair.Key, table.SerializeData());
            }

            var json = JsonSerialization.Serialize(new Dictionary<string, object> { { "tables", tables } });
            json = JsonHelper.FormatJson(json);

            return json;
        }

        private void AdoptDocumentState(GameDBDocument document)
        {
            var state = document.SerializeCurrent();
            var imported = DeserializeSchema(state.SchemaJson);
            GameDBSerializer.DeserializeData(imported.Tables, state.DataJson);
            ApplyImportedState(imported);
        }

        private static void ImportFromRuntimeDB(Dictionary<string, TableBase> tables, IGameDB runtimeDB)
        {
            foreach (var tablePair in tables)
            {
                if (!runtimeDB.Tables.ContainsKey(tablePair.Key))
                {
                    throw new FormatException("runtime gamedb missing table: " + tablePair.Key);
                }

                tablePair.Value.Import(runtimeDB.Tables[tablePair.Key]);
            }
        }

        private sealed class ImportedState
        {
            internal string ScopeName { get; set; } = string.Empty;
            internal bool LocalizationDB { get; set; }
            internal Dictionary<string, TableBase> Tables { get; }
                = new Dictionary<string, TableBase>();
        }

        private static string GetSchemaPath(string gameDBPath)
        {
            return Path.ChangeExtension(gameDBPath, ".schema.json");
        }
    }
}
