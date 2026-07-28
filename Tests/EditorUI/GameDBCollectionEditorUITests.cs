using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDBLibrary.EditorUITests
{
    [Category("EditorUI")]
    public class GameDBCollectionEditorUITests
        : EditorWindowUITestFixture<GameDBAttachedCollectionTestWindow>
    {
        [Test]
        public void CollectionModal_BlocksFocusesRecyclesCancelsAndAppliesOnAttachedPanel()
        {
            panelSize = new UnityEngine.Vector2(900f, 650f);
            simulate.FrameUpdate();

            var cell = window.CollectionCell;
            Assert.That(cell, Is.Not.Null);
            Assert.That(cell.Q<Label>().text, Is.EqualTo("200 items"));
            cell.Open();
            for (var frame = 0; frame < 6; frame++)
            {
                simulate.FrameUpdate();
            }

            Assert.That(window.ModalHost.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.ModalHost.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(window.CollectionPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.CollectionList.itemsSource.Count, Is.EqualTo(200));
            Assert.That(window.CollectionList.Query<IntegerField>().First(), Is.Not.Null);
            var firstEditor = window.CollectionList.Query<IntegerField>().First();
            Assert.That(firstEditor.tabIndex, Is.GreaterThanOrEqualTo(0));
            var focused = window.CollectionList.panel.focusController.focusedElement
                as VisualElement;
            Assert.That(focused != null && window.CollectionPanel.Contains(focused),
                Is.True, "Focus should remain trapped inside the collection modal.");
            Assert.That(window.ModalHost.Contains(
                window.Root.panel.Pick(window.Grid.worldBound.center)), Is.True);

            var initialRows = RealizedRows(window.CollectionList);
            Assert.That(initialRows.Count, Is.GreaterThan(0));
            Assert.That(initialRows.Count, Is.LessThan(200));
            window.CollectionList.ScrollToItem(199);
            simulate.FrameUpdate();
            var scrolledRows = RealizedRows(window.CollectionList);
            Assert.That(scrolledRows.Count, Is.GreaterThan(0));
            Assert.That(scrolledRows.Count, Is.LessThan(200));
            Assert.That(scrolledRows.Intersect(initialRows), Is.Not.Empty);

            simulate.KeyPress(KeyCode.Escape);
            simulate.FrameUpdate();
            Assert.That(window.ModalHost.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(window.ApplyCount, Is.Zero);
            Assert.That(window.ItemCount, Is.EqualTo(200));

            cell = window.CollectionCell;
            cell.Open();
            simulate.FrameUpdate();
            simulate.Click(window.Root.Q<Button>("collection-add-button"));
            simulate.Click(window.Root.Q<Button>("collection-apply-button"));
            simulate.FrameUpdate();

            Assert.That(window.ApplyCount, Is.EqualTo(1));
            Assert.That(window.ItemCount, Is.EqualTo(201));
            Assert.That(window.CollectionCell.Q<Label>().text, Is.EqualTo("201 items"));
            Assert.That(window.ModalHost.style.display.value, Is.EqualTo(DisplayStyle.None));

            panelSize = new UnityEngine.Vector2(420f, 300f);
            simulate.FrameUpdate();
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.True);
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.True);
        }

        private static HashSet<VisualElement> RealizedRows(ListView list)
        {
            return list.Query<VisualElement>(
                    className: "gamedb-editor__collection-row").ToList().ToHashSet();
        }
    }

    public sealed class GameDBAttachedCollectionTestWindow : EditorWindow
    {
        private GameDBEditorWorkspace m_workspace;
        private GameDBTableViewController m_table;
        private GameDBCollectionEditorController m_collection;
        private GameDBEditorResponsiveLayout m_responsive;
        private GameDBWorkspaceTabViewState m_viewState;

        internal int ApplyCount { get; private set; }
        internal VisualElement Root => rootVisualElement.Q<VisualElement>(
            "gamedb-editor-root");
        internal MultiColumnListView Grid => rootVisualElement.Q<MultiColumnListView>(
            "table-row-grid");
        internal VisualElement ModalHost => rootVisualElement.Q<VisualElement>("modal-host");
        internal VisualElement CollectionPanel => rootVisualElement.Q<VisualElement>(
            "collection-editor-panel");
        internal ListView CollectionList => rootVisualElement.Q<ListView>(
            "collection-editor-list");
        internal GameDBCollectionValueCell CollectionCell => Grid
            .Query<GameDBCollectionValueCell>().ToList().FirstOrDefault(cell =>
                Equals(cell.userData, "Item"));
        internal int ItemCount => ((System.Collections.ICollection)m_workspace.ActiveTab.Session
            .CreateSnapshot().Tables.Single().Rows.Single().Values["Values"]).Count;

        public void CreateGUI()
        {
            GameDBEditorUiAssets.Build(rootVisualElement);
            var document = GameDBDocument.CreateNew(
                $"Assets/GameDBCollectionEditorUITests/{Guid.NewGuid():N}.json",
                "CollectionEditorUI", false);
            var values = Enumerable.Range(0, 200).Cast<object>().ToList();
            var created = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Values",
                    new GameDBFieldTypeSpec(FieldType.@int, true, null)),
                new AddRowCommand("Items", "Item", new Dictionary<string, object>
                {
                    { "Values", values }
                })
            });
            if (!created.Success)
            {
                throw new InvalidOperationException(created.Message);
            }

            var store = new MemoryRecoveryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("collection", document.CaptureState())
            }, "collection"));
            m_workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());

            rootVisualElement.Q<VisualElement>("workspace-state-host")
                .style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("document-shell")
                .style.display = DisplayStyle.Flex;
            m_responsive = new GameDBEditorResponsiveLayout(Root);
            rootVisualElement.Q<VisualElement>("settings-panel").style.display
                = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("modal-host").style.display
                = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("modal-host").pickingMode
                = PickingMode.Ignore;
            m_collection = new GameDBCollectionEditorController(rootVisualElement,
                m_workspace, ApplyAndRebind);
            m_table = new GameDBTableViewController(
                rootVisualElement.Q<ToolbarSearchField>("table-search-field"),
                rootVisualElement.Q<ListView>("table-navigation-list"), Grid,
                rootVisualElement.Q<Label>("active-document-placeholder"),
                state => m_viewState = state, editCollection: OpenCollection);
            m_viewState = new GameDBWorkspaceTabViewState("Items", "Item");
            Rebind();
        }

        private void OpenCollection(GameDBCollectionEditRequest request)
        {
            m_collection.Open(request);
        }

        private void ApplyAndRebind()
        {
            ApplyCount++;
            Rebind();
        }

        private void Rebind()
        {
            m_viewState = m_table.Bind(m_viewState,
                m_workspace.ActiveTab.Session.CreateSnapshot());
        }

        private void OnDisable()
        {
            m_table?.Dispose();
            m_collection?.Dispose();
            m_responsive?.Dispose();
            m_workspace?.Dispose();
            m_table = null;
            m_collection = null;
            m_responsive = null;
            m_workspace = null;
            m_viewState = null;
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
                return "collection-ui-quarantine";
            }
            public string WriteQuarantine(string label, string contents) =>
                "collection-ui-" + label;
        }
    }
}
