using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    interface IWebRequest
    {
        RequestUpdater StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback);
        RequestUpdater StartPostRequest(string url, IForm form, Action<Exception, IDownloadHandler> callback);
    }
}
