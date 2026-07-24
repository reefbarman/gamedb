using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    public class TableBase
    {
        public struct TableKey
        {
            public KeyType KeyType;
            public object TypeArg;

            public TableKey(KeyType type, object typeArg)
            {
                KeyType = type;
                TypeArg = typeArg;
            }
        }

        protected string m_name = string.Empty;
        private Func<string, RowBase> m_rowFactory = null;

        protected TableKey m_tableKey;

        protected Dictionary<string, FieldBase> m_fields = new Dictionary<string, FieldBase>();
        protected Dictionary<string, RowBase> m_data = new Dictionary<string, RowBase>();

        public string Name => m_name;
        internal Dictionary<string, FieldBase> Fields => m_fields;
        internal Dictionary<string, RowBase> Data => m_data;
        internal TableKey TableKeyType => m_tableKey;

        public TableBase(string name, KeyType type, object typeArg, Func<string, RowBase> rowFactory)
        {
            m_name = name;

            m_tableKey = new TableKey(type, typeArg);
            m_rowFactory = rowFactory;
        }

        public RowBase GetByKeyRaw(string key)
        {
            return Data[key];
        }

        internal void DeserializeData(object tableObj, string[] columnImportList = null)
        {
            var data = new Dictionary<string, RowBase>();

            if (!(tableObj is IDictionary<string, object> tableDic))
            {
                throw new FormatException("top level table object not a dictionary");
            }

            foreach (var rowPair in tableDic) {
                RowBase row = m_rowFactory(rowPair.Key);
                row.DeserializeRow(m_fields, rowPair.Value, columnImportList);

                data.Add(rowPair.Key, row);
            }

            m_data = data;
        }

        internal void Import(TableBase table) {
            var data = new Dictionary<string, RowBase>();

            foreach (var rowPair in table.Data) {
                RowBase row = m_rowFactory(rowPair.Key);
                row.Import(m_fields, rowPair.Value);

                data.Add(rowPair.Key, row);
            }

            m_data = data;
        }
    }
}
