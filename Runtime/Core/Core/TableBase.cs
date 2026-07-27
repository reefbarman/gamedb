using System;
using System.Collections.Generic;
using System.Threading;

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
        private GameDBInternal m_owner;

        protected TableKey m_tableKey;

        private Dictionary<string, FieldBase> m_fields = new Dictionary<string, FieldBase>();
        private Dictionary<string, RowBase> m_data = new Dictionary<string, RowBase>();

        public string Name => m_name;
        internal Dictionary<string, FieldBase> Fields => m_fields;
        internal Dictionary<string, FieldBase> MutableFields
        {
            get => m_fields;
            set => m_fields = value ?? throw new ArgumentNullException(nameof(value));
        }
        internal Dictionary<string, RowBase> Data =>
            m_owner?.CurrentSnapshot?.GetRows(this) ?? m_data;
        internal Dictionary<string, RowBase> MutableData
        {
            get => m_data;
            set => m_data = value ?? throw new ArgumentNullException(nameof(value));
        }
        protected IReadOnlyDictionary<string, RowBase> CurrentRows => Data;
        internal TableKey TableKeyType => m_tableKey;

        protected internal TableBase(string name, KeyType type, object typeArg,
            Func<string, RowBase> rowFactory)
        {
            m_name = name;

            m_tableKey = new TableKey(type, typeArg);
            m_rowFactory = rowFactory;
        }

        protected internal RowBase GetByKeyRaw(string key)
        {
            return Data[key];
        }

        protected void InitializeFields(Dictionary<string, FieldBase> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            if (m_fields.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Fields for table '{Name}' are already initialized.");
            }

            m_fields = fields;
        }

        protected IReadOnlyDictionary<TKey, TRow> GetRows<TKey, TRow>(
            Func<string, TKey> keySelector) where TRow : RowBase
        {
            var snapshot = m_owner?.CurrentSnapshot;
            if (snapshot == null)
            {
                var projected = new Dictionary<TKey, TRow>();
                foreach (var row in m_data)
                {
                    projected.Add(keySelector(row.Key), (TRow)row.Value);
                }

                return new System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TRow>(
                    projected);
            }

            return snapshot.GetRows<TKey, TRow>(this, keySelector);
        }

        internal void AttachOwner(GameDBInternal owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (m_owner != null && !ReferenceEquals(m_owner, owner))
            {
                throw new InvalidOperationException(
                    $"Table '{Name}' is already registered with another GameDB.");
            }

            m_owner = owner;
        }

        internal Dictionary<string, RowBase> StageData(object tableObj,
            string[] columnImportList = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
        {
            var data = new Dictionary<string, RowBase>();

            if (!(tableObj is IDictionary<string, object> tableDic))
            {
                throw new FormatException("top level table object not a dictionary");
            }

            foreach (var rowPair in tableDic)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RowBase row = m_rowFactory(rowPair.Key);
                row.DeserializeRow(m_fields, rowPair.Value, columnImportList,
                    allowMissingSelectedFields);

                data.Add(rowPair.Key, row);
            }

            return data;
        }

        internal void PublishData(Dictionary<string, RowBase> data)
        {
            m_data = data ?? throw new ArgumentNullException(nameof(data));
        }

        internal void DeserializeData(object tableObj, string[] columnImportList = null)
        {
            PublishData(StageData(tableObj, columnImportList));
        }

        internal void Import(TableBase table, RuntimeGameDBSnapshot snapshot = null)
        {
            var data = new Dictionary<string, RowBase>();
            var sourceRows = snapshot == null ? table.Data : snapshot.GetRows(table);

            foreach (var rowPair in sourceRows)
            {
                RowBase row = m_rowFactory(rowPair.Key);
                row.Import(m_fields, rowPair.Value);

                data.Add(rowPair.Key, row);
            }

            m_data = data;
        }
    }
}
