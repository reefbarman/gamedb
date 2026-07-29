using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary.Tests;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.UIElements.TestFramework;
using UnityEngine.UIElements;

namespace GameDBLibrary.EditorUITests
{
    [Category("EditorUI")]
    public class GameDBUndoEditorUITests
        : EditorWindowUITestFixture<GameDBAttachedUndoTestWindow>
    {
        [Test]
        public void ConsecutiveVisibleCellEdits_RebindCanonicalRevisionAndWindowState()
        {
            simulate.FrameUpdate();

            var first = window.FindTextCell("Row0000", "Field00");
            Assert.That(first, Is.Not.Null);
            var firstField = (TextField)first.Control;
            firstField.Focus();
            firstField.SelectAll();
            simulate.TypingText("First committed value");
            window.Grid.Focus();
            simulate.FrameUpdate();

            Assert.That(window.Value("Row0000", "Field00"),
                Is.EqualTo("First committed value"));
            Assert.That(window.Undo.text, Is.EqualTo("Undo Set Value"));
            Assert.That(window.Summary.text, Does.Contain("Unsaved changes"));

            var second = window.FindTextCell("Row0001", "Field00");
            Assert.That(second, Is.Not.Null);
            var secondField = (TextField)second.Control;
            secondField.Focus();
            secondField.SelectAll();
            simulate.TypingText("Second committed value");
            window.Grid.Focus();
            simulate.FrameUpdate();

            Assert.That(window.Value("Row0001", "Field00"),
                Is.EqualTo("Second committed value"));
            Assert.That(window.FindTextCell("Row0001", "Field00")
                .ClassListContains("gamedb-editor__value-editor--invalid"), Is.False);
        }

        [Test]
        public void AddRowPopover_OpensSubmitsDismissesRestoresFocusAndClampsOnAttachedPanel()
        {
            panelSize = new UnityEngine.Vector2(900f, 600f);
            simulate.FrameUpdate();

            simulate.Click(window.AddRow);
            simulate.FrameUpdate();
            Assert.That(window.PopoverLayer.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.AddRowPopover.resolvedStyle.visibility,
                Is.EqualTo(Visibility.Visible));
            Assert.That(window.AddRowPopover.worldBound.xMin,
                Is.GreaterThanOrEqualTo(window.Root.worldBound.xMin - 1f));
            Assert.That(window.AddRowPopover.worldBound.xMax,
                Is.LessThanOrEqualTo(window.Root.worldBound.xMax + 1f));
            Assert.That(window.AddRowKey.hasFocusPseudoState, Is.True);

            simulate.TypingText("AttachedRow");
            simulate.KeyPress(KeyCode.Return);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.That(window.HasRow("AttachedRow"), Is.True);
            Assert.That(window.PopoverLayer.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(window.AddRow.hasFocusPseudoState, Is.True);

            simulate.Click(window.AddRow);
            simulate.FrameUpdate();
            simulate.KeyPress(KeyCode.Escape);
            simulate.FrameUpdate();
            Assert.That(window.PopoverLayer.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(window.AddRow.hasFocusPseudoState, Is.True);

            simulate.Click(window.AddRow);
            simulate.FrameUpdate();
            simulate.Click(window.TableNavigation);
            simulate.FrameUpdate();
            Assert.That(window.PopoverLayer.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));

            panelSize = new UnityEngine.Vector2(380f, 260f);
            simulate.Click(window.AddRow);
            simulate.FrameUpdate();
            Assert.That(window.AddRowPopover.worldBound.xMax,
                Is.LessThanOrEqualTo(window.Root.worldBound.xMax + 1f));
            Assert.That(window.AddRowPopover.worldBound.yMax,
                Is.LessThanOrEqualTo(window.Root.worldBound.yMax + 1f));
        }

        [Test]
        public void Toolbar_UndoRedoTracksAttachedDocumentState()
        {
            simulate.FrameUpdate();
            Assert.That(window.Undo.enabledSelf, Is.False);
            Assert.That(window.Redo.enabledSelf, Is.False);

            window.AddHistoryTable();
            simulate.FrameUpdate();
            Assert.That(window.Undo.enabledSelf, Is.True);
            Assert.That(window.Undo.text, Is.EqualTo("Undo Add Table"));
            Assert.That(window.Redo.enabledSelf, Is.False);
            Assert.That(window.Summary.text, Does.Contain("Unsaved changes"));

            simulate.Click(window.Undo);
            simulate.FrameUpdate();
            Assert.That(window.HasHistoryTable, Is.False);
            Assert.That(window.Undo.enabledSelf, Is.False);
            Assert.That(window.Redo.enabledSelf, Is.True);
            Assert.That(window.Redo.text, Is.EqualTo("Redo Add Table"));

            simulate.Click(window.Redo);
            simulate.FrameUpdate();
            Assert.That(window.HasHistoryTable, Is.True);
            Assert.That(window.Undo.enabledSelf, Is.True);
            Assert.That(window.Redo.enabledSelf, Is.False);
        }
    }

    public sealed class GameDBAttachedUndoTestWindow : EditorWindow
    {
        private GameDBEditorWindowController m_controller;
        private GameDBEditorWorkspace m_workspace;

        internal VisualElement Root => rootVisualElement.Q<VisualElement>("gamedb-editor-root");
        internal ToolbarButton AddRow => rootVisualElement.Q<ToolbarButton>(
            "table-add-row-button");
        internal VisualElement PopoverLayer => rootVisualElement.Q<VisualElement>(
            "popover-layer");
        internal VisualElement AddRowPopover => rootVisualElement.Q<VisualElement>(
            "add-row-popover");
        internal TextField AddRowKey => rootVisualElement.Q<VisualElement>(
            "add-row-key-control-host").Q<TextField>();
        internal ListView TableNavigation => rootVisualElement.Q<ListView>(
            "table-navigation-list");
        internal ToolbarButton Undo => rootVisualElement.Q<ToolbarButton>("undo-button");
        internal ToolbarButton Redo => rootVisualElement.Q<ToolbarButton>("redo-button");
        internal Label Summary => rootVisualElement.Q<Label>("active-document-summary-label");
        internal MultiColumnListView Grid => rootVisualElement.Q<MultiColumnListView>(
            "table-row-grid");
        internal bool HasHistoryTable => m_workspace.ActiveTab.Session.CreateSnapshot()
            .Tables.Any(table => table.Name == "History");
        internal bool HasRow(string rowKey) => m_workspace.ActiveTab.Session.CreateSnapshot()
            .Tables.Single(table => table.Name == "Table00").Rows
            .Any(row => row.Key == rowKey);

        internal GameDBValueEditorCell FindTextCell(string rowKey, string fieldName)
        {
            return Grid.Query<GameDBValueEditorCell>().ToList().FirstOrDefault(cell =>
                Equals(cell.userData, rowKey) && cell.FieldName == fieldName
                && cell.Control is TextField);
        }

        internal object Value(string rowKey, string fieldName)
        {
            return m_workspace.ActiveTab.Session.CreateSnapshot().Tables
                .Single(table => table.Name == "Table00").Rows
                .Single(row => row.Key == rowKey).Values[fieldName];
        }

        internal void AddHistoryTable()
        {
            var result = m_workspace.ActiveTab.Session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("History", GameDBLibrary.KeyType.@string, null)
            });
            if (!result.Success)
            {
                throw new AssertionException(result.Message);
            }
        }

        public void CreateGUI()
        {
            GameDBEditorUiAssets.Build(rootVisualElement);
            var document = (GameDBDocument)GameDBRepresentativeFixture
                .CreateDocumentForEditorUiTests(documentId: "undo-editor-ui");
            var store = new MemoryRecoveryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("undo", document.CaptureState())
            }, "undo"));
            m_workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
            m_controller = new GameDBEditorWindowController(rootVisualElement, m_workspace);
        }

        private void OnDisable()
        {
            m_controller?.Dispose();
            m_workspace?.Dispose();
            m_controller = null;
            m_workspace = null;
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
                return "quarantine.json";
            }
            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }
        }
    }
}
