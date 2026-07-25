using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class GameDB : Singleton<GameDB>, IGameDB
    {
        internal static List<GameDBInternal> RuntimeDBs = new List<GameDBInternal>();

        private Dictionary<string, TableBase> m_tables = null;

        private int m_loadedInGameDB = -1;

        public Dictionary<string, TableBase> Tables => m_tables;

        public string ScopeName { get; set; }
        public string LoadedPath { get; set; }
        public bool LocalizationDB { get; set; }

        public bool Load(string gameDBPath)
        {
            var loaded = false;

            try
            {
                var schemaJSON = File.ReadAllText(Path.Combine(Application.dataPath, GetSchemaPath(gameDBPath)));
                var gameDBJSON = File.ReadAllText(Path.Combine(Application.dataPath, gameDBPath));

                loaded = Import(gameDBJSON, schemaJSON);

                LoadedPath = gameDBPath;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB: " + Path.Combine(Application.dataPath, gameDBPath));
                Debug.LogException(e);
            }

            return loaded;
        }

        public bool LoadRuntimeDB(int selectedInGameDBIndex, string gameDBPath)
        {
            var loaded = false;

            ScopeName = string.Empty;
            LocalizationDB = false;
            m_tables = null;

            LoadedPath = string.Empty;
            m_loadedInGameDB = -1;

            try
            {
                var schemaJSON = File.ReadAllText(Path.Combine(Application.dataPath, GetSchemaPath(gameDBPath)));

                DeserializeSchema(schemaJSON);

                ImportFromRuntimeDB(RuntimeDBs[selectedInGameDBIndex]);

                LoadedPath = gameDBPath;
                m_loadedInGameDB = selectedInGameDBIndex;
                loaded = true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load gameDB: " + gameDBPath);
                Debug.LogException(e);
            }

            return loaded;
        }

        public bool Import(string jsonData, string jsonSchema)
        {
            var loaded = false;

            ScopeName = string.Empty;
            LocalizationDB = false;
            m_tables = null;

            m_loadedInGameDB = -1;

            try
            {
                DeserializeSchema(jsonSchema);
                GameDBSerializer.DeserializeData(m_tables, jsonData);

                loaded = true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load imported gameDB");
                Debug.LogException(e);
            }

            return loaded;
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
            Save();
        }

        internal void CreateInMemory(string gameDBPath)
        {
            ScopeName = string.Empty;
            LocalizationDB = false;
            m_loadedInGameDB = -1;
            LoadedPath = gameDBPath;
            m_tables = new Dictionary<string, TableBase>();
        }

        public bool Save()
        {
            try
            {
                var dataJSON = SerializeData();
                var schemaJSON = SerializeSchema();
                var dataPath = Path.Combine(Application.dataPath, LoadedPath);
                var schemaPath = Path.Combine(Application.dataPath, GetSchemaPath(LoadedPath));

                SavePair(dataPath, dataJSON, schemaPath, schemaJSON);

                if (dataPath.Contains("Assets"))
                {
                    AssetDatabase.ImportAsset(dataPath.Substring(dataPath.IndexOf("Assets")), ImportAssetOptions.ForceUpdate);
                    AssetDatabase.ImportAsset(schemaPath.Substring(schemaPath.IndexOf("Assets")), ImportAssetOptions.ForceUpdate);
                }

                GameDBEditor.OnGameDBSaved?.Invoke(ScopeName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to save gameDB: " + LoadedPath);
                Debug.LogException(e);
                return false;
            }
        }

        private static void SavePair(string dataPath, string dataJSON, string schemaPath, string schemaJSON)
        {
            var directory = Path.GetDirectoryName(dataPath);
            Directory.CreateDirectory(directory);

            var operationId = Guid.NewGuid().ToString("N");
            var dataTemporaryPath = dataPath + "." + operationId + ".tmp";
            var schemaTemporaryPath = schemaPath + "." + operationId + ".tmp";
            var dataBackupPath = dataPath + "." + operationId + ".bak";
            var schemaBackupPath = schemaPath + "." + operationId + ".bak";
            var dataExisted = File.Exists(dataPath);
            var schemaExisted = File.Exists(schemaPath);

            try
            {
                File.WriteAllText(dataTemporaryPath, dataJSON);
                File.WriteAllText(schemaTemporaryPath, schemaJSON);
                ReplaceFile(dataTemporaryPath, dataPath, dataBackupPath, dataExisted);

                try
                {
                    ReplaceFile(schemaTemporaryPath, schemaPath, schemaBackupPath, schemaExisted);
                }
                catch
                {
                    RestoreFile(dataPath, dataBackupPath, dataExisted);
                    RestoreFile(schemaPath, schemaBackupPath, schemaExisted);
                    throw;
                }
            }
            finally
            {
                DeleteFile(dataTemporaryPath);
                DeleteFile(schemaTemporaryPath);
                DeleteFile(dataBackupPath);
                DeleteFile(schemaBackupPath);
            }
        }

        private static void ReplaceFile(string temporaryPath, string destinationPath, string backupPath, bool destinationExisted)
        {
            if (destinationExisted)
            {
                File.Replace(temporaryPath, destinationPath, backupPath);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }

        private static void RestoreFile(string destinationPath, string backupPath, bool destinationExisted)
        {
            if (destinationExisted && File.Exists(backupPath))
            {
                File.Copy(backupPath, destinationPath, true);
            }
            else if (!destinationExisted)
            {
                DeleteFile(destinationPath);
            }
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
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

        private void DeserializeSchema(string schemaJSON)
        {
            var tables = new Dictionary<string, TableBase>();

            if (!(JsonSerialization.Deserialize(schemaJSON) is IDictionary<string, object> gameDBSchemaDic))
            {
                throw new FormatException("top level object not a dictionary");
            }

            if (gameDBSchemaDic.ContainsKey("scope"))
            {
                ScopeName = gameDBSchemaDic["scope"] as string;
            }

            if (gameDBSchemaDic.ContainsKey("localizationDB"))
            {
                LocalizationDB = (bool)gameDBSchemaDic["localizationDB"];
            }

            //TODO: test how this handles no tables key
            if (!(gameDBSchemaDic["tables"] is IDictionary<string, object> tablesObjDic))
            {
                throw new FormatException("gamedb tables object not a dictionary");
            }

            foreach (var tablePair in tablesObjDic)
            {
                TableModel tableModel = new TableModel(tablePair.Key);
                tableModel.DeserializeSchema(tablePair.Value);

                tables.Add(tablePair.Key, tableModel);
            }

            m_tables = tables;
        }

        internal string SerializeSchema()
        {
            var tableSchemas = new Dictionary<string, object>();

            foreach (var tablePair in m_tables)
            {
                var table = (TableModel)tablePair.Value;
                tableSchemas.Add(tablePair.Key, table.SerializeSchema());
            }

            var json = JsonSerialization.Serialize(new Dictionary<string, object> { { "tables", tableSchemas }, { "scope", ScopeName }, { "localizationDB", LocalizationDB } });
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

        private void ImportFromRuntimeDB(IGameDB runtimeDB)
        {
            foreach (var tablePair in m_tables)
            {
                if (!runtimeDB.Tables.ContainsKey(tablePair.Key))
                {
                    throw new FormatException("runtime gamedb missing table: " + tablePair.Key);
                }

                tablePair.Value.Import(runtimeDB.Tables[tablePair.Key]);
            }
        }


        private static string GetSchemaPath(string gameDBPath)
        {
            return Path.ChangeExtension(gameDBPath, ".schema.json");
        }
    }
}
