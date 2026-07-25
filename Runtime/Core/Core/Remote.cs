#pragma warning disable CS0618 // Retained implementation of the obsolete remote API.

using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    /// <summary>
    /// The Remote class provides methods to communicate with GameDB servers.
    /// </summary>
    [Obsolete(LegacyRemoteApi.Message)]
    public
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
        /// <param name="callback">A callback called when the communication is complete. Returning information about the latest GameDB.</param>
        /// <returns>A <see cref="RequestUpdater"/> used to montior the request to the server.</returns>
        public static RequestUpdater GetLatestDeployment(string serverHost, string downloadHost, string scope, string tag, string checksum, string userID, Action<Exception, string, int> callback)
        {
            RequestUpdater updater = null;

            updater = WebRequestHelper.StartRequest(Utils.UrlCombine(serverHost, "/gamedb/getcurrent"), RequestMethod.POST,
                new Dictionary<string, string> {
                    { "scope", scope },
                    { "tag", tag },
                    { "checksum", checksum },
                    { "userID", userID }
                },
                (reqError, response) =>
                {

                    if (reqError == null)
                    {
                        var error = ServerResponse.HandleBasicResponse(response);
                        if (error == null)
                        {
                            var resp = JsonSerialization.Deserialize(response.GetText()) as IDictionary<string, object>;

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

                                    Action onDownloadUpdate = delegate
                                    {
                                        downloadUpdater.Update();
                                    };

                                    downloadUpdater = WebRequestHelper.StartRequest(Utils.UrlCombine(downloadHost, currRevision["path"] as string), RequestMethod.GET, null, (downloadReqError, downloadRes) =>
                                    {
                                        updater.OnUpdate -= onDownloadUpdate;

                                        if (downloadReqError == null)
                                        {
                                            var revisionJson = downloadRes?.GetText();
                                            if (!string.IsNullOrWhiteSpace(revisionJson))
                                            {
                                                callback(null, revisionJson, Convert.ToInt32(currRevision["revision"]));
                                            }
                                            else
                                            {
                                                callback(new InvalidOperationException("The downloaded GameDB revision was empty."), null, -1);
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

#pragma warning restore CS0618
