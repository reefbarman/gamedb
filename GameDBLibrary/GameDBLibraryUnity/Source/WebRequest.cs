using GameDBLibrary;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    public class WebRequest : IWebRequest
    {
        public RequestUpdater StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback)
        {
            UnityWebRequest request = null;

            switch (method)
            {
                case RequestMethod.POST:
                    request = UnityWebRequest.Post(url, requestParams);
                    break;
                case RequestMethod.GET:
                    if (requestParams != null)
                    {
                        var paramPairs = new string[requestParams.Count];

                        var i = 0;
                        foreach (var requestPair in requestParams)
                        {
                            paramPairs[i] = $"{WWW.EscapeURL(requestPair.Key)}={WWW.EscapeURL(requestPair.Value)}";
                            i++;
                        }

                        if (paramPairs.Length > 0)
                        {
                            url += $"?{string.Join("&", paramPairs)}";
                        }
                    }

                    request = UnityWebRequest.Get(url);
                    break;
            }

            return DoRequest(request, callback);
        }

        public RequestUpdater StartPostRequest(string url, IForm form, Action<Exception, IDownloadHandler> callback)
        {
            var request = UnityWebRequest.Post(url, ((UnityForm)form).Form);

            return DoRequest(request, callback);
        }

        private RequestUpdater DoRequest(UnityWebRequest request, Action<Exception, IDownloadHandler> callback)
        {
            var result = request.Send();

            var updater = new RequestUpdater();

            void OnUpdate()
            {
                if (result.isDone)
                {
                    updater.OnUpdate -= OnUpdate;

                    if (request.isError)
                    {
                        callback(new Exception($"Error in request - statusCode: {request.responseCode} error: {request.error}"), null);
                    }
                    else
                    {
                        callback(null, new UnityDownloadHandler(request.downloadHandler));
                    }
                }
            }

            updater.OnUpdate += OnUpdate;

            return updater;
        }
    }
}
