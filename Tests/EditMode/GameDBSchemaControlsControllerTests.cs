using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBSchemaControlsControllerTests
    {
        [Test]
        public void MetadataAndAddActions_UseCommandsAndSelectCreatedEntities()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Controller.SetDatabaseMetadata(
                    "EditedScope", true).Success, Is.True);
                Assert.That(fixture.Controller.AddTable(
                    "Items", KeyType.@string, null).Success, Is.True);
                Assert.That(fixture.Controller.AddField("Items", "Name",
                    FieldType.@string, null).Success, Is.True);
                Assert.That(fixture.Controller.AddRow("Items", "Sword").Success, Is.True);

                var snapshot = fixture.Workspace.ActiveTab.Session.CreateSnapshot();
                Assert.That(snapshot.ScopeName, Is.EqualTo("EditedScope"));
                Assert.That(snapshot.LocalizationDatabase, Is.True);
                Assert.That(snapshot.Tables.Single().Name, Is.EqualTo("Items"));
                Assert.That(snapshot.Tables.Single().Fields.Single().Name, Is.EqualTo("Name"));
                Assert.That(snapshot.Tables.Single().Rows.Single().Key, Is.EqualTo("Sword"));
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedTableId,
                    Is.EqualTo("Items"));
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("Sword"));
                Assert.That(fixture.Policy.Requests, Is.Empty);
                Assert.That(fixture.RefreshCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void DestructiveActions_RequireExactConfirmationAndRewriteViewIdentities()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.AddTable("Items", KeyType.@string, null);
                fixture.Controller.AddField("Items", "Name", FieldType.@string, null);
                fixture.Controller.AddRow("Items", "Sword");
                fixture.Workspace.TrySetTabViewState(fixture.Workspace.ActiveTab.TabId,
                    new GameDBWorkspaceTabViewState("Items", "Sword", sorts: new[]
                    {
                        new GameDBWorkspaceSortState("Name", true)
                    }, columns: new[]
                    {
                        new GameDBWorkspaceColumnState("Name", 140f, 0, "Items")
                    }));
                fixture.Controller.Bind(fixture.Workspace.ActiveTab,
                    fixture.Workspace.ActiveTab.Session.CreateSnapshot());
                fixture.SimulatePresentationSanitization = true;
                var before = fixture.Workspace.ActiveTab.Session.CreateSnapshot().Revision;

                fixture.Policy.Allow = false;
                Assert.That(fixture.Controller.RenameTable("Items", "Gear"), Is.Null);
                Assert.That(fixture.RefreshCount, Is.EqualTo(3));
                Assert.That(fixture.Workspace.ActiveTab.Session.CreateSnapshot().Revision,
                    Is.EqualTo(before));
                Assert.That(fixture.Policy.Requests.Single().Kind,
                    Is.EqualTo(GameDBCommandKind.RenameTable));

                fixture.Policy.Allow = true;
                Assert.That(fixture.Controller.RenameTable("Items", "Gear").Success, Is.True);
                var tableState = fixture.Workspace.ActiveTab.ViewState;
                Assert.That(tableState.SelectedTableId, Is.EqualTo("Gear"));
                Assert.That(tableState.SelectedRowId, Is.EqualTo("Sword"));
                Assert.That(tableState.Columns.Single().TableId, Is.EqualTo("Gear"));

                Assert.That(fixture.Controller.RenameField(
                    "Gear", "Name", "Label").Success, Is.True);
                var fieldState = fixture.Workspace.ActiveTab.ViewState;
                Assert.That(fieldState.Sorts.Single().FieldId, Is.EqualTo("Label"));
                Assert.That(fieldState.Columns.Single().FieldId, Is.EqualTo("Label"));

                Assert.That(fixture.Controller.RenameRow(
                    "Gear", "Sword", "Blade").Success, Is.True);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("Blade"));
                Assert.That(fixture.Policy.Requests.Select(request => request.Kind),
                    Is.EqualTo(new[]
                    {
                        GameDBCommandKind.RenameTable,
                        GameDBCommandKind.RenameTable,
                        GameDBCommandKind.RenameField,
                        GameDBCommandKind.RenameRow
                    }));
            }
        }

        [Test]
        public void ConfirmationMutation_CausesRevisionConflictAndPreservesTarget()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.AddTable("Items", KeyType.@string, null);
                fixture.Policy.OnConfirm = () => fixture.Workspace.ActiveTab.Session
                    .ApplyTransaction(new GameDBCommand[]
                    {
                        new SetDatabaseMetadataCommand("ChangedDuringConfirmation", false)
                    });

                var result = fixture.Controller.DeleteTable("Items");

                Assert.That(result.Success, Is.False);
                Assert.That(result.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.RevisionConflict));
                var snapshot = fixture.Workspace.ActiveTab.Session.CreateSnapshot();
                Assert.That(snapshot.ScopeName, Is.EqualTo("ChangedDuringConfirmation"));
                Assert.That(snapshot.Tables.Single().Name, Is.EqualTo("Items"));
            }
        }

        [Test]
        public void DeleteAndReplaceActions_ApplyOnlyAfterConfirmationAndPruneState()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.AddTable("Items", KeyType.@string, null);
                fixture.Controller.AddField("Items", "Name", FieldType.@string, null);
                fixture.Controller.AddRow("Items", "Sword");
                fixture.Workspace.TrySetTabViewState(fixture.Workspace.ActiveTab.TabId,
                    new GameDBWorkspaceTabViewState("Items", "Sword", sorts: new[]
                    {
                        new GameDBWorkspaceSortState("Name", false)
                    }, columns: new[]
                    {
                        new GameDBWorkspaceColumnState("Name", 150f, 0, "Items")
                    }));
                fixture.Controller.Bind(fixture.Workspace.ActiveTab,
                    fixture.Workspace.ActiveTab.Session.CreateSnapshot());

                Assert.That(fixture.Controller.ReplaceField(
                    "Items", "Name", FieldType.@int, null).Success, Is.True);
                var replaced = fixture.Workspace.ActiveTab.Session.CreateSnapshot()
                    .Tables.Single().Fields.Single();
                Assert.That(replaced.FieldType, Is.EqualTo(FieldType.@int));

                Assert.That(fixture.Controller.DeleteRow("Items", "Sword").Success, Is.True);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedRowId, Is.Null);
                Assert.That(fixture.Controller.DeleteField("Items", "Name").Success, Is.True);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.Sorts, Is.Empty);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.Columns, Is.Empty);
                Assert.That(fixture.Controller.DeleteTable("Items").Success, Is.True);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedTableId, Is.Null);
                Assert.That(fixture.Workspace.ActiveTab.Session.CreateSnapshot().Tables,
                    Is.Empty);
            }
        }

        [Test]
        public void ScalarBoundaryErrorsAndDisposePreserveCanonicalState()
        {
            using (var fixture = new Fixture())
            {
                var fieldTypes = fixture.Root.Q<DropdownField>("field-type-field");
                Assert.That(fieldTypes.choices,
                    Does.Not.Contain(FieldType.dictionary.ToString()));
                fieldTypes.index = -1;
                Assert.That(fixture.Root.Q<Button>("add-field-button").enabledSelf,
                    Is.False);
                Assert.That(fixture.Root.Q<Button>("replace-field-button").enabledSelf,
                    Is.False);
                Assert.Throws<ArgumentException>(() => fixture.Controller.AddField(
                    "Items", "Map", FieldType.dictionary, null));

                var before = fixture.Workspace.ActiveTab.Session.CreateSnapshot();
                var invalid = fixture.Controller.SetDatabaseMetadata(string.Empty, false);
                Assert.That(invalid.Success, Is.False);
                Assert.That(fixture.Workspace.ActiveTab.Session.CreateSnapshot().Revision,
                    Is.EqualTo(before.Revision));
                Assert.That(fixture.Root.Q<VisualElement>("editor-action-message-host")
                    .Q<HelpBox>(), Is.Not.Null);

                fixture.Controller.Dispose();
                Assert.That(fixture.Controller.AddTable(
                    "Ignored", KeyType.@string, null), Is.Null);
                Assert.That(fixture.Workspace.ActiveTab.Session.CreateSnapshot().Tables,
                    Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal VisualElement Root { get; }
            internal GameDBEditorWorkspace Workspace { get; }
            internal RecordingPolicy Policy { get; }
            internal GameDBSchemaControlsController Controller { get; }
            internal int RefreshCount { get; private set; }
            internal bool SimulatePresentationSanitization { get; set; }

            internal Fixture()
            {
                var assetPath = $"Assets/GameDBSchemaControlsTests/{Guid.NewGuid():N}.json";
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
                Workspace.StateChanged += SanitizePresentationState;
                Root = new VisualElement();
                GameDBEditorUiAssets.Build(Root);
                Policy = new RecordingPolicy();
                Controller = new GameDBSchemaControlsController(Root, Workspace, Policy,
                    () => RefreshCount++);
                Controller.Bind(Workspace.ActiveTab,
                    Workspace.ActiveTab.Session.CreateSnapshot());
            }

            private void SanitizePresentationState()
            {
                if (!SimulatePresentationSanitization || Workspace.ActiveTab == null)
                {
                    return;
                }
                var tab = Workspace.ActiveTab;
                var snapshot = tab.Session.CreateSnapshot();
                var table = snapshot.Tables.FirstOrDefault(candidate =>
                    candidate.Name == tab.ViewState.SelectedTableId)
                    ?? snapshot.Tables.FirstOrDefault();
                var validFields = table?.Fields.Select(field => field.Name).ToHashSet()
                    ?? new HashSet<string>();
                var selectedRow = table?.Rows.Any(row =>
                    row.Key == tab.ViewState.SelectedRowId) == true
                    ? tab.ViewState.SelectedRowId : null;
                tab.SetViewState(new GameDBWorkspaceTabViewState(table?.Name,
                    selectedRow, tab.ViewState.SearchText,
                    tab.ViewState.Sorts.Where(sort => validFields.Contains(sort.FieldId)),
                    tab.ViewState.Columns.Where(column => column.TableId == table?.Name
                        && (column.FieldId == GameDBTableViewProjection.KeyFieldId
                            || validFields.Contains(column.FieldId))),
                    tab.ViewState.HorizontalScroll, tab.ViewState.VerticalScroll), false);
            }

            public void Dispose()
            {
                Workspace.StateChanged -= SanitizePresentationState;
                Controller.Dispose();
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
