using System;
using System.Collections.Generic;
using System.Threading;

namespace GameDBLibrary
{
    internal class GameDBInternal : IGameDB
    {
        public Action OnDBLoaded = null;

        protected Dictionary<string, TableBase> m_tables = new Dictionary<string, TableBase>();
        protected string m_name = string.Empty;
        protected Logger m_logger = new Logger();
        private int m_operationInProgress;

        public string Name => m_name;
        public Dictionary<string, TableBase> Tables => m_tables;

        public Logger Logger
        {
            set { m_logger = value; }
            get { return m_logger; }
        }

        public GameDBInternal(string name)
        {
            m_name = name;
        }

        public bool Load(string path)
        {
            throw new NotImplementedException("Only Import is supported for runtime dbs");
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
            bool notify = true, Action beforePublish = null,
            CancellationToken cancellationToken = default,
            bool allowMissingSelectedFields = false)
        {
            Exception error = null;

            try
            {
                GameDBSerializer.DeserializeData(m_tables, jsonData, columnImportList,
                    beforePublish, cancellationToken, allowMissingSelectedFields);
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

        internal static InvalidOperationException OperationInProgressException()
        {
            return new InvalidOperationException(
                "A GameDB load or import operation is already in progress for this database instance.");
        }
    }
}
