#pragma warning disable CS0618 // Retained implementation for the obsolete remote API and optional Google Sheets bridge.

using GameDBLibraryUnity;
using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    [Obsolete(LegacyRemoteApi.Message)]
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

#pragma warning restore CS0618
