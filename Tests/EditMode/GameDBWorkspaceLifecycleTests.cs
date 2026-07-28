using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBWorkspaceLifecycleTests
    {
        private const string DatabasePath =
            "Assets/GameDBWorkspaceLifecycleTests/database.json";

        [Test]
        public void Workspace_RestoresOwnsFocusesPersistsAndReleasesSessionsOnce()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var source = CreateDocument().CaptureState();
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("restored", source,
                    new GameDBWorkspaceTabViewState(searchText: "sword"))
            }, "restored")).Success, Is.True);
            store.ResetWriteCount();
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var hub = new GameDBActiveWorkspaceHub();

            var workspace = new GameDBEditorWorkspace(registry, recovery, hub, "owner");
            var session = workspace.Tabs.Single().Session;

            Assert.That(workspace.ActiveTabId, Is.EqualTo("restored"));
            Assert.That(workspace.Tabs.Single().ViewState.SearchText, Is.EqualTo("sword"));
            Assert.That(hub.RegistrationCount, Is.EqualTo(1));
            Assert.That(hub.TryGetActive(out _), Is.False);
            Assert.That(workspace.MarkFocused(), Is.True);
            Assert.That(hub.TryGetActive(out var active), Is.True);
            Assert.That(active, Is.SameAs(workspace));

            workspace.Dispose();
            workspace.Dispose();

            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(hub.RegistrationCount, Is.Zero);
            Assert.That(workspace.IsDisposed, Is.True);
        }

        [Test]
        public void Workspace_ExplicitPersistPreventsDuplicateTeardownWrite()
        {
            var store = new MemoryStore();
            var workspace = CreateWorkspace(store, out var hub);

            var persisted = workspace.PersistRecovery();
            workspace.Dispose();

            Assert.That(persisted.Success, Is.True, persisted.Error);
            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(hub.RegistrationCount, Is.Zero);
        }

        [Test]
        public void Workspace_ViewStateChangeAfterPersistTriggersTeardownWrite()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("active", CreateDocument().CaptureState())
            }, "active")).Success, Is.True);
            store.ResetWriteCount();
            var workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());

            Assert.That(workspace.PersistRecovery().Success, Is.True);
            workspace.Tabs.Single().ViewState =
                new GameDBWorkspaceTabViewState(searchText: "changed");
            workspace.Dispose();

            Assert.That(store.WriteCount, Is.EqualTo(2));
        }

        [Test]
        public void Workspace_FailedTeardownPersistStillReleasesRegistrationAndSession()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("active", CreateDocument().CaptureState())
            }, "active")).Success, Is.True);
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var hub = new GameDBActiveWorkspaceHub();
            var workspace = new GameDBEditorWorkspace(registry, recovery, hub);
            var session = workspace.Tabs.Single().Session;
            store.FailWrites = true;

            var exception = Assert.Throws<InvalidOperationException>(() => workspace.Dispose());

            Assert.That(exception.Message, Does.Contain("recovery write failed"));
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(hub.RegistrationCount, Is.Zero);
            Assert.That(workspace.IsDisposed, Is.True);
            var replacement = registry.TryAcquire(DatabasePath, "replacement");
            Assert.That(replacement.Status,
                Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            replacement.Lease.Dispose();
        }

        [Test]
        public void Workspace_FacadeUsesRestoredSessionAndPreservesLegacyAddExceptions()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var document = CreateDocument();
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Power",
                    new GameDBFieldTypeSpec(FieldType.@int, false, null, null))
            }).Success, Is.True);
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("active", document.CaptureState())
            }, "active")).Success, Is.True);
            store.ResetWriteCount();
            var workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());

            workspace.AddRowToTable("Items", "Sword",
                new Dictionary<string, object> { { "Power", 12 } });

            var row = workspace.Tabs.Single().Session.CreateSnapshot().Tables
                .Single(table => table.Name == "Items").Rows.Single();
            Assert.That(row.Key, Is.EqualTo("Sword"));
            Assert.That(row.Values["Power"], Is.EqualTo(12L));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
                workspace.AddRowToTable("Missing", "Key", new Dictionary<string, object>()))
                .ParamName, Is.EqualTo("table"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
                workspace.AddRowToTable("Items", "Sword", new Dictionary<string, object>()))
                .ParamName, Is.EqualTo("key"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
                workspace.AddRowToTable("Items", "Shield",
                    new Dictionary<string, object> { { "Missing", 1 } })).ParamName,
                Is.EqualTo("Field"));
            Assert.Throws<InvalidCastException>(() => workspace.AddRowToTable(
                "Items", "Shield", new Dictionary<string, object> { { "Power", "high" } }));
            workspace.Dispose();
        }

        [Test]
        public void Workspace_DisposalRestoresHeadlessRouterFallback()
        {
            var store = new MemoryStore();
            var hub = new GameDBActiveWorkspaceHub();
            var workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), hub);
            var fallback = new RecordingTarget();
            var router = new GameDBEditorFacadeRouter(hub, fallback);

            Assert.That(workspace.MarkFocused(), Is.True);
            workspace.Dispose();

            Assert.That(router.SaveGameDB(), Is.False);
            Assert.That(fallback.SaveCalls, Is.EqualTo(1));
        }

        private static GameDBEditorWorkspace CreateWorkspace(MemoryStore store,
            out GameDBActiveWorkspaceHub hub)
        {
            hub = new GameDBActiveWorkspaceHub();
            return new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), hub);
        }

        private static GameDBDocument CreateDocument()
        {
            return GameDBDocument.CreateNew(DatabasePath, "WorkspaceLifecycle", false,
                GameDBFilePairStore.Instance, new NoOpPostSaveActions());
        }

        private sealed class MemoryStore : IGameDBWorkspaceRecoveryStore
        {
            internal string Contents { get; private set; }
            internal int WriteCount { get; private set; }
            internal bool FailWrites { get; set; }
            public bool Exists => Contents != null;

            public string ReadAllText()
            {
                return Contents;
            }

            public void WriteAtomically(string contents)
            {
                if (FailWrites)
                {
                    throw new InvalidOperationException("recovery write failed");
                }
                Contents = contents;
                WriteCount++;
            }

            public string QuarantinePrimary()
            {
                Contents = null;
                return "quarantine.json";
            }

            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }

            internal void ResetWriteCount()
            {
                WriteCount = 0;
            }
        }

        private sealed class NoOpPostSaveActions : IGameDBPostSaveActions
        {
            public void Import(string assetPath)
            {
            }

            public void Notify(string scopeName)
            {
            }
        }

        private sealed class RecordingTarget : IGameDBEditorFacadeTarget
        {
            internal int SaveCalls { get; private set; }

            public bool LoadGameDB(string gameDBPath)
            {
                return false;
            }

            public bool SaveGameDB()
            {
                SaveCalls++;
                return false;
            }

            public void AddRowToTable(string table, string key,
                Dictionary<string, object> data)
            {
            }
        }
    }
}
