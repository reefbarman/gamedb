using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    internal class GameDBSerializer
    {
        internal static void DeserializeData(Dictionary<string, TableBase> tables, string gameDBJSON, string[] columnImportList = null)
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

            foreach (var tablePair in tables)
            {
                if (!tablesObjDic.ContainsKey(tablePair.Key))
                {
                    throw new FormatException("gamedb missing table: " + tablePair.Key);
                }

                tablePair.Value.DeserializeData(tablesObjDic[tablePair.Key], columnImportList);
            }
        }
    }
}
