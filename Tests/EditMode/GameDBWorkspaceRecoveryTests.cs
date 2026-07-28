using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBWorkspaceRecoveryTests
    {
        private const string FirstPath = "Assets/GameDBWorkspaceRecoveryTests/first.json";
        private const string SecondPath = "Assets/GameDBWorkspaceRecoveryTests/second.json";

        [Test]
        public void SaveLoad_RoundTripsDocumentAndViewStateWithImmutableCopies()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var document = GameDBDocument.CreateNew(FirstPath, "Recovery", false);
            var state = document.CaptureState();
            var sorts = new List<GameDBWorkspaceSortState>
            {
                new GameDBWorkspaceSortState("Power", true)
            };
            var columns = new List<GameDBWorkspaceColumnState>
            {
                new GameDBWorkspaceColumnState("Key", 140.5f, 0),
                new GameDBWorkspaceColumnState("Power", 90f, 1)
            };
            var tab = new GameDBWorkspaceRecoveryTab("first", state,
                new GameDBWorkspaceTabViewState("Items", "Sword", " sw ",
                    sorts, columns, 12.5f, 48.25f));
            var snapshot = new GameDBWorkspaceRecoverySnapshot(new[] { tab }, "first");

            var saved = service.Save(snapshot);
            state.AssetPath = "mutated";
            sorts.Clear();
            columns.Clear();
            var loaded = new GameDBWorkspaceRecoveryService(store).Load();

            Assert.That(saved.Success, Is.True, saved.Error);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Issues, Is.Empty);
            Assert.That(loaded.Snapshot.Version,
                Is.EqualTo(GameDBWorkspaceRecoverySnapshot.CurrentVersion));
            Assert.That(loaded.Snapshot.ActiveTabId, Is.EqualTo("first"));
            Assert.That(loaded.Snapshot.Tabs, Has.Count.EqualTo(1));
            var restored = loaded.Snapshot.Tabs[0];
            Assert.That(restored.DocumentState.AssetPath, Is.EqualTo(FirstPath));
            Assert.That(restored.DocumentState.DocumentId, Is.EqualTo(tab.DocumentState.DocumentId));
            Assert.That(restored.DocumentState.DataJson, Is.EqualTo(tab.DocumentState.DataJson));
            Assert.That(restored.DocumentState.SchemaJson, Is.EqualTo(tab.DocumentState.SchemaJson));
            Assert.That(restored.DocumentState.BaselineDiskToken,
                Is.EqualTo(tab.DocumentState.BaselineDiskToken));
            Assert.That(restored.DocumentState.WasDirty, Is.EqualTo(tab.DocumentState.WasDirty));
            Assert.That(restored.ViewState.SelectedTableId, Is.EqualTo("Items"));
            Assert.That(restored.ViewState.SelectedRowId, Is.EqualTo("Sword"));
            Assert.That(restored.ViewState.SearchText, Is.EqualTo(" sw "));
            Assert.That(restored.ViewState.Sorts.Select(sort =>
                new { sort.FieldId, sort.Descending }),
                Is.EqualTo(new[] { new { FieldId = "Power", Descending = true } }));
            Assert.That(restored.ViewState.Columns.Select(column =>
                new { column.FieldId, column.Width, column.Order }),
                Is.EqualTo(new[]
                {
                    new { FieldId = "Key", Width = 140.5f, Order = 0 },
                    new { FieldId = "Power", Width = 90f, Order = 1 }
                }));
            Assert.That(restored.ViewState.HorizontalScroll, Is.EqualTo(12.5f));
            Assert.That(restored.ViewState.VerticalScroll, Is.EqualTo(48.25f));
            Assert.That(((IList<GameDBWorkspaceRecoveryTab>)loaded.Snapshot.Tabs).IsReadOnly,
                Is.True);
            Assert.That(((IList<GameDBWorkspaceSortState>)restored.ViewState.Sorts).IsReadOnly,
                Is.True);

            var detached = restored.DocumentState;
            detached.AssetPath = "changed-again";
            Assert.That(restored.DocumentState.AssetPath, Is.EqualTo(FirstPath));
        }

        [Test]
        public void Load_MissingStoreReturnsEmptySnapshotWithoutWriting()
        {
            var store = new MemoryStore();
            var result = new GameDBWorkspaceRecoveryService(store).Load();

            Assert.That(result.Success, Is.True);
            Assert.That(result.Snapshot.Tabs, Is.Empty);
            Assert.That(result.Snapshot.ActiveTabId, Is.Null);
            Assert.That(store.WriteCount, Is.Zero);
            Assert.That(store.QuarantinePrimaryCount, Is.Zero);
        }

        [TestCase("not json")]
        [TestCase("{\"version\":999,\"activeTabId\":null,\"tabs\":[]}")]
        [TestCase("{\"version\":1,\"activeTabId\":null,\"tabs\":{}}")]
        public void Load_InvalidTopLevelPayloadQuarantinesPrimary(string contents)
        {
            var store = new MemoryStore { Contents = contents };
            var result = new GameDBWorkspaceRecoveryService(store).Load();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.StartWith("Failed to load GameDB workspace recovery:"));
            Assert.That(result.Snapshot.Tabs, Is.Empty);
            Assert.That(result.QuarantinePath, Is.EqualTo("quarantine-primary.json"));
            Assert.That(store.QuarantinedPrimaryContents, Is.EqualTo(contents));
            Assert.That(store.Exists, Is.False);
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_MalformedTabQuarantinesFragmentAndKeepsValidTabsInOrder()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var first = RecoveryTab("first", FirstPath);
            var second = RecoveryTab("second", SecondPath);
            Assert.That(service.Save(new GameDBWorkspaceRecoverySnapshot(
                new[] { first, second }, "second")).Success, Is.True);
            var root = (IDictionary<string, object>)JsonSerialization.Deserialize(store.Contents);
            var tabs = (IList<object>)root["tabs"];
            tabs.Insert(1, new Dictionary<string, object>
            {
                { "tabId", "broken" },
                { "document", "invalid" },
                { "view", new Dictionary<string, object>() }
            });
            store.Contents = JsonSerialization.Serialize(root);

            var loaded = new GameDBWorkspaceRecoveryService(store).Load();

            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Snapshot.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "first", "second" }));
            Assert.That(loaded.Snapshot.ActiveTabId, Is.EqualTo("second"));
            Assert.That(loaded.Issues, Has.Count.EqualTo(1));
            Assert.That(loaded.Issues[0].TabId, Is.EqualTo("broken"));
            Assert.That(loaded.Issues[0].QuarantinePath,
                Is.EqualTo("quarantine-broken.json"));
            Assert.That(store.QuarantinedFragments["broken"], Does.Contain("\"tabId\""));
        }

        [Test]
        public void RestoreAssetSessions_ContinuesAfterInvalidDocumentAndQuarantinesFullTab()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var valid = RecoveryTab("valid", FirstPath);
            var invalidState = RecoveryTab("invalid", SecondPath).DocumentState;
            invalidState.Version++;
            var invalid = new GameDBWorkspaceRecoveryTab("invalid", invalidState,
                new GameDBWorkspaceTabViewState(searchText: "retain me"));
            var snapshot = new GameDBWorkspaceRecoverySnapshot(
                new[] { invalid, valid }, "invalid");
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);

            var restored = service.RestoreAssetSessions(snapshot, registry);

            Assert.That(restored.Tabs.Select(tab => tab.TabId), Is.EqualTo(new[] { "valid" }));
            Assert.That(restored.ActiveTabId, Is.EqualTo("valid"));
            Assert.That(restored.Issues, Has.Count.EqualTo(2));
            Assert.That(restored.Issues[0].TabId, Is.EqualTo("invalid"));
            Assert.That(restored.Issues[0].Message, Does.Contain("Unsupported document state version"));
            Assert.That(restored.Issues[1].Message, Does.Contain("first restored tab was activated"));
            Assert.That(store.QuarantinedFragments["invalid"], Does.Contain("retain me"));
            Assert.That(restored.Tabs[0].Session.AssetPath, Is.EqualTo(FirstPath));
            restored.Tabs[0].Session.Dispose();
        }

        [Test]
        public void SaveLoadRestore_RoundTripsParsedDocumentStateIntoLiveSession()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var source = RecoveryTab("first", FirstPath);
            var sourceState = source.DocumentState;
            sourceState.BaselineDiskToken = new GameDBDiskToken
            {
                DataExists = true,
                SchemaExists = true,
                DataSha256 = "data-hash",
                SchemaSha256 = "schema-hash"
            };
            sourceState.PersistenceStateUnknown = true;
            var snapshot = new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("first", sourceState,
                    new GameDBWorkspaceTabViewState(horizontalScroll: 0.1f,
                        verticalScroll: 1234.5678f))
            }, "first");

            var saved = service.Save(snapshot);
            var loaded = new GameDBWorkspaceRecoveryService(store).Load();
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var restored = new GameDBWorkspaceRecoveryService(store)
                .RestoreAssetSessions(loaded.Snapshot, registry);

            Assert.That(saved.Success, Is.True, saved.Error);
            Assert.That(saved.Issues, Is.Empty);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Snapshot.Tabs[0].DocumentState.BaselineDiskToken,
                Is.EqualTo(sourceState.BaselineDiskToken));
            Assert.That(loaded.Snapshot.Tabs[0].DocumentState.PersistenceStateUnknown, Is.True);
            Assert.That(loaded.Snapshot.Tabs[0].ViewState.HorizontalScroll, Is.EqualTo(0.1f));
            Assert.That(loaded.Snapshot.Tabs[0].ViewState.VerticalScroll,
                Is.EqualTo(1234.5678f));
            Assert.That(restored.Issues, Is.Empty);
            Assert.That(restored.Tabs, Has.Count.EqualTo(1));
            Assert.That(restored.Tabs[0].Session.GetState().PersistenceStateUnknown, Is.True);
            restored.Tabs[0].Session.Dispose();
        }

        [Test]
        public void Save_InvalidTabIsQuarantinedWhileHealthyTabsArePersisted()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var invalidState = RecoveryTab("invalid", FirstPath).DocumentState;
            invalidState.Version++;
            var snapshot = new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("invalid", invalidState),
                RecoveryTab("valid", SecondPath)
            }, "invalid");

            var saved = service.Save(snapshot);
            var loaded = new GameDBWorkspaceRecoveryService(store).Load();

            Assert.That(saved.Success, Is.True, saved.Error);
            Assert.That(saved.Issues, Has.Count.EqualTo(1));
            Assert.That(saved.Issues[0].TabId, Is.EqualTo("invalid"));
            Assert.That(store.QuarantinedFragments["invalid"],
                Does.Contain("\"version\": 2"));
            Assert.That(loaded.Snapshot.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "valid" }));
            Assert.That(loaded.Snapshot.ActiveTabId, Is.Null);
        }

        [Test]
        public void SaveExact_InvalidTabDoesNotWriteOrQuarantine()
        {
            var store = new MemoryStore { Contents = "previous" };
            var service = new GameDBWorkspaceRecoveryService(store);
            var invalidState = RecoveryTab("invalid", FirstPath).DocumentState;
            invalidState.Version++;

            var result = service.SaveExact(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("invalid", invalidState),
                RecoveryTab("valid", SecondPath)
            }, "valid"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("Unsupported document state version"));
            Assert.That(result.Issues, Is.Empty);
            Assert.That(store.Contents, Is.EqualTo("previous"));
            Assert.That(store.WriteCount, Is.Zero);
            Assert.That(store.QuarantinedFragments, Is.Empty);
        }

        [Test]
        public void RestoreAssetSessions_BusyTabDoesNotLeakLeaseOrBlockOtherTabs()
        {
            var store = new MemoryStore();
            var service = new GameDBWorkspaceRecoveryService(store);
            var first = RecoveryTab("first", FirstPath);
            var duplicate = new GameDBWorkspaceRecoveryTab("duplicate", first.DocumentState);
            var snapshot = new GameDBWorkspaceRecoverySnapshot(
                new[] { first, duplicate }, "first");
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);

            var restored = service.RestoreAssetSessions(snapshot, registry);

            Assert.That(restored.Tabs.Select(tab => tab.TabId), Is.EqualTo(new[] { "first" }));
            Assert.That(restored.Issues, Has.Count.EqualTo(1));
            Assert.That(restored.Issues[0].Message, Does.Contain("already open"));
            restored.Tabs[0].Session.Dispose();
            var reacquired = registry.TryAcquire(FirstPath, "after-restore");
            Assert.That(reacquired.Status, Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            reacquired.Lease.Dispose();
        }

        [Test]
        public void ViewState_RejectsNonFiniteGeometryAndNullEntries()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameDBWorkspaceTabViewState(horizontalScroll: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameDBWorkspaceTabViewState(columns: new[]
                {
                    new GameDBWorkspaceColumnState("Power", float.PositiveInfinity, 0)
                }));
            Assert.Throws<ArgumentException>(() =>
                new GameDBWorkspaceTabViewState(sorts: new GameDBWorkspaceSortState[] { null }));
            Assert.Throws<ArgumentException>(() =>
                new GameDBWorkspaceRecoverySnapshot(new GameDBWorkspaceRecoveryTab[] { null }));
        }

        [Test]
        public void Save_WriteFailureIsReportedWithoutChangingStore()
        {
            var store = new MemoryStore { Contents = "previous" };
            store.WriteException = new IOException("disk full");
            var service = new GameDBWorkspaceRecoveryService(store);

            var result = service.Save(new GameDBWorkspaceRecoverySnapshot(
                new[] { RecoveryTab("first", FirstPath) }, "first"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("disk full"));
            Assert.That(store.Contents, Is.EqualTo("previous"));
        }

        [Test]
        public void FileStore_AtomicallyWritesAndRenamesPrimaryToQuarantine()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "GameDBWorkspaceRecoveryTests_" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "Library", "GameDB", "WorkspaceRecovery.json");

            try
            {
                var store = new GameDBWorkspaceRecoveryFileStore(path);
                Assert.That(store.Exists, Is.False);

                store.WriteAtomically("first");
                store.WriteAtomically("second");
                var quarantine = store.QuarantinePrimary();

                Assert.That(store.Exists, Is.False);
                Assert.That(File.ReadAllText(quarantine), Is.EqualTo("second"));
                Assert.That(Path.GetFileName(quarantine), Does.Contain("workspace.quarantine"));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.bak"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static GameDBWorkspaceRecoveryTab RecoveryTab(string tabId, string assetPath)
        {
            var document = GameDBDocument.CreateNew(assetPath, "Recovery", false);
            return new GameDBWorkspaceRecoveryTab(tabId, document.CaptureState(),
                new GameDBWorkspaceTabViewState());
        }

        private sealed class MemoryStore : IGameDBWorkspaceRecoveryStore
        {
            internal string Contents { get; set; }
            internal int WriteCount { get; private set; }
            internal int QuarantinePrimaryCount { get; private set; }
            internal string QuarantinedPrimaryContents { get; private set; }
            internal Dictionary<string, string> QuarantinedFragments { get; }
                = new Dictionary<string, string>(StringComparer.Ordinal);
            internal Exception WriteException { get; set; }

            public bool Exists => Contents != null;

            public string ReadAllText()
            {
                return Contents;
            }

            public void WriteAtomically(string contents)
            {
                WriteCount++;
                if (WriteException != null)
                {
                    throw WriteException;
                }
                Contents = contents;
            }

            public string QuarantinePrimary()
            {
                QuarantinePrimaryCount++;
                QuarantinedPrimaryContents = Contents;
                Contents = null;
                return "quarantine-primary.json";
            }

            public string WriteQuarantine(string label, string contents)
            {
                QuarantinedFragments[label] = contents;
                return "quarantine-" + label + ".json";
            }
        }
    }
}
