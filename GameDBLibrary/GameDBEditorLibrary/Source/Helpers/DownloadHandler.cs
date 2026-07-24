using GameDBLibrary;
using RSG;
using System;
using System.Linq;

namespace GameDBEditorLibrary
{
    internal class DownloadHelper
    {
        public static void DownloadGameDBRevision(int revision, string revisionPath, string baseDBPath, string schemaPath, Action<Exception, string, string> callback)
        {
            revisionPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, revisionPath);
            schemaPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, schemaPath);
            baseDBPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, baseDBPath);

            Promise<string>.All(
                DownloadRevision(revisionPath),
                DownloadJSON(schemaPath),
                DownloadJSON(baseDBPath)
            ).Catch(exception => {
                callback(exception, null, null);
            }).Done(downloads => {
                if (revision == 0)
                {
                    callback(null, downloads.ElementAt(0), downloads.ElementAt(1));
                }
                else
                {
                    try
                    {
                        var patcher = new JsonPatch();
                        var patchedJson = patcher.Patch(downloads.ElementAt(2), downloads.ElementAt(0));

                        callback(null, patchedJson, downloads.ElementAt(1));
                    }
                    catch (Exception e)
                    {
                        callback(e, null, null);
                    }
                }
            });
        }

        private static IPromise<string> DownloadRevision(string url)
        {
            var promise = new Promise<string>();

            RequestHelper.StartRequest(url, RequestMethod.GET, null, (error, handler) => {
                if (error == null)
                {
                    if (handler != null && handler.GetData().Length > 0)
                    {
                        try
                        {
                            var json = BinaryGameDB.Deserialize(handler.GetData(), Config.Instance.EncryptionKey, Config.Instance.EncryptionSalt);
                            promise.Resolve(json);
                        }
                        catch (Exception e)
                        {
                            promise.Reject(e);
                        }
                    }
                    else
                    {
                        promise.Reject(new Exception("Empty response received"));
                    }
                }
                else
                {
                    promise.Reject(error);
                }
            });

            return promise;
        }

        private static IPromise<string> DownloadJSON(string url)
        {
            var promise = new Promise<string>();

            RequestHelper.StartRequest(url, RequestMethod.GET, null, (error, handler) => {
                if (error == null)
                {
                    if (handler != null && !string.IsNullOrEmpty(handler.GetText()))
                    {
                        promise.Resolve(handler.GetText());
                    }
                    else
                    {
                        promise.Reject(new Exception("Empty response received"));
                    }
                }
                else
                {
                    promise.Reject(error);
                }
            });

            return promise;
        }
    }
}
