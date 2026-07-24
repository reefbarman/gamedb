using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    class HTTPRequest : IWebRequest
    {
        public RequestUpdater StartRequest(string url, RequestMethod method, Dictionary<string, string> requestParams, Action<Exception, IDownloadHandler> callback)
        {
            switch (method)
            {
                case RequestMethod.GET:
                    var paramPairs = new List<string>();

                    foreach (var requestParam in requestParams)
                    {
                        paramPairs.Add($"{requestParam.Key}={requestParam.Value}");
                    }

                    var getParams = string.Join("&", paramPairs.ToArray());

                    DoRequest($"{url}?{getParams}", method, null, callback);

                    break;
                case RequestMethod.POST:
                    var form = new PostForm();

                    foreach (var requestParam in requestParams)
                    {
                        form.AddField(requestParam.Key, requestParam.Value);
                    }

                    DoRequest(url, method, form, callback);
                    break;
            }

            return new RequestUpdater();
        }

        public RequestUpdater StartPostRequest(string url, IForm form, Action<Exception, IDownloadHandler> callback)
        {
            DoRequest(url, RequestMethod.POST, form as PostForm, callback);

            return new RequestUpdater();
        }

        private void DoRequest(string url, RequestMethod method, PostForm form, Action<Exception, IDownloadHandler> callback)
        {
            try
            {
                var request = HttpWebRequest.Create(url);
                request.Method = method == RequestMethod.POST ? "POST" : "GET";

                if (form != null)
                {
                    request.ContentType = "multipart/form-data; boundary=" + PostForm.Boundary;

                    byte[] data = Encoding.UTF8.GetBytes(form.GetPostData());

                    request.ContentLength = data.Length;

                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }

                IDownloadHandler handler = null;

                request.BeginGetResponse(ar =>
                {
                    try
                    {
                        using (var response = request.EndGetResponse(ar))
                        {
                            using (var stream = response.GetResponseStream())
                            {
                                var buffer = new byte[16 * 1024];
                                using (var ms = new MemoryStream())
                                {
                                    int read;
                                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        ms.Write(buffer, 0, read);
                                    }

                                    callback(null, new HTTPDownloadHandler(ms.ToArray()));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        callback(e, null);
                    }
                }, request);
            }
            catch (Exception e)
            {
                callback(e, null);
            }
        }
    }
}
