using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameDBEditorLibrary.Documents
{
    internal sealed class GameDBSerializedState
    {
        internal string DataJson { get; }
        internal string SchemaJson { get; }
        internal string Revision { get; }

        internal GameDBSerializedState(string dataJson, string schemaJson, string revision)
        {
            DataJson = dataJson;
            SchemaJson = schemaJson;
            Revision = revision;
        }
    }

    internal static class GameDBModelCodec
    {
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;
        private static readonly object TypeResolutionGate = new object();

        internal static GameDBSerializedState Serialize(GameDB gameDB)
        {
            if (gameDB == null)
            {
                throw new ArgumentNullException(nameof(gameDB));
            }

            var schemaJson = gameDB.SerializeSchema();
            var dataJson = gameDB.SerializeData();
            return new GameDBSerializedState(dataJson, schemaJson, ComputeRevision(schemaJson, dataJson));
        }

        internal static string ComputeRevision(GameDB gameDB)
        {
            return Serialize(gameDB).Revision;
        }

        internal static string ComputeRevision(string schemaJson, string dataJson)
        {
            if (schemaJson == null)
            {
                throw new ArgumentNullException(nameof(schemaJson));
            }

            if (dataJson == null)
            {
                throw new ArgumentNullException(nameof(dataJson));
            }

            using (var algorithm = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(schemaJson + "\n" + dataJson);
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        internal static GameDB Import(string dataJson, string schemaJson, string loadedPath = null)
        {
            lock (TypeResolutionGate)
            {
                AssemblyExplorer.Instance.Load();
                var gameDB = new GameDB();
                gameDB.ImportOrThrow(dataJson, schemaJson);
                gameDB.LoadedPath = loadedPath;
                return gameDB;
            }
        }

        internal static GameDB CreateDetachedModel(GameDB gameDB)
        {
            var state = Serialize(gameDB);
            return Import(state.DataJson, state.SchemaJson, gameDB.LoadedPath);
        }

        internal static GameDBSnapshot CreateSnapshot(string assetPath, string schemaAssetPath, GameDB gameDB)
        {
            if (gameDB == null)
            {
                throw new ArgumentNullException(nameof(gameDB));
            }

            var snapshot = new GameDBSnapshot
            {
                DatabasePath = assetPath,
                SchemaPath = schemaAssetPath,
                Revision = ComputeRevision(gameDB),
                ScopeName = gameDB.ScopeName,
                LocalizationDatabase = gameDB.LocalizationDB
            };

            foreach (var tablePair in gameDB.Tables.OrderBy(pair => pair.Key, NameComparer))
            {
                var table = (TableModel)tablePair.Value;
                var tableSnapshot = new GameDBTableSnapshot
                {
                    Name = tablePair.Key,
                    KeyType = table.TableKeyType.KeyType,
                    KeyTypeArgument = TypeArgumentName(table.TableKeyType.TypeArg)
                };

                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, NameComparer))
                {
                    var field = fieldPair.Value;
                    var fieldSnapshot = new GameDBFieldSnapshot
                    {
                        Name = fieldPair.Key,
                        FieldType = field.Type,
                        IsArray = field.IsArray
                    };

                    if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        fieldSnapshot.DictionaryType = new GameDBDictionaryTypeDefinition
                        {
                            KeyType = dictionary.KeyType,
                            KeyTypeArgument = TypeArgumentName(dictionary.KeyTypeArg),
                            ValueType = dictionary.ValueType,
                            ValueTypeArgument = TypeArgumentName(dictionary.ValueTypeArg)
                        };
                    }
                    else
                    {
                        fieldSnapshot.TypeArgument = TypeArgumentName(field.GetTypeArg<object>());
                    }

                    tableSnapshot.Fields.Add(fieldSnapshot);
                }

                foreach (var rowPair in table.Data.OrderBy(pair => pair.Key, NameComparer))
                {
                    var values = new Dictionary<string, object>();
                    foreach (var valuePair in rowPair.Value.Data)
                    {
                        values.Add(valuePair.Key, DetachValue(valuePair.Value));
                    }

                    tableSnapshot.Rows.Add(new GameDBRowSnapshot
                    {
                        Key = rowPair.Key,
                        Values = values
                    });
                }

                snapshot.Tables.Add(tableSnapshot);
            }

            return snapshot;
        }

        internal static object DetachValue(object value)
        {
            if (value == null || value is string || value is Type || value.GetType().IsPrimitive
                || value is decimal || value is Enum)
            {
                return value;
            }

            if (value is UnityObjectReference unityObjectReference)
            {
                return unityObjectReference;
            }

            if (value is Color color)
            {
                return new Color(color.r, color.g, color.b, color.a);
            }

            if (value is Vector2 vector2)
            {
                return new Vector2(vector2.x, vector2.y);
            }

            if (value is Vector3 vector3)
            {
                return new Vector3(vector3.x, vector3.y, vector3.z);
            }

            if (value is Vector4 vector4)
            {
                return new Vector4(vector4.x, vector4.y, vector4.z, vector4.w);
            }

            if (value is IDictionary dictionary)
            {
                var copy = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    copy.Add(DetachValue(entry.Key), DetachValue(entry.Value));
                }

                return copy;
            }

            if (value is IEnumerable enumerable)
            {
                var copy = new List<object>();
                foreach (var item in enumerable)
                {
                    copy.Add(DetachValue(item));
                }

                return copy;
            }

            throw new InvalidOperationException($"Snapshot value type is not supported: {value.GetType().FullName}");
        }

        private static string TypeArgumentName(object value)
        {
            return value is Type type ? type.FullName : value?.ToString();
        }
    }
}
