#pragma warning disable CS0618 // Retained implementation for the obsolete remote API.

using GameDBLibrary;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    [Obsolete("The legacy GameDB remote/deployment transport is unsupported and will be removed in GameDB 1.0.0. Use generated Load/Import with local JSON, or provide your own network transport. See Documentation~/runtime.md#intentionally-unsupported-surfaces.")]
    internal sealed class UnityWebRequestTransport
    {
        public RequestUpdater StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback)
        {
            switch (method)
            {
                case RequestMethod.POST:
                    return DoRequest(UnityWebRequest.Post(url, requestParams), callback);
                case RequestMethod.GET:
                    if (requestParams != null)
                    {
                        var paramPairs = new string[requestParams.Count];

                        var i = 0;
                        foreach (var requestPair in requestParams)
                        {
                            paramPairs[i] = $"{UnityWebRequest.EscapeURL(requestPair.Key)}={UnityWebRequest.EscapeURL(requestPair.Value)}";
                            i++;
                        }

                        if (paramPairs.Length > 0)
                        {
                            url += $"{(url.Contains("?") ? "&" : "?")}{string.Join("&", paramPairs)}";
                        }
                    }

                    return DoRequest(UnityWebRequest.Get(url), callback);
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported request method.");
            }
        }

        public RequestUpdater StartPostRequest(string url, UnityForm form, Action<Exception, IDownloadHandler> callback)
        {
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            return DoRequest(UnityWebRequest.Post(url, form.Sections), callback);
        }

        private static RequestUpdater DoRequest(UnityWebRequest request, Action<Exception, IDownloadHandler> callback)
        {
            var result = request.SendWebRequest();
            var updater = new RequestUpdater();

            void OnUpdate()
            {
                if (!result.isDone)
                {
                    return;
                }

                updater.OnUpdate -= OnUpdate;

                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        callback(new InvalidOperationException($"Request failed with status {request.responseCode}: {request.error}"), null);
                    }
                    else
                    {
                        callback(null, new UnityDownloadHandler(request.downloadHandler));
                    }
                }
                finally
                {
                    request.Dispose();
                }
            }

            updater.OnUpdate += OnUpdate;
            return updater;
        }
    }
}

#pragma warning restore CS0618
