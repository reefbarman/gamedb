using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibraryAddressables.Tests
{
    internal sealed class AddressablesGameDBDataLoaderTests
    {
        [UnityTest]
        public IEnumerator SuccessCopiesJsonAndReleasesBeforeReturning()
        {
            var asset = new TextAsset("{\"tables\":{}}");
            var operation = FakeOperation<TextAsset>.Completed(asset);
            var backend = new FakeBackend(operation);
            var loader = new AddressablesGameDBDataLoader(backend);
            string json = null;
            Exception exception = null;

            yield return Await(loader.LoadAsync("database-key"),
                value => json = value, value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(json, Is.EqualTo("{\"tables\":{}}"));
            Assert.That(backend.StartCount, Is.EqualTo(1));
            Assert.That(backend.Key, Is.EqualTo("database-key"));
            Assert.That(backend.RequestedType, Is.EqualTo(typeof(TextAsset)));
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator DelayedSuccessCopiesJsonAndReleasesOnce()
        {
            var asset = new TextAsset("delayed-json");
            var operation = new FakeOperation<TextAsset>();
            var backend = new FakeBackend(operation);
            var loader = new AddressablesGameDBDataLoader(backend);
            var awaitable = loader.LoadAsync("database-key");
            var awaiter = awaitable.GetAwaiter();

            yield return WaitUntil(() => backend.StartCount > 0,
                "Delayed Addressables load did not start.");

            operation.Complete(asset);
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Delayed Addressables load did not complete.");

            Assert.That(awaiter.GetResult(), Is.EqualTo("delayed-json"));
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator PreCancelledStartsNoOperation()
        {
            var backend = new FakeBackend();
            var loader = new AddressablesGameDBDataLoader(backend);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Exception exception = null;

            yield return Await(loader.LoadAsync("database-key", cancellation.Token),
                _ => Assert.Fail("Cancelled load returned JSON."),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(backend.StartCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InFlightCancellationReleasesOwnedOperationOnce()
        {
            var operation = new FakeOperation<TextAsset>();
            var backend = new FakeBackend(operation);
            var loader = new AddressablesGameDBDataLoader(backend);
            using var cancellation = new CancellationTokenSource();
            var awaitable = loader.LoadAsync("database-key", cancellation.Token);
            var awaiter = awaitable.GetAwaiter();

            yield return WaitUntil(() => backend.StartCount > 0,
                "Cancellable Addressables load did not start.");

            cancellation.Cancel();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Cancelled Addressables load did not complete.");

            Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompletionRacingCancellationReleasesExactlyOnce()
        {
            var asset = new TextAsset("racing-json");
            var operation = new FakeOperation<TextAsset>();
            var backend = new FakeBackend(operation);
            var loader = new AddressablesGameDBDataLoader(backend);
            using var cancellation = new CancellationTokenSource();
            var awaitable = loader.LoadAsync("database-key", cancellation.Token);
            var awaiter = awaitable.GetAwaiter();

            yield return WaitUntil(() => backend.StartCount > 0,
                "Racing Addressables load did not start.");

            operation.Complete(asset);
            cancellation.Cancel();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Racing Addressables load did not complete.");

            try
            {
                Assert.That(awaiter.GetResult(), Is.EqualTo("racing-json"));
            }
            catch (OperationCanceledException)
            {
            }

            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            Assert.That(operation.IsValid, Is.False);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator StartFailureAndInitialInvalidOperationOwnNothing()
        {
            var cause = new InvalidOperationException("start failed");
            var throwingBackend = new FakeBackend { StartException = cause };
            var invalidOperation = new FakeOperation<TextAsset> { IsValid = false };
            var invalidBackend = new FakeBackend(invalidOperation);
            Exception startException = null;
            Exception invalidException = null;

            yield return Await(new AddressablesGameDBDataLoader(throwingBackend)
                    .LoadAsync("database-key"),
                _ => Assert.Fail("Failed start returned JSON."),
                value => startException = value);
            yield return Await(new AddressablesGameDBDataLoader(invalidBackend)
                    .LoadAsync("database-key"),
                _ => Assert.Fail("Invalid operation returned JSON."),
                value => invalidException = value);

            Assert.That(startException, Is.SameAs(cause));
            Assert.That(invalidException, Is.TypeOf<InvalidOperationException>());
            Assert.That(invalidOperation.ReleaseCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NullOperationOwnsNothing()
        {
            var backend = new FakeBackend();
            Exception exception = null;

            yield return Await(new AddressablesGameDBDataLoader(backend)
                    .LoadAsync("database-key"),
                _ => Assert.Fail("Null operation returned JSON."),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(backend.StartCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedAndNullResultOperationsReleaseOnce()
        {
            var cause = new InvalidOperationException("catalog miss");
            var failed = FakeOperation<TextAsset>.Failed(cause);
            var nullResult = FakeOperation<TextAsset>.Completed(null);
            Exception failedException = null;
            Exception nullException = null;

            yield return Await(new AddressablesGameDBDataLoader(new FakeBackend(failed))
                    .LoadAsync("failed-key"),
                _ => Assert.Fail("Failed operation returned JSON."),
                value => failedException = value);
            yield return Await(new AddressablesGameDBDataLoader(new FakeBackend(nullResult))
                    .LoadAsync("null-key"),
                _ => Assert.Fail("Null result returned JSON."),
                value => nullException = value);

            Assert.That(failedException, Is.TypeOf<InvalidOperationException>());
            Assert.That(failedException.InnerException, Is.SameAs(cause));
            Assert.That(nullException, Is.TypeOf<InvalidOperationException>());
            Assert.That(failed.ReleaseCount, Is.EqualTo(1));
            Assert.That(nullResult.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RepeatedLoadsReleaseIndependentOperations()
        {
            var firstAsset = new TextAsset("first");
            var secondAsset = new TextAsset("second");
            var first = FakeOperation<TextAsset>.Completed(firstAsset);
            var second = FakeOperation<TextAsset>.Completed(secondAsset);
            var loader = new AddressablesGameDBDataLoader(new FakeBackend(first, second));
            string firstJson = null;
            string secondJson = null;

            yield return Await(loader.LoadAsync("first-key"),
                value => firstJson = value, value => Assert.Fail(value.ToString()));
            yield return Await(loader.LoadAsync("second-key"),
                value => secondJson = value, value => Assert.Fail(value.ToString()));

            Assert.That(firstJson, Is.EqualTo("first"));
            Assert.That(secondJson, Is.EqualTo("second"));
            Assert.That(first.ReleaseCount, Is.EqualTo(1));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(firstAsset);
            UnityEngine.Object.DestroyImmediate(secondAsset);
        }

        private static IEnumerator Await<T>(Awaitable<T> awaitable,
            Action<T> onSuccess, Action<Exception> onFailure)
        {
            var awaiter = awaitable.GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Addressables database load did not complete.");

            try
            {
                onSuccess(awaiter.GetResult());
            }
            catch (Exception exception)
            {
                onFailure(exception);
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string failureMessage)
        {
            for (var frame = 0; !condition() && frame < 300; frame++)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, failureMessage + " Timed out after 300 frames.");
        }

        private sealed class FakeBackend : IAddressableLoadBackend
        {
            private readonly Queue<object> m_operations = new Queue<object>();

            internal FakeBackend(params object[] operations)
            {
                foreach (var operation in operations)
                {
                    m_operations.Enqueue(operation);
                }
            }

            internal Exception StartException { get; set; }
            internal int StartCount { get; private set; }
            internal string Key { get; private set; }
            internal Type RequestedType { get; private set; }

            public IAddressableLoadOperation<T> Start<T>(string key)
                where T : UnityEngine.Object
            {
                StartCount++;
                Key = key;
                RequestedType = typeof(T);
                if (StartException != null)
                {
                    throw StartException;
                }

                return m_operations.Count == 0
                    ? null
                    : (IAddressableLoadOperation<T>)m_operations.Dequeue();
            }
        }

        private sealed class FakeOperation<T> : IAddressableLoadOperation<T>
            where T : UnityEngine.Object
        {
            public bool IsValid { get; set; } = true;
            public bool IsDone { get; private set; }
            public bool Succeeded { get; private set; }
            public T Result { get; private set; }
            public Exception OperationException { get; private set; }
            internal int ReleaseCount { get; private set; }

            internal static FakeOperation<T> Completed(T result)
            {
                var operation = new FakeOperation<T>();
                operation.Complete(result);
                return operation;
            }

            internal static FakeOperation<T> Failed(Exception exception)
            {
                return new FakeOperation<T>
                {
                    IsDone = true,
                    Succeeded = false,
                    OperationException = exception
                };
            }

            internal void Complete(T result)
            {
                IsDone = true;
                Succeeded = true;
                Result = result;
            }

            public void Release()
            {
                ReleaseCount++;
                IsValid = false;
            }
        }
    }
}
