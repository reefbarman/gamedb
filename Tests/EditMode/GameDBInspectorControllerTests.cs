using GameDBEditorLibrary.Automation;
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
    public class GameDBInspectorControllerTests
    {
        [Test]
        public void Bind_ProjectsCanonicalTableAndFieldContextsWithoutImplicitFieldSelection()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Root.Q<Label>("inspector-title-label").text,
                    Is.EqualTo("Items"));
                Assert.That(fixture.Root.Q<Label>("inspector-table-summary").text,
                    Does.Contain("1 fields").And.Contain("1 rows"));
                Assert.That(fixture.Controller.Context.Kind,
                    Is.EqualTo(GameDBInspectorContextKind.Table));

                fixture.Root.Q<ListView>("field-navigation-list").SetSelection(0);

                Assert.That(fixture.Controller.Context.Kind,
                    Is.EqualTo(GameDBInspectorContextKind.Field));
                Assert.That(fixture.Root.Q<Label>("inspector-title-label").text,
                    Is.EqualTo("Name"));
                Assert.That(fixture.Root.Q<Label>("inspector-field-type-label").text,
                    Is.EqualTo("String"));
            }
        }

        [Test]
        public void CreateFieldTask_AddsScalarFieldAndReturnsToFieldContext()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.RequestCreateField();
                var name = fixture.Root.Q<TextField>("inspector-task-name-field");
                Assert.That(name, Is.Not.Null);
                name.value = "Power";

                fixture.Controller.SubmitActiveTask();

                var snapshot = fixture.Workspace.ActiveTab.Session.CreateSnapshot();
                Assert.That(snapshot.Tables.Single().Fields.Select(field => field.Name),
                    Is.EqualTo(new[] { "Name", "Power" }));
                Assert.That(fixture.Controller.Context.Kind,
                    Is.EqualTo(GameDBInspectorContextKind.Field));
                Assert.That(fixture.Controller.Context.FieldName, Is.EqualTo("Power"));
            }
        }

        [Test]
        public void DirtyTask_BlocksTableSelectionUntilDiscarded()
        {
            using (var fixture = new Fixture(includeSecondTable: true))
            {
                fixture.Controller.RequestCreateField();
                fixture.Root.Q<TextField>("inspector-task-name-field").value = "Draft";

                Assert.That(fixture.Controller.RequestTableSelection("Recipes"), Is.False);
                Assert.That(fixture.Root.Q<VisualElement>("inspector-navigation-decision")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedTableId,
                    Is.EqualTo("Items"));

                fixture.Controller.DiscardPendingNavigation();

                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedTableId,
                    Is.EqualTo("Recipes"));
                Assert.That(fixture.Controller.HasDirtyTask, Is.False);
            }
        }

        [Test]
        public void Bind_UsesWorkspaceSelectionInsteadOfPreviousInspectorContext()
        {
            using (var fixture = new Fixture(includeSecondTable: true))
            {
                fixture.Root.Q<ListView>("field-navigation-list").SetSelection(0);
                Assert.That(fixture.Controller.Context.Kind,
                    Is.EqualTo(GameDBInspectorContextKind.Field));

                fixture.Workspace.TrySetTabViewState(fixture.Workspace.ActiveTab.TabId,
                    new GameDBWorkspaceTabViewState("Recipes"));
                fixture.Controller.Bind(fixture.Workspace.ActiveTab,
                    fixture.Workspace.ActiveTab.Session.CreateSnapshot());

                Assert.That(fixture.Controller.Context.Kind,
                    Is.EqualTo(GameDBInspectorContextKind.Table));
                Assert.That(fixture.Controller.Context.TableName, Is.EqualTo("Recipes"));
                Assert.That(fixture.Root.Q<Label>("inspector-title-label").text,
                    Is.EqualTo("Recipes"));
            }
        }

        [Test]
        public void CurrentTableReselection_DoesNotCancelActiveTask()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.RequestCreateField();
                Assert.That(fixture.Controller.RequestTableSelection("Items"), Is.True);
                Assert.That(fixture.Root.Q<TextField>("inspector-task-name-field"),
                    Is.Not.Null);
            }
        }

        [Test]
        public void InvalidFieldIntent_DoesNotReplaceDirtyTaskOrOpenDecision()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.RequestCreateField();
                fixture.Root.Q<TextField>("inspector-task-name-field").value = "Draft";

                fixture.Controller.RequestInspectField("Items", "Missing");

                Assert.That(fixture.Controller.HasDirtyTask, Is.True);
                Assert.That(fixture.Root.Q<VisualElement>("inspector-navigation-decision")
                    .style.display.value, Is.EqualTo(DisplayStyle.None));
            }
        }

        [Test]
        public void DirtyTask_DefersWindowActionUntilDiscarded()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controller.RequestCreateField();
                fixture.Root.Q<TextField>("inspector-task-name-field").value = "Draft";
                var continued = false;

                Assert.That(fixture.Controller.RequestWindowAction(
                    GameDBInspectorPendingIntentKind.OpenSettingsOrModal,
                    "Finish the Inspector task first.", () => continued = true), Is.False);
                Assert.That(continued, Is.False);
                Assert.That(fixture.Root.Q<VisualElement>("inspector-navigation-decision")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));

                fixture.Controller.DiscardPendingNavigation();

                Assert.That(continued, Is.True);
                Assert.That(fixture.Controller.HasDirtyTask, Is.False);
            }
        }

        [Test]
        public void CancellingTaskFooterAfterDecisionDoesNotLeakContinuation()
        {
            using (var fixture = new Fixture(includeSecondTable: true))
            {
                fixture.Controller.RequestCreateField();
                fixture.Root.Q<TextField>("inspector-task-name-field").value = "Draft";
                var continued = 0;
                fixture.Controller.RequestWindowAction(
                    GameDBInspectorPendingIntentKind.CloseInspector,
                    "Finish the Inspector task first.", () => continued++);

                fixture.Controller.CancelActiveTask();
                fixture.Controller.RequestCreateField();
                fixture.Root.Q<TextField>("inspector-task-name-field").value = "Other";
                Assert.That(fixture.Controller.RequestTableSelection("Recipes"), Is.False);
                fixture.Controller.DiscardPendingNavigation();

                Assert.That(continued, Is.Zero);
                Assert.That(fixture.Workspace.ActiveTab.ViewState.SelectedTableId,
                    Is.EqualTo("Recipes"));
            }
        }

        [Test]
        public void DisposedController_IgnoresTaskEntryPoints()
        {
            var fixture = new Fixture();
            fixture.Controller.RequestCreateField();
            fixture.Controller.Dispose();

            Assert.DoesNotThrow(() => fixture.Controller.SubmitActiveTask());
            Assert.That(fixture.Controller.RequestTableSelection("Items"), Is.False);
            fixture.Workspace.Dispose();
        }

        [Test]
        public void RequestInspectField_OpensExactFieldAndEnsuresInspectorVisible()
        {
            using (var fixture = new Fixture())
            {
                VisualElement focusTarget = null;
                fixture.EnsureOpen = target => focusTarget = target;
                fixture.Controller.RequestInspectField("Items", "Name");

                Assert.That(fixture.Controller.Context.FieldName, Is.EqualTo("Name"));
                Assert.That(focusTarget, Is.Not.Null);
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal VisualElement Root { get; }
            internal GameDBEditorWorkspace Workspace { get; }
            internal GameDBInspectorController Controller { get; }
            internal Action<VisualElement> EnsureOpen { set; private get; }

            internal Fixture(bool includeSecondTable = false)
            {
                var assetPath = $"Assets/GameDBInspectorControllerTests/{Guid.NewGuid():N}.json";
                var document = GameDBDocument.CreateNew(assetPath, "InspectorTests", false);
                var store = new MemoryRecoveryStore();
                var recovery = new GameDBWorkspaceRecoveryService(store);
                Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
                {
                    new GameDBWorkspaceRecoveryTab("active", document.CaptureState())
                }, "active")).Success, Is.True);
                Workspace = new GameDBEditorWorkspace(
                    new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                    recovery, new GameDBActiveWorkspaceHub());
                var commands = new List<GameDBCommand>
                {
                    new AddTableCommand("Items", KeyType.@string, null),
                    new AddFieldCommand("Items", "Name",
                        new GameDBFieldTypeSpec(FieldType.@string, false, null)),
                    new AddRowCommand("Items", "Sword",
                        new Dictionary<string, object> { { "Name", "Iron" } })
                };
                if (includeSecondTable)
                {
                    commands.Add(new AddTableCommand("Recipes", KeyType.@string, null));
                }
                Assert.That(Workspace.ActiveTab.Session.ApplyTransaction(commands).Success,
                    Is.True);
                Workspace.TrySetTabViewState(Workspace.ActiveTab.TabId,
                    new GameDBWorkspaceTabViewState("Items"));
                Root = new VisualElement();
                GameDBEditorUiAssets.Build(Root);
                Controller = new GameDBInspectorController(Root, Workspace,
                    importedEnumTypes: () => Array.Empty<string>(),
                    refreshPresentation: Refresh,
                    ensureOpen: target => EnsureOpen?.Invoke(target));
                Controller.Bind(Workspace.ActiveTab,
                    Workspace.ActiveTab.Session.CreateSnapshot());
            }

            private void Refresh()
            {
                Controller.Bind(Workspace.ActiveTab,
                    Workspace.ActiveTab.Session.CreateSnapshot());
            }

            public void Dispose()
            {
                Controller.Dispose();
                Workspace.Dispose();
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
