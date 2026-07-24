using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace GameDBLibrary.UnitTests
{
    public class HTTPDownloadHandlerTests
    {
        private readonly ITestOutputHelper _output;

        public HTTPDownloadHandlerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SuccessfulGetRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();
            WebRequestHelper.Request.StartRequest("http://httpbin.org/get", RequestMethod.GET, new Dictionary<string, string> {{"test", "hello"}}, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(exception);
            Assert.NotNull(handler);

            _output.WriteLine(handler.GetText());

            var response = MiniJSON.Json.Deserialize(handler.GetText()) as SortedDictionary<string, object>;
            Assert.NotNull(response);

            Assert.True(response.ContainsKey("args"));

            var args = response["args"] as SortedDictionary<string, object>;
            Assert.NotNull(args);

            Assert.True(args.ContainsKey("test"));
            Assert.True(args["test"] as string == "hello");
        }

        [Fact]
        public void FailedGetRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();
            WebRequestHelper.Request.StartRequest("http://httpbin.org/status/500", RequestMethod.GET, new Dictionary<string, string> { { "test", "hello" } }, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(handler);
            Assert.NotNull(exception);
            Assert.IsType<WebException>(exception);

            _output.WriteLine(exception.Message);

            var webException = (WebException) exception;
            var response = webException.Response as HttpWebResponse;

            Assert.True(response.StatusCode == HttpStatusCode.InternalServerError);
        }

        [Fact]
        public void SuccessfulPostRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();
            WebRequestHelper.Request.StartRequest("http://httpbin.org/post", RequestMethod.POST, new Dictionary<string, string> { { "test", "hello" } }, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(exception);
            Assert.NotNull(handler);

            _output.WriteLine(handler.GetText());

            var response = MiniJSON.Json.Deserialize(handler.GetText()) as SortedDictionary<string, object>;
            Assert.NotNull(response);

            Assert.True(response.ContainsKey("form"));

            var form = response["form"] as SortedDictionary<string, object>;
            Assert.NotNull(form);

            Assert.True(form.ContainsKey("test"));
            Assert.True(form["test"] as string == "hello");
        }

        [Fact]
        public void FailedPostRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();
            WebRequestHelper.Request.StartRequest("http://httpbin.org/status/500", RequestMethod.POST, new Dictionary<string, string> { { "test", "hello" } }, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(handler);
            Assert.NotNull(exception);
            Assert.IsType<WebException>(exception);

            _output.WriteLine(exception.Message);

            var webException = (WebException)exception;
            var response = webException.Response as HttpWebResponse;

            Assert.True(response.StatusCode == HttpStatusCode.InternalServerError);
        }

        [Fact]
        public void SuccessfulUploadFileRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();

            var form = new PostForm();
            form.AddField("test", "hello");
            form.AddBinaryData("testFile", Encoding.UTF8.GetBytes("{\"test\":\"goodbye\"}"), "testFile.txt");

            WebRequestHelper.Request.StartPostRequest("http://httpbin.org/post", form, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(exception);
            Assert.NotNull(handler);

            _output.WriteLine(handler.GetText());

            var response = MiniJSON.Json.Deserialize(handler.GetText()) as SortedDictionary<string, object>;
            Assert.NotNull(response);

            Assert.True(response.ContainsKey("form"));

            var retForm = response["form"] as SortedDictionary<string, object>;
            Assert.NotNull(form);

            Assert.True(retForm.ContainsKey("test"));
            Assert.True(retForm["test"] as string == "hello");

            Assert.True(response.ContainsKey("files"));

            var files = response["files"] as SortedDictionary<string, object>;
            Assert.NotNull(files);

            Assert.True(files.ContainsKey("testFile"));
            Assert.True(files["testFile"] as string == "{\"test\":\"goodbye\"}");
        }

        [Fact]
        public void FailedUploadFileRequest()
        {
            var requestFinished = false;

            Exception exception = null;
            IDownloadHandler handler = null;

            WebRequestHelper.Request = new HTTPRequest();

            var form = new PostForm();
            form.AddField("test", "hello");
            form.AddBinaryData("testFile", Encoding.UTF8.GetBytes("{\"test\":\"goodbye\"}"), "testFile.txt");

            WebRequestHelper.Request.StartPostRequest("http://httpbin.org/status/500", form, (e, h) =>
            {
                exception = e;
                handler = h;

                requestFinished = true;
            });

            while (!requestFinished)
            {
                Thread.Sleep(1);
            }

            Assert.Null(handler);
            Assert.NotNull(exception);
            Assert.IsType<WebException>(exception);

            _output.WriteLine(exception.Message);

            var webException = (WebException)exception;
            var response = webException.Response as HttpWebResponse;

            Assert.True(response.StatusCode == HttpStatusCode.InternalServerError);
        }
    }
}
