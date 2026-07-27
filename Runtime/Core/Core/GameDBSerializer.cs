using System;
using System.Collections.Generic;
using System.Threading;

namespace GameDBLibrary
{
    internal class GameDBSerializer
    {
        internal static void DeserializeData(GameDBInternal gameDB,
            string gameDBJSON, string[] columnImportList = null,
            object publicationMetadata = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
        {
            if (gameDB == null)
            {
                throw new ArgumentNullException(nameof(gameDB));
            }

            foreach (var table in gameDB.Tables.Values)
            {
                table.AttachOwner(gameDB);
            }

            var stagedData = StageData(gameDB.Tables, gameDBJSON,
                columnImportList, cancellationToken, allowMissingSelectedFields);
            ValidateReferences(gameDB.Tables, stagedData, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            gameDB.PublishSnapshot(new RuntimeGameDBSnapshot(stagedData,
                publicationMetadata));
        }

        internal static void DeserializeData(Dictionary<string, TableBase> tables,
            string gameDBJSON, string[] columnImportList = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
        {
            var stagedData = StageData(tables, gameDBJSON, columnImportList,
                cancellationToken, allowMissingSelectedFields);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var candidate in stagedData)
            {
                candidate.Key.PublishData(candidate.Value);
            }
        }

        private static Dictionary<TableBase, Dictionary<string, RowBase>> StageData(
            Dictionary<string, TableBase> tables, string gameDBJSON,
            string[] columnImportList, CancellationToken cancellationToken,
            bool allowMissingSelectedFields)
        {
            if (!(JsonSerialization.Deserialize(gameDBJSON) is IDictionary<string, object> gameDBObjDic))
            {
                throw new FormatException("top level object not a dictionary");
            }

            if (!gameDBObjDic.ContainsKey("tables"))
            {
                throw new FormatException("gamedb tables object not found");
            }

            if (!(gameDBObjDic["tables"] is IDictionary<string, object> tablesObjDic))
            {
                throw new FormatException("gamedb tables object not a dictionary");
            }

            var stagedData = new Dictionary<TableBase, Dictionary<string, RowBase>>();

            foreach (var tablePair in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!tablesObjDic.ContainsKey(tablePair.Key))
                {
                    throw new FormatException("gamedb missing table: " + tablePair.Key);
                }

                stagedData.Add(tablePair.Value,
                    tablePair.Value.StageData(tablesObjDic[tablePair.Key],
                        columnImportList, cancellationToken,
                        allowMissingSelectedFields));
            }

            return stagedData;
        }

        private static void ValidateReferences(
            Dictionary<string, TableBase> tables,
            Dictionary<TableBase, Dictionary<string, RowBase>> stagedData,
            CancellationToken cancellationToken)
        {
            foreach (var sourceTablePair in tables)
            {
                var sourceTable = sourceTablePair.Value;
                foreach (var rowPair in stagedData[sourceTable])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var fieldPair in sourceTable.Fields)
                    {
                        if (!rowPair.Value.Data.TryGetValue(fieldPair.Key,
                            out var value))
                        {
                            continue;
                        }

                        var field = fieldPair.Value;
                        if (field.Type == FieldType.tableRef)
                        {
                            if (field.IsArray)
                            {
                                foreach (var item in (IEnumerable<object>)value)
                                {
                                    ValidateReference(tables, stagedData,
                                        sourceTablePair.Key, rowPair.Key,
                                        fieldPair.Key, field.GetTypeArg<string>(), item);
                                }
                            }
                            else
                            {
                                ValidateReference(tables, stagedData,
                                    sourceTablePair.Key, rowPair.Key,
                                    fieldPair.Key, field.GetTypeArg<string>(), value);
                            }
                        }
                        else if (field.Type == FieldType.dictionary)
                        {
                            var dictionaryType = field.GetTypeArg<DictionaryType>();
                            if (dictionaryType.ValueType != FieldType.tableRef)
                            {
                                continue;
                            }

                            foreach (var item in
                                (IDictionary<object, object>)value)
                            {
                                ValidateReference(tables, stagedData,
                                    sourceTablePair.Key, rowPair.Key,
                                    fieldPair.Key,
                                    dictionaryType.ValueTypeArg as string,
                                    item.Value);
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateReference(
            Dictionary<string, TableBase> tables,
            Dictionary<TableBase, Dictionary<string, RowBase>> stagedData,
            string sourceTable, string sourceRow, string sourceField,
            string targetTable, object value)
        {
            var targetKey = value as string;
            if (string.IsNullOrEmpty(targetKey))
            {
                return;
            }

            if (string.IsNullOrEmpty(targetTable)
                || !tables.TryGetValue(targetTable, out var table)
                || !stagedData[table].ContainsKey(targetKey))
            {
                throw new FormatException(
                    $"Table reference {sourceTable}[{sourceRow}].{sourceField} " +
                    $"targets missing row {targetTable}[{targetKey}].");
            }
        }
    }
}
