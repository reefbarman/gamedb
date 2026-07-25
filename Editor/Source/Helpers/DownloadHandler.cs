using GameDBLibrary;
using System;

namespace GameDBEditorLibrary
{
    internal static class DownloadHelper
    {
        public static void DownloadGameDBRevision(int revision, string revisionPath, string baseDBPath, string schemaPath, Action<Exception, string, string> callback)
        {
            revisionPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, revisionPath);
            schemaPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, schemaPath);
            baseDBPath = GameDBLibrary.Utils.UrlCombine(Settings.Instance.DownloadServer, baseDBPath);

            var pendingDownloads = revision == 0 ? 2 : 3;
            var completed = false;
            string revisionJson = null;
            string schemaJson = null;
            string baseJson = null;

            void CompleteDownload(Exception error)
            {
                if (completed)
                {
                    return;
                }

                if (error != null)
                {
                    completed = true;
                    callback(error, null, null);
                    return;
                }

                pendingDownloads--;
                if (pendingDownloads > 0)
                {
                    return;
                }

                completed = true;

                try
                {
                    var gameDBJson = revision == 0
                        ? revisionJson
                        : new JsonPatch().Patch(baseJson, revisionJson);
                    callback(null, gameDBJson, schemaJson);
                }
                catch (Exception exception)
                {
                    callback(exception, null, null);
                }
            }

            DownloadJson(revisionPath, (error, json) =>
            {
                revisionJson = json;
                CompleteDownload(error);
            });

            DownloadJson(schemaPath, (error, json) =>
            {
                schemaJson = json;
                CompleteDownload(error);
            });

            if (revision != 0)
            {
                DownloadJson(baseDBPath, (error, json) =>
                {
                    baseJson = json;
                    CompleteDownload(error);
                });
            }
        }

        private static void DownloadJson(string url, Action<Exception, string> callback)
        {
            try
            {
                RequestHelper.StartRequest(url, RequestMethod.GET, null, (error, handler) =>
                {
                    if (error != null)
                    {
                        callback(error, null);
                        return;
                    }

                    var json = handler?.GetText();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        callback(new InvalidOperationException($"Empty response received from {url}"), null);
                        return;
                    }

                    callback(null, json);
                });
            }
            catch (Exception exception)
            {
                callback(exception, null);
            }
        }
    }
}
