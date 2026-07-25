using GameDBLibraryUnity;
using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    public static class WebRequestHelper
    {
        private static readonly UnityWebRequestTransport Transport = new UnityWebRequestTransport();

        public static RequestUpdater StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback)
        {
            return Transport.StartRequest(url, method, requestParams, callback);
        }

        public static RequestUpdater StartPostRequest(string url, UnityForm form, Action<Exception, IDownloadHandler> callback)
        {
            return Transport.StartPostRequest(url, form, callback);
        }

        public static UnityForm CreateForm()
        {
            return new UnityForm();
        }
    }
}
