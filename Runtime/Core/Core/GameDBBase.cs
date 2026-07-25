using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        /// when importing or loading data. This allows code to deal with data potentially
        /// beind asynchronously loaded (when imported from a server) or hot reloaded via
        /// an Import or Reload from the GameDB Editor during run-time in editor.
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

        protected Dictionary<string, TableBase> Tables => m_internal.Tables;

        internal GameDBInternal m_internal = null;

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
        public Exception Import(string jsonData, bool notify = true)
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
        public Exception Import(string jsonData, string[] columImportList, bool notify = true)
        {
            return m_internal.Import(jsonData, columImportList, notify);
        }

        /// <summary>
        /// Imports the GameDB from a server.
        /// This allows over the air updating of GameDBs so new data can be pushed to clients without the need of releasing
        /// new versions of the clients.
        /// Downloaded GameDBs will be cached locally to avoid downloading again if they haven't changed.
        /// If they have change, only the diff between the original built-in GameDB and the uploaded GameDB will be downloaded.
        /// </summary>
        /// <param name="serverHost">The server host base address of the GameDB server to communicate with. For example: https://mygamedbserver.com</param>
        /// <param name="downloadHost">The download host base address of the GameDB to download. For example https://s3.aws.com/mygamedb</param>
        /// <param name="userID">A unique identified used to represent the client downloading the GameDB. Can be used for A/B testing GameDB versions</param>
        /// <param name="tag">The tag used to download the correct version of the GameDB</param>
        /// <param name="originalJSON">The original json the update is based off. This will be imported if there are no cached version or anything to download from the server</param>
        /// <param name="cachePath">The path to cache downloaded GameDBs too</param>
        /// <param name="columImportList">An array of field names to import.</param>
        /// <param name="onImport">A callback called when the data has be imported.</param>
        /// <returns>A <see cref="RequestUpdater"/> used to montior the request to the server.</returns>
        public RequestUpdater ImportFromServer(string serverHost, string downloadHost, string userID, string tag, string originalJSON, string cachePath, string[] columImportList = null, Action<Exception> onImport = null)
        {
            var baseGameDBJson = originalJSON;
            var gameDBJson = string.Empty;

            cachePath = Path.Combine(cachePath, $"gamedb/{tag}/{ScopeName}.json");

            if (File.Exists(cachePath))
            {
                try
                {
                    gameDBJson = File.ReadAllText(cachePath);
                    var error = m_internal.Import(gameDBJson, null, false);

                    if (error != null)
                    {
                        gameDBJson = string.Empty;
                        File.Delete(cachePath);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                }
            }

            if (string.IsNullOrEmpty(gameDBJson))
            {
                gameDBJson = originalJSON;
            }

            var checksum = Utils.GetChecksum(Encoding.UTF8.GetBytes(gameDBJson));

            return Remote.GetLatestDeployment(serverHost, downloadHost, ScopeName, tag, checksum, userID, (error, json, revision) =>
            {
                if (error == null)
                {
                    if (json != null)
                    {
                        if (revision == 0)
                        {
                            gameDBJson = json;
                        }
                        else
                        {
                            var patcher = new JsonPatch();
                            gameDBJson = patcher.Patch(baseGameDBJson, json);
                        }

                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                            File.WriteAllText(cachePath, gameDBJson);
                        }
                        catch (Exception e)
                        {
                            Logger.LogException(e);
                        }
                    }

                    onImport?.Invoke(Import(gameDBJson, columImportList));
                }
                else
                {
                    Logger.Log(error.Message);
                    onImport?.Invoke(Import(baseGameDBJson, columImportList));
                }
            });
        }
    }
}
