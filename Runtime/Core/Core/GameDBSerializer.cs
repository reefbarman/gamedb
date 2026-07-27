using System;
using System.Collections.Generic;
using System.Threading;

namespace GameDBLibrary
{
    internal class GameDBSerializer
    {
        internal static void DeserializeData(Dictionary<string, TableBase> tables,
            string gameDBJSON, string[] columnImportList = null,
            Action beforePublish = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
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

            cancellationToken.ThrowIfCancellationRequested();
            beforePublish?.Invoke();

            foreach (var candidate in stagedData)
            {
                candidate.Key.PublishData(candidate.Value);
            }
        }
    }
}
