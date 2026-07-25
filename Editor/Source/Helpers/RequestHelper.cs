using GameDBLibrary;
using GameDBLibraryUnity;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    internal class RequestHelper
    {
        public static void StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback)
        {
            RequestUpdater updater = null;

            Action onUpdate = delegate
            {
                updater.Update();
            };

            updater = WebRequestHelper.StartRequest(url, method, requestParams, (error, response) =>
            {
                Updater.Instance.OnUpdate -= onUpdate;
                callback(error, response);
            });

            Updater.Instance.OnUpdate += onUpdate;
        }

        public static void StartPostRequest(string url, UnityForm form, Action<Exception, IDownloadHandler> callback)
        {
            RequestUpdater updater = null;

            Action onUpdate = delegate
            {
                updater.Update();
            };

            updater = WebRequestHelper.StartPostRequest(url, form, (error, response) =>
            {
                Updater.Instance.OnUpdate -= onUpdate;
                callback(error, response);
            });

            Updater.Instance.OnUpdate += onUpdate;
        }
    }
}
