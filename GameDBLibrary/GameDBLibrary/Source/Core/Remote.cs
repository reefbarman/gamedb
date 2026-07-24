using GameDBLibrary.MiniJSON;
using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    /// <summary>
    /// The Remote class provides methods to communicate with GameDB servers.
    /// </summary>
#if FREE_VERSION
    internal
#else
    public
#endif
    class Remote
    {
        //TODO improve documentation
        /// <summary>
        /// Gets the latest deployed GameDB for a particular tag.
        /// </summary>
        /// <param name="serverHost">The server host base address of the GameDB server to communicate with. For example: https://mygamedbserver.com</param>
        /// <param name="downloadHost">The download host base address of the GameDB to download. For example https://s3.aws.com/mygamedb</param>
        /// <param name="scope">The scope name of the GameDB to check.</param>
        /// <param name="tag">The tag used to download the correct version of the GameDB</param>
        /// <param name="checksum">The checksum of the currently on disk GameDB.</param>
        /// <param name="userID">A unique identified used to represent the client downloading the GameDB. Can be used for A/B testing GameDB versions</param>
        /// <param name="key">the key used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <param name="salt">the salt used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <param name="callback">A callback called when the communication is complete. Returning information about the latest GameDB.</param>
        /// <returns>A <see cref="RequestUpdater"/> used to montior the request to the server.</returns>
        public static RequestUpdater GetLatestDeployment(string serverHost, string downloadHost, string scope, string tag, string checksum, string userID, string key, string salt, Action<Exception, string, int> callback)
        {
            RequestUpdater updater = null;

            updater = WebRequestHelper.Request.StartRequest(Utils.UrlCombine(serverHost, "/gamedb/getcurrent"), RequestMethod.POST, 
                new Dictionary<string, string> {
                    { "scope", scope },
                    { "tag", tag },
                    { "checksum", checksum },
                    { "userID", userID }
                }, 
                (reqError, response) => {

                    if (reqError == null)
                    {
                        var error = ServerResponse.HandleBasicResponse(response);
                        if (error == null)
                        {
                            var resp = Json.Deserialize(response.GetText()) as IDictionary<string, object>;

                            bool downloadNeeded = true;

                            try
                            {
                                downloadNeeded = Convert.ToBoolean(resp["success"]);
                            }
                            catch (Exception) { }

                            if (downloadNeeded)
                            {
                                if (resp["success"] is IDictionary<string, object> currRevision && currRevision.ContainsKey("path") && currRevision.ContainsKey("revision"))
                                {
                                    RequestUpdater downloadUpdater = null;

                                    Action onDownloadUpdate = delegate {
                                        downloadUpdater.Update();
                                    };

                                    downloadUpdater = WebRequestHelper.Request.StartRequest(Utils.UrlCombine(downloadHost, currRevision["path"] as string), RequestMethod.GET, null, (downloadReqError, downloadRes) => {
                                        updater.OnUpdate -= onDownloadUpdate;

                                        if (downloadReqError == null)
                                        {
                                            if (downloadRes?.GetData() != null && downloadRes.GetData().Length > 0)
                                            {
                                                try
                                                {
                                                    var revisionJson = BinaryGameDB.Deserialize(downloadRes.GetData(), key, salt);

                                                    if (revisionJson != null)
                                                    {
                                                        callback(null, revisionJson, Convert.ToInt32(currRevision["revision"]));
                                                    }
                                                    else
                                                    {
                                                        callback(new Exception("unable to decrypt downloaded gamedb"), null, -1);
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    callback(e, null, -1);
                                                }
                                            }
                                            else
                                            {
                                                callback(new Exception("unable to download revision"), null, -1);
                                            }
                                        }
                                        else
                                        {
                                            callback(downloadReqError, null, -1);
                                        }
                                    });

                                    updater.OnUpdate += onDownloadUpdate;
                                }
                                else
                                {
                                    callback(new Exception("invalid response received"), null, -1);
                                }
                            }
                            else
                            {
                                callback(null, null, -1);
                            }
                        }
                        else
                        {
                            callback(new Exception(error), null, -1);
                        }
                    }
                    else
                    {
                        callback(reqError, null, -1);
                    }
                }
            );

            return updater;
        }
    }
}
