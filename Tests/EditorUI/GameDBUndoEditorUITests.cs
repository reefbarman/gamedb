using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary.Tests;
using NUnit.Framework;
using System.Linq;
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

        internal ToolbarButton Undo => rootVisualElement.Q<ToolbarButton>("undo-button");
        internal ToolbarButton Redo => rootVisualElement.Q<ToolbarButton>("redo-button");
        internal Label Summary => rootVisualElement.Q<Label>("active-document-summary-label");
        internal bool HasHistoryTable => m_workspace.ActiveTab.Session.CreateSnapshot()
            .Tables.Any(table => table.Name == "History");

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
