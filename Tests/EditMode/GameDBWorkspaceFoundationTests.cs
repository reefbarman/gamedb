using GameDBEditorLibrary;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace GameDBLibrary.Tests
{
    public class GameDBWorkspaceFoundationTests
    {
        private string m_rootPath;
        private string m_databasePath;

        [SetUp]
        public void SetUp()
        {
            m_rootPath = $"Assets/GameDBWorkspaceFoundationTests_{Guid.NewGuid():N}";
            m_databasePath = $"{m_rootPath}/database.json";
        }

        [TearDown]
        public void TearDown()
        {
            GameDBEditor.OnGameDBSaved = null;
            AssetDatabase.DeleteAsset(m_rootPath);
        }

        [Test]
        public void Resolver_CollapsesAliasesIntoCanonicalAssetAndRelativePaths()
        {
            var alias = $"  {m_rootPath}\\nested\\..\\database.json  ";

            var resolved = GameDBFilePairStore.Instance.Resolve(alias);

            Assert.That(resolved.AssetPath, Is.EqualTo(m_databasePath));
            Assert.That(resolved.RelativePath,
                Is.EqualTo(m_databasePath.Substring("Assets/".Length)));
            Assert.That(resolved.SchemaAssetPath,
                Is.EqualTo(Path.ChangeExtension(m_databasePath, ".schema.json")));
        }

        [Test]
        public void LeaseRegistry_CanonicalAliasIsBusyAndDoesNotInvokeFactory()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var first = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "First", false, "first");
            var alias = $"{m_rootPath}/nested/../database.json";

            var second = GameDBAssetSession.TryCreateNew(registry, alias,
                "Second", false, "second");

            Assert.That(first.Status, Is.EqualTo(GameDBAssetSessionOpenStatus.Opened));
            Assert.That(second.Status, Is.EqualTo(GameDBAssetSessionOpenStatus.Busy));
            Assert.That(second.Session, Is.Null);
            Assert.That(second.CanonicalAssetPath, Is.EqualTo(m_databasePath));
            Assert.That(second.ExistingSessionId, Is.EqualTo("first"));
            first.Session.Dispose();
        }

        [Test]
        public void LeaseRegistry_ReleaseIsIdempotentAndDistinctPathsAreIndependent()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var first = registry.TryAcquire(m_databasePath, "first");
            var otherPath = $"{m_rootPath}/other.json";
            var other = registry.TryAcquire(otherPath, "other");

            Assert.That(first.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            Assert.That(other.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            first.Lease.Dispose();
            first.Lease.Dispose();
            var reacquired = registry.TryAcquire(m_databasePath, "replacement");

            Assert.That(first.Lease.IsDisposed, Is.True);
            Assert.That(reacquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            Assert.That(reacquired.Lease.OwnerId, Is.EqualTo("replacement"));
            other.Lease.Dispose();
            reacquired.Lease.Dispose();
        }

        [Test]
        public void AssetSession_ConstructionFailuresReleaseReservationAndPreserveException()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);

            Assert.Throws<FileNotFoundException>(() =>
                GameDBAssetSession.TryOpen(registry, m_databasePath, "missing"));

            var created = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "WorkspaceTests", false, "created");
            var invalidState = created.Session.CaptureState();
            created.Session.Dispose();
            invalidState.Version++;
            Assert.Throws<FormatException>(() =>
                GameDBAssetSession.TryRestore(registry, invalidState, "invalid"));

            var acquired = registry.TryAcquire(m_databasePath, "recovered");
            Assert.That(acquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            acquired.Lease.Dispose();
        }

        [Test]
        public void AssetSession_ExposesInitialStateAndForwardsEventsWithSubscriberIsolation()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var opened = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "WorkspaceTests", false, "session");
            var session = opened.Session;
            var initial = session.GetState();
            var observed = new List<string>();
            session.Changed += change => observed.Add("content:" + change.Origin);
            session.Changed += change => throw new InvalidOperationException("content failed");
            session.Changed += change => observed.Add("content-after:" + change.Origin);
            session.StateChanged += change => observed.Add("state:" + change.Origin);
            session.StateChanged += change => throw new InvalidOperationException("state failed");
            session.StateChanged += change => observed.Add("state-after:" + change.Origin);

            var result = session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(initial.DocumentId, Is.EqualTo(session.DocumentId));
            Assert.That(initial.IsDirty, Is.True);
            Assert.That(initial.BaselineRevision, Is.Null);
            Assert.That(session.GetState().IsDirty, Is.True);
            Assert.That(observed, Is.EqualTo(new[]
            {
                "content:Transaction",
                "content-after:Transaction",
                "state:Transaction",
                "state-after:Transaction"
            }));
            Assert.That(result.NotificationErrors,
                Is.EqualTo(new[] { "content failed", "state failed" }));
            session.Dispose();
        }

        [Test]
        public void AssetSession_DisposeDetachesEventsGuardsAccessAndReleasesLease()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var opened = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "WorkspaceTests", false, "session");
            var session = opened.Session;
            var notifications = 0;
            session.StateChanged += change => notifications++;

            session.Dispose();
            session.Dispose();
            var reacquired = registry.TryAcquire(m_databasePath, "replacement");

            Assert.That(notifications, Is.Zero);
            Assert.That(session.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => session.GetState());
            Assert.Throws<ObjectDisposedException>(() => session.ApplyTransaction(
                new GameDBCommand[] { new AddTableCommand("Detached", KeyType.@string, null) }));
            Assert.Throws<ObjectDisposedException>(() => { var _ = session.DocumentId; });
            Assert.Throws<ObjectDisposedException>(() =>
                session.StateChanged += change => { });
            Assert.That(reacquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            reacquired.Lease.Dispose();
        }

        [Test]
        public void AssetSession_DisposeWaitsForInFlightDispatchBeforeReleasingLease()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var opened = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "WorkspaceTests", false, "session");
            var session = opened.Session;
            Task disposal = null;
            session.StateChanged += change =>
            {
                disposal = Task.Run(() => session.Dispose());
                Assert.That(disposal.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
                Assert.That(registry.TryAcquire(m_databasePath, "early").Status,
                    Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Busy));
            };

            var mutation = session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null)
            });

            Assert.That(mutation.Success, Is.True, mutation.Message);
            Assert.That(disposal, Is.Not.Null);
            Assert.That(disposal.Wait(TimeSpan.FromSeconds(5)), Is.True);
            var reacquired = registry.TryAcquire(m_databasePath, "replacement");
            Assert.That(reacquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            reacquired.Lease.Dispose();
        }

        [Test]
        public void AssetSession_SelfDisposeFromSavedCallbackDefersReleaseUntilSaveCompletes()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var opened = GameDBAssetSession.TryCreateNew(registry, m_databasePath,
                "WorkspaceTests", false, "session");
            var session = opened.Session;
            GameDBEditor.OnGameDBSaved = _ => session.Dispose();

            var saved = session.Save();
            var reacquired = registry.TryAcquire(m_databasePath, "replacement");

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(reacquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            reacquired.Lease.Dispose();
        }

        [Test]
        public void LeaseRegistry_UsesInjectedStoreComparer()
        {
            var caseSensitiveStore = new IdentityPairStore(StringComparer.Ordinal);
            var registry = new GameDBDocumentLeaseRegistry(caseSensitiveStore);
            var lower = registry.TryAcquire("Assets/database.json", "lower");
            var upper = registry.TryAcquire("Assets/Database.json", "upper");

            Assert.That(lower.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            Assert.That(upper.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            lower.Lease.Dispose();
            upper.Lease.Dispose();
        }

        [Test]
        public void LeaseRegistry_RejectsInvalidOwnerWithoutReservingPath()
        {
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);

            Assert.Throws<ArgumentException>(() => registry.TryAcquire(m_databasePath, " "));
            var acquired = registry.TryAcquire(m_databasePath, "valid");

            Assert.That(acquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            acquired.Lease.Dispose();
        }
        private sealed class IdentityPairStore : IGameDBPairStore
        {
            internal IdentityPairStore(StringComparer comparer)
            {
                LockKeyComparer = comparer;
            }

            public StringComparer LockKeyComparer { get; }

            public GameDBResolvedPath Resolve(string assetPath)
            {
                var normalized = assetPath.Replace('\\', '/');
                return new GameDBResolvedPath(normalized,
                    Path.ChangeExtension(normalized, ".schema.json"),
                    normalized.Substring("Assets/".Length), normalized,
                    Path.ChangeExtension(normalized, ".schema.json"), normalized);
            }

            public GameDBPairRead Read(string assetPath)
            {
                return new GameDBPairRead(Resolve(assetPath), null, null,
                    GameDBDiskToken.Absent);
            }

            public GameDBPairCommitResult Commit(string assetPath,
                GameDBDiskToken expectedToken, byte[] dataBytes, byte[] schemaBytes)
            {
                return new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.Committed,
                    TokenBefore = GameDBDiskToken.Absent,
                    TokenAfter = GameDBDiskToken.Absent
                };
            }
        }

    }
}
