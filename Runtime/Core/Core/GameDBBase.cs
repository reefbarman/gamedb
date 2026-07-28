using System;
using System.Threading;
using UnityEngine;

namespace GameDBLibrary
{
    /// <summary>
    /// The base class all GameDBs inherit from. 
    /// The GameDB provides methods for importing data.
    /// </summary>
    public class GameDBBase
    {
        /// <summary>
        /// OnDBLoaded allows callbacks to be added that are triggered if <c>notify = true</c>
        /// when importing or loading data, including asynchronous loads or hot reloads from
        /// the GameDB Editor during run-time in editor.
        /// </summary>
        public Action OnDBLoaded = null;

        /// <summary>
        /// Gets the ScopeName of the GameDB set when creating it in the GameDB Editor.
        /// </summary>
        /// <value>
        /// The ScopeName
        /// </value>
        public string ScopeName { get; } = string.Empty;

        /// <summary>
        /// Gets the name of the GameDB set when instantiating.
        /// </summary>
        /// <value>
        /// The name of the GameDB.
        /// </value>
        public string Name => m_internal.Name;

        /// <summary>
        /// Gets or sets the logger used for internal logs.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        public Logger Logger
        {
            set => m_internal.Logger = value;
            get => m_internal.Logger;
        }

        internal GameDBInternal m_internal = null;

        protected void RegisterTable(string name, TableBase table)
        {
            m_internal.Tables.Add(name, table);
        }

        protected T GetTable<T>(string name) where T : TableBase
        {
            return (T)m_internal.Tables[name];
        }

        protected T GetCurrentPublicationMetadata<T>() where T : class
        {
            return m_internal.CurrentSnapshot?.Metadata as T;
        }

        protected GameDBBase(string dbName, string scopeName)
        {
            m_internal = new GameDBInternal(dbName)
            {
                OnDBLoaded = delegate () { OnDBLoaded?.Invoke(); }
            };

            ScopeName = scopeName;
        }

        /// <summary>
        /// Imports JSON representing the GameDB.
        /// </summary>
        /// <param name="jsonData">A string representing the JSON format of the GameDB to import.</param>
        /// <param name="notify">if set to <c>true</c> the <see cref="OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <returns>An exception is returned if the GameDB fails to imprt</returns>
        public virtual Exception Import(string jsonData, bool notify = true)
        {
            return m_internal.Import(jsonData, null, notify);
        }

        /// <summary>
        /// Imports JSON representing the GameDB.
        /// Allows specifying only certain fields to be imported. Useful for limiting the memory used of the loaded GameDB.
        /// </summary>
        /// <param name="jsonData">A string representing the JSON format of the GameDB to import.</param>
        /// <param name="columImportList">An array of field names to import.</param>
        /// <param name="notify">if set to <c>true</c> the <see cref="OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <returns>An exception is returned if the GameDB fails to imprt</returns>
        public virtual Exception Import(string jsonData, string[] columImportList, bool notify = true)
        {
            return m_internal.Import(jsonData, columImportList, notify);
        }

        internal Exception ImportEditorData(string jsonData)
        {
            return ImportEditorDataCore(jsonData);
        }

        protected virtual Exception ImportEditorDataCore(string jsonData)
        {
            return Import(jsonData);
        }

        protected Exception ImportData(string jsonData, string[] columnImportList,
            bool notify, object publicationMetadata)
        {
            return ImportDataInternal(jsonData, columnImportList, notify,
                publicationMetadata, false);
        }

        protected Exception ImportLocalizationData(string jsonData,
            string[] columnImportList, bool notify, object publicationMetadata)
        {
            return ImportDataInternal(jsonData, columnImportList, notify,
                publicationMetadata, true);
        }

        private Exception ImportDataInternal(string jsonData,
            string[] columnImportList, bool notify, object publicationMetadata,
            bool allowMissingSelectedFields)
        {
            if (!m_internal.TryBeginOperation())
            {
                return GameDBInternal.OperationInProgressException();
            }

            try
            {
                return m_internal.ImportOwned(jsonData, columnImportList, notify,
                    publicationMetadata,
                    allowMissingSelectedFields: allowMissingSelectedFields);
            }
            finally
            {
                m_internal.EndOperation();
            }
        }

        protected Exception LoadData(Func<string> loadData,
            string[] columnImportList = null, bool notify = true,
            object publicationMetadata = null)
        {
            return LoadDataInternal(loadData, columnImportList, notify,
                publicationMetadata, false);
        }

        protected Exception LoadLocalizationData(Func<string> loadData,
            string[] columnImportList, bool notify, object publicationMetadata)
        {
            return LoadDataInternal(loadData, columnImportList, notify,
                publicationMetadata, true);
        }

        private Exception LoadDataInternal(Func<string> loadData,
            string[] columnImportList, bool notify, object publicationMetadata,
            bool allowMissingSelectedFields)
        {
            if (loadData == null)
            {
                return new ArgumentNullException(nameof(loadData));
            }

            if (!m_internal.TryBeginOperation())
            {
                return GameDBInternal.OperationInProgressException();
            }

            try
            {
                string jsonData;
                try
                {
                    jsonData = loadData();
                }
                catch (Exception exception)
                {
                    return exception;
                }

                return m_internal.ImportOwned(jsonData, columnImportList, notify,
                    publicationMetadata,
                    allowMissingSelectedFields: allowMissingSelectedFields);
            }
            finally
            {
                m_internal.EndOperation();
            }
        }

        protected Awaitable LoadDataAsync(string location,
            IGameDBDataLoader loader, string[] columnImportList = null,
            bool notify = true, object publicationMetadata = null,
            CancellationToken cancellationToken = default)
        {
            return LoadDataAsyncInternal(location, loader, columnImportList,
                notify, publicationMetadata, cancellationToken, false);
        }

        protected Awaitable LoadLocalizationDataAsync(string location,
            IGameDBDataLoader loader, string[] columnImportList,
            bool notify, object publicationMetadata,
            CancellationToken cancellationToken = default)
        {
            return LoadDataAsyncInternal(location, loader, columnImportList,
                notify, publicationMetadata, cancellationToken, true);
        }

        private async Awaitable LoadDataAsyncInternal(string location,
            IGameDBDataLoader loader, string[] columnImportList,
            bool notify, object publicationMetadata,
            CancellationToken cancellationToken, bool allowMissingSelectedFields)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (!m_internal.TryBeginOperation())
            {
                throw GameDBInternal.OperationInProgressException();
            }

            try
            {
                string jsonData;
                try
                {
                    jsonData = await loader.LoadAsync(location, cancellationToken);
                    if (jsonData == null)
                    {
                        throw new InvalidOperationException(
                            "The GameDB data loader returned null JSON.");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (GameDBDataLoadException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new GameDBDataLoadException(location,
                        loader.GetType(), exception);
                }

                await Awaitable.MainThreadAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var error = m_internal.ImportOwned(jsonData, columnImportList,
                    notify, publicationMetadata, cancellationToken,
                    allowMissingSelectedFields);
                if (error != null)
                {
                    throw error;
                }
            }
            finally
            {
                m_internal.EndOperation();
            }
        }
    }
}
