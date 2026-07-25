using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    internal class GameDBInternal : IGameDB
    {
        public Action OnDBLoaded = null;

        protected Dictionary<string, TableBase> m_tables = new Dictionary<string, TableBase>();
        protected string m_name = string.Empty;
        protected Logger m_logger = new Logger();

        public string Name => m_name;
        public Dictionary<string, TableBase> Tables => m_tables;

        public Logger Logger
        {
            set { m_logger = value; }
            get { return m_logger; }
        }

        public GameDBInternal(string name) {
            m_name = name;
        }

        public bool Load(string path) {
            throw new NotImplementedException("Only Import is supported for runtime dbs");
        }

        public Exception Import(string jsonData, string[] columnImportList = null, bool notify = true)
        {
            Exception error = null;

            try
            {
                GameDBSerializer.DeserializeData(m_tables, jsonData, columnImportList);
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
    }
}
