using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibraryAddressables.Tests
{
    internal sealed class UnityObjectReferenceAddressablesTests
    {
        private const string AssetGuid = "0123456789abcdef0123456789abcdef";
        private const string AssetPath = "Assets/Game/Items/Sword.asset";

        [UnityTest]
        public IEnumerator SuccessfulLoadTransfersOwnershipToAnIdempotentLease()
        {
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            var operation = FakeOperation<TestAsset>.Completed(asset);
            var backend = new FakeBackend(operation);
            AddressableAssetLease<TestAsset> lease = null;
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                value => lease = value,
                value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(backend.StartCount, Is.EqualTo(1));
            Assert.That(backend.Key, Is.EqualTo(AssetGuid));
            Assert.That(backend.RequestedType, Is.EqualTo(typeof(TestAsset)));
            Assert.That(lease.Asset, Is.SameAs(asset));
            Assert.That(operation.ReleaseCount, Is.Zero);

            lease.Dispose();
            Assert.That(lease.IsDisposed, Is.True);
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => _ = lease.Asset);

            lease.Dispose();
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator EmptyReferenceCreatesNoOperationAndReturnsDisposableNullLease()
        {
            var backend = new FakeBackend();
            AddressableAssetLease<TestAsset> lease = null;
            Exception exception = null;

            yield return Await(
                UnityObjectReference.Empty.LoadAddressableAsync<TestAsset>(default, backend),
                value => lease = value,
                value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(backend.StartCount, Is.Zero);
            Assert.That(lease.Asset, Is.Null);

            lease.Dispose();
            Assert.That(lease.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => _ = lease.Asset);
        }

        [UnityTest]
        public IEnumerator AlreadyCancelledTokenCreatesNoOperation()
        {
            var backend = new FakeBackend();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(cancellation.Token, backend),
                _ => Assert.Fail("A cancelled load must not return a lease."),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(backend.StartCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancellationWhilePollingReleasesOwnedOperationOnce()
        {
            var operation = new FakeOperation<TestAsset>();
            var backend = new FakeBackend(operation);
            using var cancellation = new CancellationTokenSource();
            var awaitable = Reference().LoadAddressableAsync<TestAsset>(
                cancellation.Token, backend);
            var awaiter = awaitable.GetAwaiter();

            while (backend.StartCount == 0)
            {
                yield return null;
            }

            cancellation.Cancel();
            while (!awaiter.IsCompleted)
            {
                yield return null;
            }

            Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            Assert.That(operation.IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator CompletionRacingCancellationHasExactlyOneTerminalOwner()
        {
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            var operation = new FakeOperation<TestAsset>();
            var backend = new FakeBackend(operation);
            using var cancellation = new CancellationTokenSource();
            var awaitable = Reference().LoadAddressableAsync<TestAsset>(
                cancellation.Token, backend);
            var awaiter = awaitable.GetAwaiter();

            while (backend.StartCount == 0)
            {
                yield return null;
            }

            operation.Complete(asset);
            cancellation.Cancel();
            while (!awaiter.IsCompleted)
            {
                yield return null;
            }

            AddressableAssetLease<TestAsset> lease = null;
            var cancelled = false;
            try
            {
                lease = awaiter.GetResult();
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled)
            {
                Assert.That(lease, Is.Null);
                Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(lease.Asset, Is.SameAs(asset));
                Assert.That(operation.ReleaseCount, Is.Zero);
                lease.Dispose();
                Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            }

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator DelayedSuccessTransfersOwnershipAfterPolling()
        {
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            var operation = new FakeOperation<TestAsset>();
            var backend = new FakeBackend(operation);
            var awaitable = Reference().LoadAddressableAsync<TestAsset>(default, backend);
            var awaiter = awaitable.GetAwaiter();

            while (backend.StartCount == 0)
            {
                yield return null;
            }

            operation.Complete(asset);
            while (!awaiter.IsCompleted)
            {
                yield return null;
            }

            var lease = awaiter.GetResult();
            Assert.That(lease.Asset, Is.SameAs(asset));
            Assert.That(operation.ReleaseCount, Is.Zero);

            lease.Dispose();
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [UnityTest]
        public IEnumerator SynchronousStartFailureIsWrappedWithoutRelease()
        {
            var cause = new InvalidOperationException("start failed");
            var backend = new FakeBackend { StartException = cause };
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                _ => Assert.Fail("A failed start must not return a lease."),
                value => exception = value);

            AssertLoadException(exception, cause);
            Assert.That(backend.StartCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NullOperationIsWrappedWithoutRelease()
        {
            var backend = new FakeBackend();
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                _ => Assert.Fail("A null operation must not return a lease."),
                value => exception = value);

            AssertLoadException(exception);
            Assert.That(backend.StartCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InvalidOperationIsWrappedWithoutInvalidRelease()
        {
            var operation = new FakeOperation<TestAsset> { IsValid = false };
            var backend = new FakeBackend(operation);
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                _ => Assert.Fail("An invalid operation must not return a lease."),
                value => exception = value);

            AssertLoadException(exception);
            Assert.That(operation.ReleaseCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletedFailurePreservesCauseAndReleasesOnce()
        {
            var cause = new InvalidOperationException("catalog miss");
            var operation = FakeOperation<TestAsset>.Failed(cause);
            var backend = new FakeBackend(operation);
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                _ => Assert.Fail("A failed operation must not return a lease."),
                value => exception = value);

            AssertLoadException(exception, cause);
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SuccessfulOperationWithNullResultReleasesOnceAndThrows()
        {
            var operation = FakeOperation<TestAsset>.Completed(null);
            var backend = new FakeBackend(operation);
            Exception exception = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                _ => Assert.Fail("A null result must not return a lease."),
                value => exception = value);

            AssertLoadException(exception);
            Assert.That(operation.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RepeatedLoadsHaveIndependentOwners()
        {
            var firstAsset = ScriptableObject.CreateInstance<TestAsset>();
            var secondAsset = ScriptableObject.CreateInstance<TestAsset>();
            var firstOperation = FakeOperation<TestAsset>.Completed(firstAsset);
            var secondOperation = FakeOperation<TestAsset>.Completed(secondAsset);
            var backend = new FakeBackend(firstOperation, secondOperation);
            AddressableAssetLease<TestAsset> firstLease = null;
            AddressableAssetLease<TestAsset> secondLease = null;

            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                value => firstLease = value,
                value => Assert.Fail(value.ToString()));
            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, backend),
                value => secondLease = value,
                value => Assert.Fail(value.ToString()));

            firstLease.Dispose();
            Assert.That(firstOperation.ReleaseCount, Is.EqualTo(1));
            Assert.That(secondOperation.ReleaseCount, Is.Zero);
            Assert.That(secondLease.Asset, Is.SameAs(secondAsset));

            secondLease.Dispose();
            Assert.That(secondOperation.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(firstAsset);
            UnityEngine.Object.DestroyImmediate(secondAsset);
        }

        [UnityTest]
        public IEnumerator NullArgumentsFailBeforeStartingOperation()
        {
            var backend = new FakeBackend();
            Exception nullReferenceException = null;
            Exception nullBackendException = null;

            yield return Await(
                UnityObjectReferenceAddressablesExtensions.LoadAddressableAsync<TestAsset>(
                    null, default, backend),
                _ => Assert.Fail("A null reference must not return a lease."),
                value => nullReferenceException = value);
            yield return Await(
                Reference().LoadAddressableAsync<TestAsset>(default, null),
                _ => Assert.Fail("A null backend must not return a lease."),
                value => nullBackendException = value);

            Assert.That(nullReferenceException, Is.TypeOf<ArgumentNullException>());
            Assert.That(nullBackendException, Is.TypeOf<ArgumentNullException>());
            Assert.That(backend.StartCount, Is.Zero);
        }

        private static UnityObjectReference Reference()
        {
            return new UnityObjectReference(AssetGuid, AssetPath);
        }

        private static IEnumerator Await<T>(Awaitable<T> awaitable,
            Action<T> onSuccess, Action<Exception> onFailure)
        {
            var awaiter = awaitable.GetAwaiter();
            while (!awaiter.IsCompleted)
            {
                yield return null;
            }

            try
            {
                onSuccess(awaiter.GetResult());
            }
            catch (Exception exception)
            {
                onFailure(exception);
            }
        }

        private static void AssertLoadException(Exception exception,
            Exception innerException = null)
        {
            Assert.That(exception, Is.TypeOf<AddressableAssetLoadException>());
            var loadException = (AddressableAssetLoadException)exception;
            Assert.That(loadException.AssetGuid, Is.EqualTo(AssetGuid));
            Assert.That(loadException.AssetPath, Is.EqualTo(AssetPath));
            Assert.That(loadException.RequestedType, Is.EqualTo(typeof(TestAsset)));
            Assert.That(loadException.InnerException, Is.SameAs(innerException));
            Assert.That(loadException.Message, Does.Contain(AssetGuid)
                .And.Contain(AssetPath)
                .And.Contain(typeof(TestAsset).FullName));
        }

        private sealed class TestAsset : ScriptableObject
        {
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
