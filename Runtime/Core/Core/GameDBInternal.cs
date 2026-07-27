using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace GameDBLibrary
{
    internal sealed class RuntimeGameDBSnapshot
    {
        private readonly Dictionary<TableBase, Dictionary<string, RowBase>> m_tables;
        private readonly Dictionary<TableBase, Dictionary<Type, object>> m_rowProjections =
            new Dictionary<TableBase, Dictionary<Type, object>>();
        private readonly object m_projectionLock = new object();

        internal object Metadata { get; }

        internal RuntimeGameDBSnapshot(
            IDictionary<TableBase, Dictionary<string, RowBase>> tables,
            object metadata)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            m_tables = new Dictionary<TableBase, Dictionary<string, RowBase>>(tables);
            Metadata = metadata;

            foreach (var table in m_tables.Values)
            {
                foreach (var row in table.Values)
                {
                    row.BindPublication(this);
                }
            }
        }

        internal RowBase ResolveRow(string tableName, string key)
        {
            foreach (var table in m_tables)
            {
                if (string.Equals(table.Key.Name, tableName,
                    StringComparison.Ordinal))
                {
                    return table.Value[key];
                }
            }

            throw new KeyNotFoundException(
                $"GameDB publication does not contain table '{tableName}'.");
        }

        internal IReadOnlyDictionary<TKey, TRow> GetRows<TKey, TRow>(
            TableBase table, Func<string, TKey> keySelector)
            where TRow : RowBase
        {
            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            lock (m_projectionLock)
            {
                if (!m_rowProjections.TryGetValue(table, out var projections))
                {
                    projections = new Dictionary<Type, object>();
                    m_rowProjections.Add(table, projections);
                }

                var projectionType = typeof(IReadOnlyDictionary<TKey, TRow>);
                if (projections.TryGetValue(projectionType, out var existing))
                {
                    return (IReadOnlyDictionary<TKey, TRow>)existing;
                }

                var projected = new Dictionary<TKey, TRow>();
                foreach (var row in GetRows(table))
                {
                    projected.Add(keySelector(row.Key), (TRow)row.Value);
                }

                var result = new ReadOnlyDictionary<TKey, TRow>(projected);
                projections.Add(projectionType, result);
                return result;
            }
        }

        internal Dictionary<string, RowBase> GetRows(TableBase table)
        {
            if (!m_tables.TryGetValue(table, out var rows))
            {
                throw new InvalidOperationException(
                    $"Table '{table?.Name}' is not part of this GameDB publication.");
            }

            return rows;
        }
    }

    internal class GameDBInternal
    {
        public Action OnDBLoaded = null;

        protected Dictionary<string, TableBase> m_tables = new Dictionary<string, TableBase>();
        protected string m_name = string.Empty;
        protected Logger m_logger = new Logger();
        private int m_operationInProgress;
        private RuntimeGameDBSnapshot m_snapshot;

        public string Name => m_name;
        internal Dictionary<string, TableBase> Tables => m_tables;
        internal RuntimeGameDBSnapshot CurrentSnapshot => Volatile.Read(ref m_snapshot);

        public Logger Logger
        {
            set { m_logger = value; }
            get { return m_logger; }
        }

        public GameDBInternal(string name)
        {
            m_name = name;
        }


        public Exception Import(string jsonData, string[] columnImportList = null,
            bool notify = true)
        {
            if (!TryBeginOperation())
            {
                return OperationInProgressException();
            }

            try
            {
                return ImportOwned(jsonData, columnImportList, notify);
            }
            finally
            {
                EndOperation();
            }
        }

        internal bool TryBeginOperation()
        {
            return Interlocked.CompareExchange(ref m_operationInProgress, 1, 0) == 0;
        }

        internal void EndOperation()
        {
            Volatile.Write(ref m_operationInProgress, 0);
        }

        internal Exception ImportOwned(string jsonData, string[] columnImportList = null,
            bool notify = true, object publicationMetadata = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
        {
            Exception error = null;

            try
            {
                GameDBSerializer.DeserializeData(this, jsonData, columnImportList,
                    publicationMetadata, cancellationToken, allowMissingSelectedFields);
            }
            catch (OperationCanceledException e)
            {
                error = e;
            }
            catch (Exception e)
            {
                error = e;

                m_logger.LogError("failed to import gameDB");
                m_logger.LogError(jsonData);
                m_logger.LogException(error);
            }

            if (error == null && notify)
            {
                OnDBLoaded?.Invoke();
            }

            return error;
        }

        internal RuntimeGameDBSnapshot CaptureSnapshot()
        {
            return CurrentSnapshot;
        }

        internal void PublishSnapshot(RuntimeGameDBSnapshot snapshot)
        {
            Volatile.Write(ref m_snapshot,
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
        }

        internal static InvalidOperationException OperationInProgressException()
        {
            return new InvalidOperationException(
                "A GameDB load or import operation is already in progress for this database instance.");
        }
    }
}
