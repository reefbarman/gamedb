using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBSchemaActionServiceTests
    {
        [Test]
        public void Actions_ExecuteCommandsAndRewriteViewIdentities()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                Assert.That(fixture.AddField("Items", "Name",
                    new GameDBFieldTypeSpec(FieldType.@string, false, null)).Success,
                    Is.True);
                fixture.AddRow("Items", "Sword");
                fixture.SetFieldViewState("Items", "Sword", "Name");

                var renamedTable = fixture.Invoke((revision, documentId) =>
                    fixture.Service.RenameTable(fixture.Tab, documentId, revision,
                        "Items", "Gear"));
                Assert.That(renamedTable.Success, Is.True);
                Assert.That(fixture.Tab.ViewState.SelectedTableId, Is.EqualTo("Gear"));
                Assert.That(fixture.Tab.ViewState.SelectedRowId, Is.EqualTo("Sword"));
                Assert.That(fixture.Tab.ViewState.Columns.Single().TableId,
                    Is.EqualTo("Gear"));

                var renamedField = fixture.Invoke((revision, documentId) =>
                    fixture.Service.RenameField(fixture.Tab, documentId, revision,
                        "Gear", "Name", "Label"));
                Assert.That(renamedField.Success, Is.True);
                Assert.That(fixture.Tab.ViewState.Sorts.Single().FieldId,
                    Is.EqualTo("Label"));
                Assert.That(fixture.Tab.ViewState.Columns.Single().FieldId,
                    Is.EqualTo("Label"));

                var deleted = fixture.Invoke((revision, documentId) =>
                    fixture.Service.DeleteField(fixture.Tab, documentId, revision,
                        "Gear", "Label"));
                Assert.That(deleted.Success, Is.True);
                Assert.That(fixture.Tab.ViewState.Sorts, Is.Empty);
                Assert.That(fixture.Tab.ViewState.Columns, Is.Empty);
            }
        }

        [Test]
        public void DeleteTable_SelectsFallbackAndPrunesTableState()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                Assert.That(fixture.AddTable("Abilities").Success, Is.True);
                fixture.SetFieldViewState("Items", null, "Name");
                var before = fixture.Snapshot;

                var result = fixture.Invoke((revision, documentId) =>
                    fixture.Service.DeleteTable(fixture.Tab, documentId, revision,
                        "Items", before));

                Assert.That(result.Success, Is.True, result.CommandResult?.Message);
                Assert.That(fixture.Tab.ViewState.SelectedTableId,
                    Is.EqualTo("Abilities"));
                Assert.That(fixture.Tab.ViewState.SelectedRowId, Is.Null);
                Assert.That(fixture.Tab.ViewState.Columns, Is.Empty);
                Assert.That(fixture.Policy.Requests.Single().Message,
                    Does.Contain("0 fields").And.Contain("0 rows"));
            }
        }

        [Test]
        public void NonSelectedTableFieldMutation_RewritesStoredColumnsOnly()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                Assert.That(fixture.AddField("Items", "Name",
                    new GameDBFieldTypeSpec(FieldType.@string, false, null)).Success,
                    Is.True);
                Assert.That(fixture.AddTable("Abilities").Success, Is.True);
                fixture.Workspace.TrySetTabViewState(fixture.Tab.TabId,
                    new GameDBWorkspaceTabViewState("Abilities", sorts: new[]
                    {
                        new GameDBWorkspaceSortState("Power", true)
                    }, columns: new[]
                    {
                        new GameDBWorkspaceColumnState("Name", 140f, 0, "Items")
                    }));

                var result = fixture.Invoke((revision, documentId) =>
                    fixture.Service.RenameField(fixture.Tab, documentId, revision,
                        "Items", "Name", "Label"));

                Assert.That(result.Success, Is.True);
                Assert.That(fixture.Tab.ViewState.Sorts.Single().FieldId,
                    Is.EqualTo("Power"));
                Assert.That(fixture.Tab.ViewState.Columns.Single().FieldId,
                    Is.EqualTo("Label"));
            }
        }

        [Test]
        public void FullDictionarySpec_IsAcceptedWithoutVisualAdapters()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                var dictionary = new GameDBFieldTypeSpec(FieldType.dictionary, false, null,
                    new GameDBDictionaryTypeSpec(KeyType.@string, null,
                        FieldType.@int, null));

                var result = fixture.AddField("Items", "Stats", dictionary);

                Assert.That(result.Success, Is.True, result.CommandResult?.Message);
                var field = result.Snapshot.Tables.Single().Fields.Single();
                Assert.That(field.FieldType, Is.EqualTo(FieldType.dictionary));
                Assert.That(field.DictionaryType.KeyType, Is.EqualTo(KeyType.@string));
                Assert.That(field.DictionaryType.ValueType, Is.EqualTo(FieldType.@int));
            }
        }

        [Test]
        public void DestructiveDenial_CancelsWithoutMutation()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                var before = fixture.Snapshot;
                fixture.Policy.Allow = false;

                var result = fixture.Invoke((revision, documentId) =>
                    fixture.Service.DeleteTable(fixture.Tab, documentId, revision,
                        "Items", before));

                Assert.That(result.Status, Is.EqualTo(GameDBSchemaActionStatus.Cancelled));
                Assert.That(result.CommandResult, Is.Null);
                Assert.That(fixture.Snapshot.Revision, Is.EqualTo(before.Revision));
                Assert.That(fixture.Snapshot.Tables.Single().Name, Is.EqualTo("Items"));
            }
        }

        [Test]
        public void ConfirmationMutation_ReturnsRevisionConflictAndPreservesTarget()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                fixture.Policy.OnConfirm = () => fixture.Tab.Session.ApplyTransaction(
                    new GameDBCommand[]
                    {
                        new SetDatabaseMetadataCommand("ChangedDuringConfirmation", false)
                    });
                var before = fixture.Snapshot;

                var result = fixture.Service.DeleteTable(fixture.Tab,
                    fixture.Tab.Session.DocumentId, before.Revision, "Items", before);

                Assert.That(result.Status, Is.EqualTo(GameDBSchemaActionStatus.Executed));
                Assert.That(result.Success, Is.False);
                Assert.That(result.CommandResult.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.RevisionConflict));
                Assert.That(fixture.Snapshot.ScopeName,
                    Is.EqualTo("ChangedDuringConfirmation"));
                Assert.That(fixture.Snapshot.Tables.Single().Name, Is.EqualTo("Items"));
            }
        }

        [Test]
        public void MissingExpectedRevision_IsRejectedBeforeConfirmation()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);

                Assert.Throws<ArgumentException>(() =>
                    fixture.Service.DeleteTable(fixture.Tab,
                        fixture.Tab.Session.DocumentId, null, "Items", fixture.Snapshot));
                Assert.That(fixture.Policy.Requests, Is.Empty);
            }
        }

        [Test]
        public void TargetIdentityMismatch_DoesNotConfirmOrExecute()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.AddTable("Items").Success, Is.True);
                var before = fixture.Snapshot;

                var result = fixture.Service.DeleteTable(fixture.Tab,
                    "wrong-document", before.Revision, "Items", before);

                Assert.That(result.Status,
                    Is.EqualTo(GameDBSchemaActionStatus.TargetUnavailable));
                Assert.That(fixture.Policy.Requests, Is.Empty);
                Assert.That(fixture.Snapshot.Revision, Is.EqualTo(before.Revision));
            }
        }

        [Test]
        public void DataOnlyEditing_RejectsSchemaCommandsAtCommandLayer()
        {
            using (var fixture = new Fixture(dataOnlyEditing: () => true))
            {
                var result = fixture.AddTable("Items");

                Assert.That(result.Status, Is.EqualTo(GameDBSchemaActionStatus.Executed));
                Assert.That(result.Success, Is.False);
                Assert.That(result.CommandResult.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.OperationNotAllowed));
                Assert.That(fixture.Policy.Requests, Is.Empty);
                Assert.That(fixture.Snapshot.Tables, Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal GameDBEditorWorkspace Workspace { get; }
            internal GameDBEditorWorkspaceTab Tab => Workspace.ActiveTab;
            internal RecordingPolicy Policy { get; }
            internal GameDBSchemaActionService Service { get; }
            internal GameDBEditorLibrary.Automation.GameDBSnapshot Snapshot =>
                Tab.Session.CreateSnapshot();

            internal Fixture(Func<bool> dataOnlyEditing = null)
            {
                var assetPath = $"Assets/GameDBSchemaActionTests/{Guid.NewGuid():N}.json";
                var document = GameDBDocument.CreateNew(assetPath, "SchemaTests", false);
                var store = new MemoryRecoveryStore();
                var recovery = new GameDBWorkspaceRecoveryService(store);
                Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
                {
                    new GameDBWorkspaceRecoveryTab("active", document.CaptureState())
                }, "active")).Success, Is.True);
                Workspace = new GameDBEditorWorkspace(
                    new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                    recovery, new GameDBActiveWorkspaceHub());
                Policy = new RecordingPolicy();
                Service = new GameDBSchemaActionService(Workspace, Policy,
                    dataOnlyEditing);
            }

            internal GameDBSchemaActionResult Invoke(
                Func<string, string, GameDBSchemaActionResult> action)
            {
                var snapshot = Snapshot;
                return action(snapshot.Revision, Tab.Session.DocumentId);
            }

            internal GameDBSchemaActionResult AddTable(string name)
            {
                return Invoke((revision, documentId) => Service.AddTable(Tab,
                    documentId, revision, name, KeyType.@string, null));
            }

            internal GameDBSchemaActionResult AddField(string tableName,
                string fieldName, GameDBFieldTypeSpec typeSpec)
            {
                return Invoke((revision, documentId) => Service.AddField(Tab,
                    documentId, revision, tableName, fieldName, typeSpec));
            }

            internal void AddRow(string tableName, string rowKey)
            {
                var result = Tab.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddRowCommand(tableName, rowKey,
                        new Dictionary<string, object>())
                });
                Assert.That(result.Success, Is.True, result.Message);
            }

            internal void SetFieldViewState(string tableName, string rowKey,
                string fieldName)
            {
                Workspace.TrySetTabViewState(Tab.TabId,
                    new GameDBWorkspaceTabViewState(tableName, rowKey, sorts: new[]
                    {
                        new GameDBWorkspaceSortState(fieldName, true)
                    }, columns: new[]
                    {
                        new GameDBWorkspaceColumnState(fieldName, 140f, 0, tableName)
                    }));
            }

            public void Dispose()
            {
                Workspace.Dispose();
            }
        }

        private sealed class RecordingPolicy : IGameDBEditorDestructiveActionPolicy
        {
            internal bool Allow { get; set; } = true;
            internal List<GameDBDestructiveActionRequest> Requests { get; }
                = new List<GameDBDestructiveActionRequest>();
            internal Action OnConfirm { get; set; }

            public bool Confirm(GameDBDestructiveActionRequest request)
            {
                Requests.Add(request);
                OnConfirm?.Invoke();
                return Allow;
            }
        }

        private sealed class MemoryRecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
            public string QuarantinePrimary()
            {
                m_contents = null;
                return "memory-quarantine";
            }
            public string WriteQuarantine(string label, string contents) =>
                "memory-" + label;
        }
    }
}
