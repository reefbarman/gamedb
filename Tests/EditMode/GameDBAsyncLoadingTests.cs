using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    internal sealed class GameDBAsyncLoadingTests
    {
        [UnityTest]
        public IEnumerator LoadAsync_SuccessPublishesCompleteDataBeforeNotification()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var loader = FakeLoader.Completed(Json("new-first", "new-second"));
            var notifications = 0;
            string firstAtNotification = null;
            string secondAtNotification = null;
            gameDB.OnDBLoaded += () =>
            {
                notifications++;
                firstAtNotification = gameDB.GetValue("First");
                secondAtNotification = gameDB.GetValue("Second");
            };
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("test-location", loader),
                value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(loader.StartCount, Is.EqualTo(1));
            Assert.That(loader.Location, Is.EqualTo("test-location"));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(firstAtNotification, Is.EqualTo("new-first"));
            Assert.That(secondAtNotification, Is.EqualTo("new-second"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_PreCancelledStartsNoLoader()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var loader = FakeLoader.Pending();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("test-location", loader,
                cancellation.Token), value => exception = value);

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(loader.StartCount, Is.Zero);
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_PendingOwnerRejectsSecondAndThirdLoads()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var owner = FakeLoader.Pending();
            var rejected = FakeLoader.Completed(Json("rejected-first", "rejected-second"));
            var ownerAwaitable = gameDB.LoadAsync("owner", owner);
            var ownerAwaiter = ownerAwaitable.GetAwaiter();

            yield return WaitUntil(() => owner.StartCount > 0,
                "Owner loader did not start.");

            Exception secondException = null;
            Exception thirdException = null;
            yield return Await(gameDB.LoadAsync("second", rejected),
                value => secondException = value);
            yield return Await(gameDB.LoadAsync("third", rejected),
                value => thirdException = value);

            Assert.That(secondException, Is.TypeOf<InvalidOperationException>());
            Assert.That(thirdException, Is.TypeOf<InvalidOperationException>());
            Assert.That(rejected.StartCount, Is.Zero);
            Assert.That(ownerAwaiter.IsCompleted, Is.False);

            owner.Complete(Json("owner-first", "owner-second"));
            yield return WaitUntil(() => ownerAwaiter.IsCompleted,
                "Owner load did not complete.");

            Assert.DoesNotThrow(() => ownerAwaiter.GetResult());
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("owner-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("owner-second"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_CancellationPreservesDataAndReleasesGate()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var loader = FakeLoader.Pending();
            using var cancellation = new CancellationTokenSource();
            var notifications = 0;
            gameDB.OnDBLoaded += () => notifications++;
            var awaitable = gameDB.LoadAsync("pending", loader, cancellation.Token);
            var awaiter = awaitable.GetAwaiter();

            yield return WaitUntil(() => loader.StartCount > 0,
                "Pending loader did not start.");

            cancellation.Cancel();
            loader.Cancel();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Cancelled load did not complete.");

            Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            Assert.That(notifications, Is.Zero);

            yield return Await(gameDB.LoadAsync("next",
                FakeLoader.Completed(Json("next-first", "next-second"))),
                value => Assert.That(value, Is.Null));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("next-first"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_CancelledAfterTransportPreservesDataAndReleasesGate()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            using var cancellation = new CancellationTokenSource();
            var loader = new CancelOnReturnLoader(
                Json("new-first", "new-second"), cancellation);
            var notifications = 0;
            gameDB.OnDBLoaded += () => notifications++;
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("completed", loader,
                cancellation.Token), value => exception = value);

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(loader.StartCount, Is.EqualTo(1));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            Assert.That(notifications, Is.Zero);

            yield return Await(gameDB.LoadAsync("next",
                FakeLoader.Completed(Json("next-first", "next-second"))),
                value => Assert.That(value, Is.Null));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("next-first"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_SynchronousLoaderFailureIsWrappedOnce()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var cause = new InvalidOperationException("start failed");
            var loader = FakeLoader.Throwing(cause);
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("broken", loader),
                value => exception = value);

            AssertLoadException(exception, "broken", loader, cause);
        }

        [UnityTest]
        public IEnumerator LoadAsync_AsynchronousLoaderFailureIsWrappedOnce()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var cause = new InvalidOperationException("completion failed");
            var loader = FakeLoader.Failed(cause);
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("broken", loader),
                value => exception = value);

            AssertLoadException(exception, "broken", loader, cause);
        }

        [UnityTest]
        public IEnumerator LoadAsync_MetadataPublishesWithRowsBeforeNotification()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            gameDB.SetMetadata("old-language");
            string metadataAtNotification = null;
            string valueAtNotification = null;
            gameDB.OnDBLoaded += () =>
            {
                metadataAtNotification = gameDB.Metadata;
                valueAtNotification = gameDB.GetValue("First");
            };
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("localized",
                    FakeLoader.Completed(Json("new-first", "new-second")),
                    "new-language"),
                value => exception = value);

            Assert.That(exception, Is.Null);
            Assert.That(gameDB.Metadata, Is.EqualTo("new-language"));
            Assert.That(metadataAtNotification, Is.EqualTo("new-language"));
            Assert.That(valueAtNotification, Is.EqualTo("new-first"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_FailurePreservesMetadataAndRows()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            gameDB.SetMetadata("old-language");
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("localized",
                    FakeLoader.Completed(Json("new-first", 42)),
                    "new-language"),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<FormatException>());
            Assert.That(gameDB.Metadata, Is.EqualTo("old-language"));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_CancellationPreservesMetadataAndRows()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            gameDB.SetMetadata("old-language");
            var loader = FakeLoader.Pending();
            using var cancellation = new CancellationTokenSource();
            var awaitable = gameDB.LoadAsync("localized", loader,
                "new-language", cancellation.Token);
            var awaiter = awaitable.GetAwaiter();

            yield return WaitUntil(() => loader.StartCount > 0,
                "Localized loader did not start.");

            cancellation.Cancel();
            loader.Cancel();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Cancelled localized load did not complete.");

            Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
            Assert.That(gameDB.Metadata, Is.EqualTo("old-language"));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_ImportFailureRetainsConcreteTypeAndOldData()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var loader = FakeLoader.Completed(Json("new-first", 42));
            Exception exception = null;

            yield return Await(gameDB.LoadAsync("malformed", loader),
                value => exception = value);

            Assert.That(exception, Is.TypeOf<FormatException>());
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
        }

        private static void AssertLoadException(Exception exception, string location,
            FakeLoader loader, Exception cause)
        {
            Assert.That(exception, Is.TypeOf<GameDBDataLoadException>());
            var loadException = (GameDBDataLoadException)exception;
            Assert.That(loadException.Location, Is.EqualTo(location));
            Assert.That(loadException.LoaderType, Is.EqualTo(loader.GetType()));
            Assert.That(loadException.InnerException, Is.SameAs(cause));
        }

        private static IEnumerator Await(Awaitable awaitable,
            Action<Exception> onCompleted)
        {
            var awaiter = awaitable.GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted,
                "Async database load did not complete.");

            try
            {
                awaiter.GetResult();
                onCompleted(null);
            }
            catch (Exception exception)
            {
                onCompleted(exception);
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

        private static AsyncTestGameDB CreateLoadedDatabase(string first, string second)
        {
            var gameDB = new AsyncTestGameDB();
            Assert.That(gameDB.Import(Json(first, second), false), Is.Null);
            return gameDB;
        }

        private static string Json(object firstValue, object secondValue)
        {
            return "{\"tables\":{" +
                "\"First\":{\"Row\":{\"Value\":" + JsonValue(firstValue) + "}}," +
                "\"Second\":{\"Row\":{\"Value\":" + JsonValue(secondValue) + "}}}}";
        }

        private static string JsonValue(object value)
        {
            return value is string text ? "\"" + text + "\"" : value.ToString();
        }

        private sealed class AsyncTestGameDB : GameDBBase
        {
            internal AsyncTestGameDB()
                : base("AsyncTest", "AsyncTest")
            {
                Tables.Add("First", CreateTable("First"));
                Tables.Add("Second", CreateTable("Second"));
            }

            internal string Metadata { get; private set; }

            internal Awaitable LoadAsync(string location, IGameDBDataLoader loader,
                CancellationToken cancellationToken = default)
            {
                return LoadDataAsync(location, loader,
                    cancellationToken: cancellationToken);
            }

            internal Awaitable LoadAsync(string location, IGameDBDataLoader loader,
                string metadata, CancellationToken cancellationToken = default)
            {
                return LoadDataAsync(location, loader,
                    beforePublish: () => Metadata = metadata,
                    cancellationToken: cancellationToken);
            }

            internal void SetMetadata(string metadata)
            {
                Metadata = metadata;
            }

            internal string GetValue(string table)
            {
                return (string)Tables[table].GetByKeyRaw("Row").GetValue("Value");
            }

            private static TableBase CreateTable(string name)
            {
                var table = new TableBase(name, KeyType.@string, null,
                    key => new RowBase(key));
                table.Fields.Add("Value", new FieldBase("Value", FieldType.@string, false));
                return table;
            }
        }

        private sealed class CancelOnReturnLoader : IGameDBDataLoader
        {
            private readonly string m_json;
            private readonly CancellationTokenSource m_cancellation;

            internal CancelOnReturnLoader(string json,
                CancellationTokenSource cancellation)
            {
                m_json = json;
                m_cancellation = cancellation;
            }

            internal int StartCount { get; private set; }

            public Awaitable<string> LoadAsync(string location,
                CancellationToken cancellationToken = default)
            {
                StartCount++;
                m_cancellation.Cancel();
                var completion = new AwaitableCompletionSource<string>();
                completion.SetResult(m_json);
                return completion.Awaitable;
            }
        }

        private sealed class FakeLoader : IGameDBDataLoader
        {
            private readonly AwaitableCompletionSource<string> m_completion;
            private readonly Exception m_startException;

            private FakeLoader(AwaitableCompletionSource<string> completion = null,
                Exception startException = null)
            {
                m_completion = completion;
                m_startException = startException;
            }

            internal int StartCount { get; private set; }
            internal string Location { get; private set; }

            internal static FakeLoader Completed(string json)
            {
                var completion = new AwaitableCompletionSource<string>();
                completion.SetResult(json);
                return new FakeLoader(completion);
            }

            internal static FakeLoader Pending()
            {
                return new FakeLoader(completion: new AwaitableCompletionSource<string>());
            }

            internal static FakeLoader Failed(Exception exception)
            {
                var completion = new AwaitableCompletionSource<string>();
                completion.SetException(exception);
                return new FakeLoader(completion: completion);
            }

            internal static FakeLoader Throwing(Exception exception)
            {
                return new FakeLoader(startException: exception);
            }

            internal void Complete(string json)
            {
                m_completion.SetResult(json);
            }

            internal void Cancel()
            {
                m_completion.SetCanceled();
            }

            public Awaitable<string> LoadAsync(string location,
                CancellationToken cancellationToken = default)
            {
                StartCount++;
                Location = location;
                if (m_startException != null)
                {
                    throw m_startException;
                }

                return m_completion.Awaitable;
            }
        }
    }
}
