using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorWindowControllerTests
    {
        private const string FirstPath =
            "Assets/GameDBEditorWindowControllerTests/first.json";
        private const string SecondPath =
            "Assets/GameDBEditorWindowControllerTests/second.json";

        [Test]
        public void Controller_BindsActiveDocumentAndInteractiveTabSelection()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                Assert.That(root.Query<ToolbarButton>(className: "gamedb-editor__tab")
                    .ToList(), Has.Count.EqualTo(2));
                Assert.That(root.Q<ToolbarButton>("document-tab-first")
                    .ClassListContains("gamedb-editor__tab--active"), Is.True);
                Assert.That(root.Q<Label>("active-document-path-label").text,
                    Is.EqualTo(FirstPath));
                Assert.That(root.Q<Label>("active-document-summary-label").text,
                    Does.Contain("FirstScope").And.Contain("1 table").And.Contain("Unsaved changes"));
                Assert.That(root.Q<Label>("table-empty-message").text,
                    Is.EqualTo("'Items' has no rows."));
                Assert.That(root.Q<ToolbarButton>("save-button").enabledSelf, Is.True);
                Assert.That(root.Q<ToolbarButton>("reload-button").enabledSelf, Is.False);
                AssertReadOnlyTableView(root);

                Assert.That(controller.ActivateTab("second"), Is.True);

                Assert.That(workspace.ActiveTabId, Is.EqualTo("second"));
                Assert.That(root.Q<ToolbarButton>("document-tab-second")
                    .ClassListContains("gamedb-editor__tab--active"), Is.True);
                Assert.That(root.Q<Label>("active-document-path-label").text,
                    Is.EqualTo(SecondPath));
                Assert.That(root.Q<Label>("active-document-summary-label").text,
                    Does.Contain("SecondScope").And.Contain("2 tables"));
                Assert.That(root.Q<Label>("table-empty-message").text,
                    Is.EqualTo("'Items' has no rows."));
                Assert.That(controller.ActivateTab("missing"), Is.False);
                Assert.That(workspace.ActiveTabId, Is.EqualTo("second"));
                AssertReadOnlyTableView(root);
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_ProjectsUndoRedoDirtyAndRevertState()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                var undo = root.Q<ToolbarButton>("undo-button");
                var redo = root.Q<ToolbarButton>("redo-button");
                var reload = root.Q<ToolbarButton>("reload-button");
                Assert.That(undo.enabledSelf, Is.False,
                    "Recovered documents intentionally start with fresh history.");
                Assert.That(redo.enabledSelf, Is.False);
                Assert.That(reload.text, Is.EqualTo("Revert"));
                Assert.That(reload.enabledSelf, Is.False,
                    "A never-saved draft has no complete disk pair to reload.");

                Assert.That(workspace.ActiveTab.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("History", KeyType.@string, null)
                }).Success, Is.True);

                Assert.That(undo.enabledSelf, Is.True);
                Assert.That(undo.text, Is.EqualTo("Undo Add Table"));
                Assert.That(redo.enabledSelf, Is.False);
                Assert.That(controller.ActivateTab("second"), Is.True);
                Assert.That(undo.enabledSelf, Is.False);
                Assert.That(controller.ActivateTab("first"), Is.True);
                Assert.That(workspace.UndoActiveDocument().Success, Is.True);
                Assert.That(undo.enabledSelf, Is.False);
                Assert.That(redo.enabledSelf, Is.True);
                Assert.That(redo.text, Is.EqualTo("Redo Add Table"));
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_WorkspaceMutationRefreshesOnlyActiveDocumentBinding()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                var firstButton = root.Q<ToolbarButton>("document-tab-first");
                var inactive = workspace.Tabs.Single(tab => tab.TabId == "second");
                Assert.That(inactive.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("InactiveOnly", KeyType.@string, null)
                }).Success, Is.True);

                Assert.That(root.Q<Label>("active-document-summary-label").text,
                    Does.Contain("FirstScope").And.Contain("1 table"));
                Assert.That(workspace.ActiveTab.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("ActiveOnly", KeyType.@string, null)
                }).Success, Is.True);

                Assert.That(root.Q<Label>("active-document-summary-label").text,
                    Does.Contain("FirstScope").And.Contain("2 tables").And.Contain("Unsaved changes"));
                Assert.That(root.Query<ToolbarButton>(className: "gamedb-editor__tab")
                    .ToList(), Has.Count.EqualTo(2));
                Assert.That(root.Q<ToolbarButton>("document-tab-first"),
                    Is.SameAs(firstButton));
                AssertReadOnlyTableView(root);
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_CloseAndMoveActionsUseInjectedPolicyAndPersistOrder()
        {
            var workspace = CreateWorkspace(out var store);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var policy = new RecordingClosePolicy(GameDBTabCloseDecision.Cancel);
            using (var controller = new GameDBEditorWindowController(root, workspace, policy))
            {
                Assert.That(root.Q<ToolbarButton>("document-tab-move-left-first").enabledSelf,
                    Is.False);
                Assert.That(root.Q<ToolbarButton>("document-tab-move-right-first").enabledSelf,
                    Is.True);
                Assert.That(controller.MoveTab("first", 1).Status,
                    Is.EqualTo(GameDBTabReorderStatus.Reordered));
                Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                    Is.EqualTo(new[] { "second", "first" }));
                Assert.That(root.Q<ToolbarButton>("document-tab-move-left-second").enabledSelf,
                    Is.False);

                Assert.That(controller.CloseTab("first").Status,
                    Is.EqualTo(GameDBTabCloseStatus.Cancelled));
                Assert.That(policy.Requests, Has.Count.EqualTo(1));
                Assert.That(policy.Requests[0].Reasons,
                    Is.EqualTo(GameDBTabCloseReason.Dirty));
                Assert.That(policy.Requests[0].CanSave, Is.True);
                policy.Decision = GameDBTabCloseDecision.Discard;
                Assert.That(controller.CloseTab("first").Status,
                    Is.EqualTo(GameDBTabCloseStatus.Closed));
                Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                    Is.EqualTo(new[] { "second" }));
                Assert.That(workspace.ActiveTabId, Is.EqualTo("second"));
                Assert.That(root.Q<VisualElement>("document-tab-container-first"), Is.Null);
                AssertReadOnlyTableView(root);
            }
            workspace.Dispose();

            var restored = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), new GameDBActiveWorkspaceHub());
            Assert.That(restored.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "second" }));
            restored.Dispose();
        }

        [Test]
        public void Workspace_RuntimeBindingBlocksDiskOperationsAndPersistsPrePlayState()
        {
            var workspace = CreateWorkspace(out var store);
            var tab = workspace.ActiveTab;
            var prePlayModeState = tab.Session.CaptureState();
            tab.BeginPlayMode(prePlayModeState, false);
            tab.SetPlayModeBinding(new GameDBPlayModeBinding("runtime-1-1", 1), false);
            Assert.That(tab.Session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("RuntimeOnly", KeyType.@string, null)
            }).Success, Is.True);
            Assert.That(tab.Session.CreateSnapshot().Tables.Select(table => table.Name),
                Does.Contain("RuntimeOnly"));

            Assert.Throws<InvalidOperationException>(() => workspace.SaveActiveDocument());
            Assert.Throws<InvalidOperationException>(() => workspace.ReloadActiveDocument(
                tab.Session.GetState().CurrentRevision, true));
            Assert.That(workspace.CloseTab(tab.TabId,
                    new RecordingClosePolicy(GameDBTabCloseDecision.Discard)).Status,
                Is.EqualTo(GameDBTabCloseStatus.PlayModeBound));
            Assert.That(workspace.PersistRecovery().Success, Is.True);
            workspace.Dispose();

            var restored = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), new GameDBActiveWorkspaceHub());
            Assert.That(restored.ActiveTab.Session.CreateSnapshot().Revision,
                Is.EqualTo(GameDBModelCodec.ComputeRevision(
                    prePlayModeState.SchemaJson, prePlayModeState.DataJson)));
            Assert.That(restored.ActiveTab.Session.CreateSnapshot().Tables
                .Select(table => table.Name), Does.Not.Contain("RuntimeOnly"));
            restored.Dispose();
        }

        [Test]
        public void Workspace_CleanCloseSkipsPolicyAndReleasesLease()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var cleanDocument = GameDBDocument.CreateNew(FirstPath, "CleanScope", false,
                GameDBFilePairStore.Instance, new NoOpPostSaveActions());
            var state = cleanDocument.CaptureState();
            state.BaselineRevision = cleanDocument.CurrentRevision;
            state.WasDirty = false;
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("first", state)
            }, "first")).Success, Is.True);
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var workspace = new GameDBEditorWorkspace(registry, recovery,
                new GameDBActiveWorkspaceHub());
            var session = workspace.ActiveTab.Session;

            var result = workspace.CloseTab("first", null);

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.Closed));
            Assert.That(workspace.Tabs, Is.Empty);
            Assert.That(workspace.ActiveTabId, Is.Null);
            Assert.That(session.IsDisposed, Is.True);
            var replacement = registry.TryAcquire(FirstPath, "replacement");
            Assert.That(replacement.Status,
                Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            replacement.Lease.Dispose();
            workspace.Dispose();
        }

        [Test]
        public void Workspace_CloseRequiresPolicyAndRecoveryFailureKeepsLiveTab()
        {
            var workspace = CreateWorkspace(out var store);
            var first = workspace.Tabs.Single(tab => tab.TabId == "first");
            var session = first.Session;

            Assert.That(workspace.CloseTab("first", null).Status,
                Is.EqualTo(GameDBTabCloseStatus.PolicyRequired));
            store.FailWrites = true;
            var result = workspace.CloseTab("first",
                new RecordingClosePolicy(GameDBTabCloseDecision.Discard));

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.RecoveryFailed));
            Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "first", "second" }));
            Assert.That(workspace.ActiveTabId, Is.EqualTo("first"));
            Assert.That(session.IsDisposed, Is.False);
            Assert.That(workspace.LastTabOperationError,
                Does.Contain("tab recovery write failed"));
            store.FailWrites = false;
            workspace.Dispose();
        }

        [Test]
        public void Workspace_ClosingInactiveTabPreservesActiveTab()
        {
            var workspace = CreateWorkspace(out _);
            var second = workspace.Tabs.Single(tab => tab.TabId == "second").Session;

            var result = workspace.CloseTab("second",
                new RecordingClosePolicy(GameDBTabCloseDecision.Discard));

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.Closed));
            Assert.That(workspace.ActiveTabId, Is.EqualTo("first"));
            Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "first" }));
            Assert.That(second.IsDisposed, Is.True);
            workspace.Dispose();
        }

        [Test]
        public void Workspace_ReorderRecoveryFailureRollsBackAndSuccessRestoresOrder()
        {
            var workspace = CreateWorkspace(out var store);
            store.FailWrites = true;

            var failed = workspace.ReorderTab("first", 1);

            Assert.That(failed.Status, Is.EqualTo(GameDBTabReorderStatus.RecoveryFailed));
            Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "first", "second" }));
            store.FailWrites = false;
            Assert.That(workspace.ReorderTab("first", 1).Status,
                Is.EqualTo(GameDBTabReorderStatus.Reordered));
            workspace.Dispose();

            var restored = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), new GameDBActiveWorkspaceHub());
            Assert.That(restored.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "second", "first" }));
            restored.Dispose();
        }

        [Test]
        public void Workspace_SaveDecisionFailureBlocksCloseAndKeepsSession()
        {
            var store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var state = CreateDocument(FirstPath, "FirstScope", "Items").CaptureState();
            state.PersistenceStateUnknown = true;
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("first", state)
            }, "first")).Success, Is.True);
            var workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
            var session = workspace.ActiveTab.Session;

            var policy = new RecordingClosePolicy(GameDBTabCloseDecision.Save);
            var result = workspace.CloseTab("first", policy);

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.SaveFailed));
            Assert.That(policy.Requests.Single().Reasons,
                Is.EqualTo(GameDBTabCloseReason.Dirty
                    | GameDBTabCloseReason.PersistenceUnknown));
            Assert.That(policy.Requests.Single().CanSave, Is.False);
            Assert.That(result.SaveOutcome, Is.Null);
            Assert.That(workspace.Tabs, Has.Count.EqualTo(1));
            Assert.That(session.IsDisposed, Is.False);
            workspace.Dispose();
        }

        [Test]
        public void Workspace_CloseRejectsStalePolicyDecision()
        {
            var workspace = CreateWorkspace(out _);
            var policy = new MutatingClosePolicy(workspace.ActiveTab.Session);

            var result = workspace.CloseTab("first", policy);

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.StateChanged));
            Assert.That(workspace.Tabs, Has.Count.EqualTo(2));
            Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.True);
            workspace.Dispose();
        }

        [Test]
        public void Workspace_CloseRejectsReentrantTopologyChange()
        {
            var workspace = CreateWorkspace(out _);
            var first = workspace.Tabs.Single(tab => tab.TabId == "first").Session;
            var second = workspace.Tabs.Single(tab => tab.TabId == "second").Session;
            var policy = new ReorderingClosePolicy(workspace);

            var result = workspace.CloseTab("first", policy);

            Assert.That(result.Status, Is.EqualTo(GameDBTabCloseStatus.StateChanged));
            Assert.That(workspace.Tabs.Select(tab => tab.TabId),
                Is.EqualTo(new[] { "second", "first" }));
            Assert.That(first.IsDisposed, Is.False);
            Assert.That(second.IsDisposed, Is.False);
            workspace.Dispose();
        }

        [Test]
        public void Controller_PlayModeBindsRuntimeTargetsAndEnforcesDataOnlyControls()
        {
            var workspace = CreateWorkspace(out _);
            var registry = new GameDBRuntimeRegistry();
            registry.Register(new RuntimeTarget("Runtime Items"));
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);

            using (var controller = new GameDBEditorWindowController(root, workspace,
                runtimeRegistry: registry, isPlaying: () => true))
            {
                Assert.That(root.Q<VisualElement>("play-mode-toolbar").style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<DropdownField>("runtime-target-field").choices,
                    Is.EqualTo(new[] { "Runtime Items" }));
                Assert.That(root.Q<Button>("load-runtime-button").enabledSelf, Is.True);
                Assert.That(root.Q<Button>("reload-in-game-button").enabledSelf, Is.False);
                Assert.That(root.Q<ToolbarButton>("save-button").enabledSelf, Is.False);
                Assert.That(root.Q<ToolbarButton>("reload-button").enabledSelf, Is.False);
                Assert.That(root.Q<ToolbarButton>("generate-button").enabledSelf, Is.False);
                Assert.That(root.Q<ToolbarButton>("build-button").enabledSelf, Is.False);
                Assert.That(root.Q<TextField>("database-scope-field").enabledSelf, Is.False);
                Assert.That(root.Q<Button>("apply-database-metadata-button").enabledSelf,
                    Is.False);
                Assert.That(root.Q<Button>("add-table-button").enabledSelf, Is.False);
                Assert.That(root.Q<Button>("add-field-button").enabledSelf, Is.False);
                Assert.That(root.Q<ToolbarButton>("table-add-row-button").enabledSelf, Is.True);
                var beforeRow = workspace.ActiveTab.Session.CreateSnapshot();
                var row = controller.CreateRow(new GameDBRowCreateIntent(
                    "Items", "RuntimeRow", beforeRow.Revision));
                Assert.That(row.Success, Is.True, row.Message);
                Assert.That(workspace.ActiveTab.Session.CreateSnapshot().Tables
                    .Single(table => table.Name == "Items").Rows.Single().Key,
                    Is.EqualTo("RuntimeRow"));
                Assert.That(root.Q<Label>("play-mode-status-label").text,
                    Does.Contain("Select a runtime GameDB"));
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_LeavingPlayModeRestoresPrePlayWorkingStateAndPolicy()
        {
            var workspace = CreateWorkspace(out _);
            var active = workspace.ActiveTab;
            var prePlayModeState = active.Session.CaptureState();
            active.BeginPlayMode(prePlayModeState, false);
            active.SetPlayModeBinding(new GameDBPlayModeBinding("runtime-1-1", 1), false);
            Assert.That(active.Session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("RuntimeOnly", KeyType.@string, null)
            }).Success, Is.True);
            var playing = true;
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);

            using (var controller = new GameDBEditorWindowController(root, workspace,
                isPlaying: () => playing))
            {
                Assert.That(root.Q<VisualElement>("play-mode-toolbar").style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Button>("add-table-button").enabledSelf, Is.False);

                playing = false;
                controller.Render();

                Assert.That(active.HasPlayModeState, Is.False);
                Assert.That(active.Session.CreateSnapshot().Revision,
                    Is.EqualTo(GameDBModelCodec.ComputeRevision(
                        prePlayModeState.SchemaJson, prePlayModeState.DataJson)));
                Assert.That(active.Session.CreateSnapshot().Tables.Select(table => table.Name),
                    Does.Not.Contain("RuntimeOnly"));
                Assert.That(root.Q<VisualElement>("play-mode-toolbar").style.display.value,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q<Button>("add-table-button").enabledSelf, Is.True);
                var schemaEdit = active.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("EditModeTable", KeyType.@string, null)
                });
                Assert.That(schemaEdit.Success, Is.True, schemaEdit.Message);
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_EditModeHidesRuntimeControlsAndKeepsSchemaEditing()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);

            using (var controller = new GameDBEditorWindowController(root, workspace,
                isPlaying: () => false))
            {
                Assert.That(root.Q<VisualElement>("play-mode-toolbar").style.display.value,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q<TextField>("database-scope-field").enabledSelf, Is.True);
                Assert.That(root.Q<Button>("apply-database-metadata-button").enabledSelf,
                    Is.True);
                Assert.That(root.Q<Button>("add-table-button").enabledSelf, Is.True);
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_DisposeDetachesWorkspaceAndRejectsFurtherIntents()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var controller = new GameDBEditorWindowController(root, workspace);
            var pathBefore = root.Q<Label>("active-document-path-label").text;

            controller.Dispose();
            Assert.That(controller.ActivateTab("second"), Is.False);
            Assert.That(workspace.TryActivateTab("second"), Is.True);

            Assert.That(root.Q<Label>("active-document-path-label").text,
                Is.EqualTo(pathBefore));
            workspace.Dispose();
        }

        [Test]
        public void Controller_ReplacementLeavesOnlyReplacementSubscribed()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var first = new GameDBEditorWindowController(root, workspace);
            first.Dispose();
            var replacement = new GameDBEditorWindowController(root, workspace);

            Assert.That(replacement.ActivateTab("second"), Is.True);

            Assert.That(root.Query<ToolbarButton>(className: "gamedb-editor__tab")
                .ToList(), Has.Count.EqualTo(2));
            Assert.That(root.Q<Label>("active-document-path-label").text,
                Is.EqualTo(SecondPath));
            replacement.Dispose();
            workspace.Dispose();
        }

        [Test]
        public void Workspace_ActivateTabPersistsSelectionAndNotifiesOnlyOnChange()
        {
            var workspace = CreateWorkspace(out var store);
            var notifications = 0;
            workspace.StateChanged += () => notifications++;

            Assert.That(workspace.TryActivateTab("first"), Is.True);
            Assert.That(notifications, Is.Zero);
            Assert.That(workspace.TryActivateTab("missing"), Is.False);
            Assert.That(notifications, Is.Zero);
            Assert.That(workspace.TryActivateTab("second"), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(workspace.PersistRecovery().Success, Is.True);
            workspace.Dispose();

            var restored = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(store), new GameDBActiveWorkspaceHub());
            Assert.That(restored.ActiveTabId, Is.EqualTo("second"));
            restored.Dispose();
        }

        [Test]
        public void AddRowPopover_ValidatesStringKeysCreatesVisibleSelectionAndCancels()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                var session = workspace.ActiveTab.Session;
                Assert.That(session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddRowCommand("Items", "Existing", new Dictionary<string, object>())
                }).Success, Is.True);
                controller.Render();
                var snapshot = session.CreateSnapshot();
                var table = snapshot.Tables.Single(candidate => candidate.Name == "Items");
                var focusTarget = root.Q<ToolbarButton>("table-add-row-button");
                Assert.That(workspace.TrySetTabViewState("first",
                    new GameDBWorkspaceTabViewState("Items", searchText: "missing")), Is.True);

                Assert.That(controller.OpenAddRow(new GameDBAddRowRequest(
                    snapshot, table, snapshot.Revision, focusTarget)), Is.True);
                var layer = root.Q<VisualElement>("popover-layer");
                var confirm = root.Q<Button>("add-row-confirm-button");
                var validation = root.Q<Label>("add-row-validation-message");
                var key = root.Q<VisualElement>("add-row-key-control-host").Q<TextField>();
                Assert.That(layer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(confirm.enabledSelf, Is.False);
                Assert.That(validation.text, Does.Contain("Enter a row key"));

                key.value = " Existing ";
                Assert.That(confirm.enabledSelf, Is.False);
                Assert.That(validation.text, Does.Contain("already exists"));
                key.value = FieldBase.NullRefToken;
                Assert.That(confirm.enabledSelf, Is.False);
                Assert.That(validation.text, Does.Contain("reserved"));
                key.value = " NewRow ";
                Assert.That(confirm.enabledSelf, Is.True);

                controller.SubmitAddRow();

                Assert.That(session.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "Items").Rows.Select(row => row.Key),
                    Does.Contain("NewRow"));
                Assert.That(workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("NewRow"));
                Assert.That(workspace.ActiveTab.ViewState.SearchText, Is.Empty);
                Assert.That(layer.style.display.value, Is.EqualTo(DisplayStyle.None));

                snapshot = session.CreateSnapshot();
                table = snapshot.Tables.Single(candidate => candidate.Name == "Items");
                Assert.That(controller.OpenAddRow(new GameDBAddRowRequest(
                    snapshot, table, snapshot.Revision, focusTarget)), Is.True);
                controller.CancelAddRow();
                Assert.That(layer.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(layer.pickingMode, Is.EqualTo(PickingMode.Ignore));
            }
            workspace.Dispose();
        }

        [Test]
        public void AddRowPopover_UsesDeclaredEnumMembersAndRejectsDuplicates()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                var session = workspace.ActiveTab.Session;
                Assert.That(session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("EnumRows", KeyType.@enum,
                        typeof(AddRowKey).AssemblyQualifiedName),
                    new AddRowCommand("EnumRows", nameof(AddRowKey.Alpha),
                        new Dictionary<string, object>())
                }).Success, Is.True);
                var snapshot = session.CreateSnapshot();
                var table = snapshot.Tables.Single(candidate => candidate.Name == "EnumRows");

                Assert.That(controller.OpenAddRow(new GameDBAddRowRequest(snapshot,
                    table, snapshot.Revision,
                    root.Q<ToolbarButton>("table-add-row-button"))), Is.True);
                var popup = root.Q<VisualElement>("add-row-key-control-host")
                    .Q<PopupField<string>>();
                var confirm = root.Q<Button>("add-row-confirm-button");
                Assert.That(popup.choices,
                    Is.EqualTo(new[] { nameof(AddRowKey.Beta) }));
                Assert.That(popup.value, Is.EqualTo(nameof(AddRowKey.Beta)));
                Assert.That(confirm.enabledSelf, Is.True);
                controller.SubmitAddRow();
                Assert.That(session.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "EnumRows").Rows.Select(row => row.Key),
                    Does.Contain(nameof(AddRowKey.Beta)));
            }
            workspace.Dispose();
        }

        [Test]
        public void AddRowPopover_RevisionAndTabRacesKeepCanonicalDataUnchanged()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace))
            {
                var first = workspace.ActiveTab.Session;
                var snapshot = first.CreateSnapshot();
                var table = snapshot.Tables.Single(candidate => candidate.Name == "Items");
                var request = new GameDBAddRowRequest(snapshot, table, snapshot.Revision,
                    root.Q<ToolbarButton>("table-add-row-button"));
                Assert.That(controller.OpenAddRow(request), Is.True);
                root.Q<VisualElement>("add-row-key-control-host").Q<TextField>().value
                    = "StaleRow";
                Assert.That(first.ApplyTransaction(new GameDBCommand[]
                {
                    new AddRowCommand("Items", "Concurrent", new Dictionary<string, object>())
                }).Success, Is.True);

                controller.SubmitAddRow();

                Assert.That(first.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "Items").Rows.Select(row => row.Key),
                    Does.Contain("Concurrent").And.Not.Contain("StaleRow"));
                Assert.That(root.Q<VisualElement>("popover-layer").style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Label>("add-row-validation-message").text,
                    Does.Contain("changed"));
                controller.SubmitAddRow();
                Assert.That(first.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "Items").Rows.Select(row => row.Key),
                    Does.Contain("StaleRow"));

                controller.CancelAddRow();
                controller.Render();
                snapshot = first.CreateSnapshot();
                table = snapshot.Tables.Single(candidate => candidate.Name == "Items");
                Assert.That(controller.OpenAddRow(new GameDBAddRowRequest(snapshot,
                    table, snapshot.Revision,
                    root.Q<ToolbarButton>("table-add-row-button"))), Is.True);
                root.Q<VisualElement>("add-row-key-control-host").Q<TextField>().value
                    = "WrongTab";
                Assert.That(workspace.TryActivateTab("second"), Is.True);

                Assert.That(root.Q<VisualElement>("popover-layer").style.display.value,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(first.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "Items").Rows.Select(row => row.Key),
                    Does.Not.Contain("WrongTab"));
                Assert.That(workspace.ActiveTab.Session.CreateSnapshot().Tables.Single(candidate =>
                    candidate.Name == "Items").Rows.Select(row => row.Key),
                    Does.Not.Contain("WrongTab"));
            }
            workspace.Dispose();
        }

        [Test]
        public void RowMutations_CreateNoOpRenameRejectStaleAndDeleteNextVisibleRow()
        {
            var workspace = CreateWorkspace(out _);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var policy = new RecordingDestructivePolicy();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                destructiveActionPolicy: policy))
            {
                var session = workspace.ActiveTab.Session;
                var revision = session.CreateSnapshot().Revision;
                var first = controller.CreateRow(new GameDBRowCreateIntent(
                    "Items", "Row10", revision));
                Assert.That(first.Success, Is.True, first.Message);
                var second = controller.CreateRow(new GameDBRowCreateIntent(
                    "Items", "Row2", first.Snapshot.Revision));
                Assert.That(second.Success, Is.True, second.Message);
                Assert.That(workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("Row2"));

                var beforeNoOp = session.CreateSnapshot();
                var noOp = controller.RenameRow(new GameDBRowRenameIntent(
                    "Items", "Row2", " Row2 ", beforeNoOp.Revision));
                Assert.That(noOp.Success, Is.True);
                Assert.That(noOp.CanonicalRowKey, Is.EqualTo("Row2"));
                Assert.That(session.CreateSnapshot().Revision,
                    Is.EqualTo(beforeNoOp.Revision));
                Assert.That(policy.Requests, Is.Empty);

                var staleRevision = beforeNoOp.Revision;
                Assert.That(session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddRowCommand("Items", "Row1", new Dictionary<string, object>())
                }).Success, Is.True);
                var stale = controller.CreateRow(new GameDBRowCreateIntent(
                    "Items", "Ignored", staleRevision));
                Assert.That(stale.Success, Is.False);
                Assert.That(stale.Message, Does.Contain("changed"));

                controller.Render();
                var current = session.CreateSnapshot();
                var deleted = controller.DeleteRow(new GameDBRowDeleteIntent(
                    "Items", "Row2", current.Revision));
                Assert.That(deleted.Success, Is.True, deleted.Message);
                Assert.That(deleted.CanonicalRowKey, Is.EqualTo("Row10"));
                Assert.That(workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("Row10"));
                Assert.That(policy.Requests, Is.Empty);

                var withWhitespace = session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddRowCommand("Items", " A ", new Dictionary<string, object>()),
                    new AddRowCommand("Items", "A", new Dictionary<string, object>())
                });
                Assert.That(withWhitespace.Success, Is.True, withWhitespace.Message);
                controller.Render();
                var whitespaceDelete = controller.DeleteRow(new GameDBRowDeleteIntent(
                    "Items", " A ", session.CreateSnapshot().Revision));
                Assert.That(whitespaceDelete.Success, Is.True, whitespaceDelete.Message);
                Assert.That(session.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Items").Rows.Select(row => row.Key),
                    Does.Contain("A").And.Not.Contain(" A "));
            }
            workspace.Dispose();
        }

        [Test]
        public void RowMutations_ConfirmReferencedRenameAndBlockReferencedDelete()
        {
            var workspace = CreateWorkspace(out _);
            var session = workspace.ActiveTab.Session;
            Assert.That(session.ApplyTransaction(new GameDBCommand[]
            {
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>()),
                new AddTableCommand("Recipes", KeyType.@string, null),
                new AddFieldCommand("Recipes", "Item",
                    new GameDBFieldTypeSpec(FieldType.tableRef, false, "Items")),
                new AddRowCommand("Recipes", "Forge", new Dictionary<string, object>
                {
                    { "Item", "Sword" }
                })
            }).Success, Is.True);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var policy = new RecordingDestructivePolicy { Allow = false };
            using (var controller = new GameDBEditorWindowController(root, workspace,
                destructiveActionPolicy: policy))
            {
                var before = session.CreateSnapshot();
                var cancelled = controller.RenameRow(new GameDBRowRenameIntent(
                    "Items", "Sword", "Blade", before.Revision));
                Assert.That(cancelled.Success, Is.False);
                Assert.That(cancelled.ReferenceImpact.SiteCount, Is.EqualTo(1));
                Assert.That(cancelled.ReferenceImpact.OccurrenceCount, Is.EqualTo(1));
                Assert.That(policy.Requests.Single().Message,
                    Does.Contain("1 reference"));
                Assert.That(session.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Items").Rows.Single().Key, Is.EqualTo("Sword"));

                policy.Allow = true;
                policy.OnConfirm = () => session.ApplyTransaction(new GameDBCommand[]
                {
                    new SetDatabaseMetadataCommand("ChangedDuringRowConfirmation", false)
                });
                var raced = controller.RenameRow(new GameDBRowRenameIntent(
                    "Items", "Sword", "Blade", before.Revision));
                Assert.That(raced.Success, Is.False);
                Assert.That(raced.Message, Does.Contain("revision"));
                Assert.That(session.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Items").Rows.Single().Key, Is.EqualTo("Sword"));

                policy.OnConfirm = null;
                var current = session.CreateSnapshot();
                var renamed = controller.RenameRow(new GameDBRowRenameIntent(
                    "Items", "Sword", "Blade", current.Revision));
                Assert.That(renamed.Success, Is.True, renamed.Message);
                Assert.That(renamed.CanonicalRowKey, Is.EqualTo("Blade"));
                Assert.That(session.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Recipes").Rows.Single().Values["Item"],
                    Is.EqualTo("Blade"));

                var blocked = controller.DeleteRow(new GameDBRowDeleteIntent(
                    "Items", "Blade", renamed.Snapshot.Revision));
                Assert.That(blocked.Success, Is.False);
                Assert.That(blocked.ReferenceImpact.OccurrenceCount, Is.EqualTo(1));
                Assert.That(blocked.Message, Does.Contain("referenced by 1 value"));
                Assert.That(policy.Requests, Has.Count.EqualTo(3),
                    "Delete blocking must not show a confirmation dialog.");
            }
            workspace.Dispose();
        }

        [Test]
        public void RowRename_AbortsWhenConfirmationSwitchesActiveDocument()
        {
            var workspace = CreateWorkspace(out _);
            var first = workspace.ActiveTab.Session;
            Assert.That(first.ApplyTransaction(new GameDBCommand[]
            {
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>()),
                new AddTableCommand("References", KeyType.@string, null),
                new AddFieldCommand("References", "Item",
                    new GameDBFieldTypeSpec(FieldType.tableRef, false, "Items")),
                new AddRowCommand("References", "Use", new Dictionary<string, object>
                {
                    { "Item", "Sword" }
                })
            }).Success, Is.True);
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            var policy = new RecordingDestructivePolicy
            {
                OnConfirm = () => workspace.TryActivateTab("second")
            };
            using (var controller = new GameDBEditorWindowController(root, workspace,
                destructiveActionPolicy: policy))
            {
                var before = first.CreateSnapshot();
                var result = controller.RenameRow(new GameDBRowRenameIntent(
                    "Items", "Sword", "Blade", before.Revision));

                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("active GameDB document changed"));
                Assert.That(workspace.ActiveTabId, Is.EqualTo("second"));
                Assert.That(first.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Items").Rows.Single().Key, Is.EqualTo("Sword"));
            }
            workspace.Dispose();
        }

        private enum AddRowKey
        {
            Alpha,
            Beta
        }

        private static void AssertReadOnlyTableView(VisualElement root)
        {
            Assert.That(root.Q<MultiColumnListView>("table-row-grid"), Is.Not.Null);
            Assert.That(root.Q<ListView>("table-navigation-list"), Is.Not.Null);
            Assert.That(root.Q<TreeView>(), Is.Null);
            Assert.That(root.Q<IMGUIContainer>(), Is.Null);
            Assert.That(root.Q<VisualElement>("inspector-host").childCount,
                Is.GreaterThan(0));
            Assert.That(root.Q<ListView>("field-navigation-list"), Is.Not.Null);
            Assert.That(root.Q<MultiColumnListView>("table-row-grid")
                .Q<TextField>("database-scope-field"), Is.Null);
            Assert.That(root.Q<VisualElement>("table-empty-state"), Is.Not.Null);
            Assert.That(root.Q<Label>("table-empty-message"), Is.Not.Null);
        }

        private static GameDBEditorWorkspace CreateWorkspace(out MemoryStore store)
        {
            store = new MemoryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var first = CreateDocument(FirstPath, "FirstScope", "Items");
            var second = CreateDocument(SecondPath, "SecondScope", "Items", "Recipes");
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("first", first.CaptureState()),
                new GameDBWorkspaceRecoveryTab("second", second.CaptureState())
            }, "first")).Success, Is.True);
            return new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
        }

        private static GameDBDocument CreateDocument(string path, string scope,
            params string[] tables)
        {
            var document = GameDBDocument.CreateNew(path, scope, false,
                GameDBFilePairStore.Instance, new NoOpPostSaveActions());
            var commands = tables.Select(table =>
                (GameDBCommand)new AddTableCommand(table, KeyType.@string, null)).ToArray();
            Assert.That(document.ApplyTransaction(commands).Success, Is.True);
            return document;
        }

        private sealed class MemoryStore : IGameDBWorkspaceRecoveryStore
        {
            internal string Contents { get; private set; }
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
                    throw new InvalidOperationException("tab recovery write failed");
                }
                Contents = contents;
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
        }

        private sealed class RecordingDestructivePolicy
            : IGameDBEditorDestructiveActionPolicy
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

        private sealed class RecordingClosePolicy : IGameDBTabClosePolicy
        {
            internal GameDBTabCloseDecision Decision { get; set; }
            internal System.Collections.Generic.List<GameDBTabCloseRequest> Requests { get; }
                = new System.Collections.Generic.List<GameDBTabCloseRequest>();

            internal RecordingClosePolicy(GameDBTabCloseDecision decision)
            {
                Decision = decision;
            }

            public GameDBTabCloseDecision Decide(GameDBTabCloseRequest request)
            {
                Requests.Add(request);
                return Decision;
            }
        }

        private sealed class ReorderingClosePolicy : IGameDBTabClosePolicy
        {
            private readonly GameDBEditorWorkspace m_workspace;

            internal ReorderingClosePolicy(GameDBEditorWorkspace workspace)
            {
                m_workspace = workspace;
            }

            public GameDBTabCloseDecision Decide(GameDBTabCloseRequest request)
            {
                Assert.That(m_workspace.ReorderTab("first", 1).Status,
                    Is.EqualTo(GameDBTabReorderStatus.Reordered));
                return GameDBTabCloseDecision.Discard;
            }
        }

        private sealed class MutatingClosePolicy : IGameDBTabClosePolicy
        {
            private readonly GameDBAssetSession m_session;

            internal MutatingClosePolicy(GameDBAssetSession session)
            {
                m_session = session;
            }

            public GameDBTabCloseDecision Decide(GameDBTabCloseRequest request)
            {
                Assert.That(m_session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("ChangedWhilePromptOpen", KeyType.@string, null)
                }).Success, Is.True);
                return GameDBTabCloseDecision.Discard;
            }
        }

        private sealed class RuntimeTarget : GameDBBase
        {
            internal RuntimeTarget(string name) : base(name, "Runtime")
            {
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
    }
}
