using GameDBLibraryUnity;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    internal sealed class ResourcesGameDBDataLoaderTests
    {
        private const string RealResourcePath = "GameDBTests/async-loader";
        private const string RealResourceJson = "{ \"tables\": {} }";

        [UnityTest]
        public IEnumerator LoadAsync_ProductionBackendLoadsRealTextAsset()
        {
            string json = null;
            Exception exception = null;

            yield return Await(ResourcesGameDBDataLoader.Instance.LoadAsync(
                    RealResourcePath),
                value => json = value, value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(json.Trim(), Is.EqualTo(RealResourceJson));
        }

        [UnityTest]
        public IEnumerator LoadAsync_ProductionBackendRejectsMissingTextAsset()
        {
            Exception exception = null;

            yield return Await(ResourcesGameDBDataLoader.Instance.LoadAsync(
                    "GameDBTests/missing"),
                _ => Assert.Fail("Missing Resources path returned JSON."),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<ArgumentException>());
            Assert.That(exception.Message, Does.Contain("GameDBTests/missing"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_ReturnsBackendJsonAndForwardsLocation()
        {
            var backend = FakeBackend.Completed("{\"tables\":{}}");
            var loader = ResourcesGameDBDataLoader.CreateForTests(backend);
            string json = null;
            Exception exception = null;

            yield return Await(loader.LoadAsync("GameDBs/main"),
                value => json = value, value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(json, Is.EqualTo("{\"tables\":{}}"));
            Assert.That(backend.StartCount, Is.EqualTo(1));
            Assert.That(backend.Location, Is.EqualTo("GameDBs/main"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_PreCancelledStartsNoBackend()
        {
            var backend = FakeBackend.Pending();
            var loader = ResourcesGameDBDataLoader.CreateForTests(backend);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Exception exception = null;

            yield return Await(loader.LoadAsync("GameDBs/main", cancellation.Token),
                _ => Assert.Fail("Cancelled Resources load returned JSON."),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(backend.StartCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoadAsync_BackendCancellationStopsAwaitingLateCompletion()
        {
            var backend = FakeBackend.Pending();
            var loader = ResourcesGameDBDataLoader.CreateForTests(backend);
            using var cancellation = new CancellationTokenSource();
            var awaitable = loader.LoadAsync("GameDBs/main", cancellation.Token);
            var awaiter = awaitable.GetAwaiter();

            for (var frame = 0; backend.StartCount == 0 && frame < 300; frame++)
            {
                yield return null;
            }

            Assert.That(backend.StartCount, Is.EqualTo(1),
                "Resources backend did not start within 300 frames.");
            cancellation.Cancel();
            for (var frame = 0; !awaiter.IsCompleted && frame < 300; frame++)
            {
                yield return null;
            }

            Assert.That(awaiter.IsCompleted, Is.True,
                "Resources cancellation did not complete within 300 frames.");
            Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
            Assert.That(backend.Location, Is.EqualTo("GameDBs/main"));
            Assert.That(backend.CancellationToken, Is.EqualTo(cancellation.Token));
        }

        [UnityTest]
        public IEnumerator LoadAsync_BackendFailurePropagatesToDatabaseWrappingBoundary()
        {
            var cause = new InvalidOperationException("Resources request failed");
            var backend = FakeBackend.Failed(cause);
            var loader = ResourcesGameDBDataLoader.CreateForTests(backend);
            Exception exception = null;

            yield return Await(loader.LoadAsync("GameDBs/main"),
                _ => Assert.Fail("Failed Resources load returned JSON."),
                value => exception = value);

            Assert.That(exception, Is.SameAs(cause));
        }

        private static IEnumerator Await(Awaitable<string> awaitable,
            Action<string> onSuccess, Action<Exception> onFailure)
        {
            var awaiter = awaitable.GetAwaiter();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!awaiter.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                yield return null;
            }

            Assert.That(awaiter.IsCompleted, Is.True,
                "Resources load did not complete within 10 seconds.");
            try
            {
                onSuccess(awaiter.GetResult());
            }
            catch (Exception exception)
            {
                onFailure(exception);
            }
        }

        private sealed class FakeBackend : IResourcesGameDBDataLoadBackend
        {
            private readonly AwaitableCompletionSource<string> m_completion;

            private FakeBackend(AwaitableCompletionSource<string> completion)
            {
                m_completion = completion;
            }

            internal int StartCount { get; private set; }
            internal string Location { get; private set; }
            internal CancellationToken CancellationToken { get; private set; }

            internal static FakeBackend Completed(string json)
            {
                var completion = new AwaitableCompletionSource<string>();
                completion.SetResult(json);
                return new FakeBackend(completion);
            }

            internal static FakeBackend Pending()
            {
                return new FakeBackend(new AwaitableCompletionSource<string>());
            }

            internal static FakeBackend Failed(Exception exception)
            {
                var completion = new AwaitableCompletionSource<string>();
                completion.SetException(exception);
                return new FakeBackend(completion);
            }

            public Awaitable<string> LoadAsync(string location,
                CancellationToken cancellationToken)
            {
                StartCount++;
                Location = location;
                CancellationToken = cancellationToken;
                cancellationToken.Register(() => m_completion.SetCanceled());
                return m_completion.Awaitable;
            }
        }
    }
}
