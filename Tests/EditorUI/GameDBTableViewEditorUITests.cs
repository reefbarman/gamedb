using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary.Tests;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDBLibrary.EditorUITests
{
    [Category("EditorUI")]
    public class GameDBTableViewEditorUITests
        : EditorWindowUITestFixture<GameDBAttachedTableTestWindow>
    {
        [Test]
        public void TypedCell_CommitsRebindsCancelsRejectsAndRecyclesOnAttachedPanel()
        {
            panelSize = new UnityEngine.Vector2(1200f, 700f);
            window.Grid.ScrollToItem(0);
            simulate.FrameUpdate();

            var cell = window.FindTextCell("Row0000");
            Assert.That(cell, Is.Not.Null);
            var field = (TextField)cell.Control;
            field.Focus();
            simulate.FrameUpdate();
            Assert.That(field.hasFocusPseudoState, Is.True);

            var original = field.value;
            var canonical = "Committed value";
            simulate.TypingText(canonical);
            simulate.FrameUpdate();
            Assert.That(field.text, Is.EqualTo(canonical));
            Assert.That(window.CommitCount, Is.Zero);
            Assert.That(window.Value("Row0000", "Field00"), Is.EqualTo(original));

            window.Grid.Focus();
            simulate.FrameUpdate();
            Assert.That(window.CommitCount, Is.EqualTo(1));
            Assert.That(window.Value("Row0000", "Field00"), Is.EqualTo(canonical));
            Assert.That(((TextField)window.FindTextCell("Row0000").Control).value,
                Is.EqualTo(canonical));

            cell = window.FindTextCell("Row0000");
            field = (TextField)cell.Control;
            field.Focus();
            simulate.TypingText("Draft value");
            simulate.FrameUpdate();
            Assert.That(field.text, Is.EqualTo("Draft value"));
            Assert.That(window.CommitCount, Is.EqualTo(1));
            simulate.KeyPress(KeyCode.Escape);
            simulate.FrameUpdate();
            Assert.That(field.text, Is.EqualTo(canonical));
            Assert.That(window.CommitCount, Is.EqualTo(1));

            field.SelectAll();
            simulate.TypingText(GameDBAttachedTableTestWindow.RejectedValue);
            window.Grid.Focus();
            simulate.FrameUpdate();
            cell = window.FindTextCell("Row0000");
            field = (TextField)cell.Control;
            Assert.That(field.value, Is.EqualTo(canonical));
            Assert.That(cell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.True);
            Assert.That(cell.tooltip, Is.EqualTo("Rejected by attached test."));
            Assert.That(window.CommitCount, Is.EqualTo(2));
            var rejectedCell = cell;

            window.Grid.ScrollToItem(GameDBRepresentativeFixture.DefaultRowsPerTable - 1);
            simulate.FrameUpdate();
            Assert.That(window.Grid.Query<GameDBValueEditorCell>().ToList()
                .Any(realized => Equals(realized.userData, "Row0299")), Is.True);
            Assert.That(rejectedCell.userData, Is.Not.EqualTo("Row0000"));
            Assert.That(rejectedCell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.False);
            Assert.That(rejectedCell.tooltip, Is.Empty);
        }

        [Test]
        public void RepresentativeGrid_HandlesResizeFocusNavigationAndRecycling()
        {
            panelSize = new UnityEngine.Vector2(1200f, 700f);
            simulate.FrameUpdate();

            AssertRepresentativeGeometry(1200f, 700f);
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.False);
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.False);
            Assert.That(window.Grid.itemsSource.Count,
                Is.EqualTo(GameDBRepresentativeFixture.DefaultRowsPerTable));
            Assert.That(window.Grid.columns.Count,
                Is.EqualTo(GameDBRepresentativeFixture.DefaultFieldsPerTable + 1));

            window.Grid.SetSelection(0);
            window.Grid.Focus();
            simulate.FrameUpdate();
            Assert.That(window.Grid.panel.focusController.focusedElement,
                Is.SameAs(window.Grid));

            simulate.KeyPress(KeyCode.DownArrow);
            simulate.FrameUpdate();
            Assert.That(window.Grid.selectedIndex, Is.EqualTo(1));
            Assert.That(window.Grid.panel.focusController.focusedElement,
                Is.SameAs(window.Grid));

            var initialRows = RealizedRows(window.Grid);
            Assert.That(initialRows.Count, Is.GreaterThan(0));
            Assert.That(initialRows.Count,
                Is.LessThan(GameDBRepresentativeFixture.DefaultRowsPerTable));

            window.Grid.ScrollToItem(GameDBRepresentativeFixture.DefaultRowsPerTable - 1);
            simulate.FrameUpdate();
            var scrolledRows = RealizedRows(window.Grid);
            Assert.That(scrolledRows.Count, Is.GreaterThan(0));
            Assert.That(scrolledRows.Count,
                Is.LessThan(GameDBRepresentativeFixture.DefaultRowsPerTable));
            Assert.That(scrolledRows.Intersect(initialRows), Is.Not.Empty,
                "Scrolling should recycle attached row containers instead of realizing all rows.");
            Assert.That(window.Grid.Query<Label>(className: "gamedb-editor__table-cell")
                .ToList().Any(label => Equals(label.userData, "Row0299")), Is.True);

            panelSize = new UnityEngine.Vector2(420f, 280f);
            simulate.FrameUpdate();
            AssertRepresentativeGeometry(420f, 280f);
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.True);
            Assert.That(window.Root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.True);
        }

        private void AssertRepresentativeGeometry(float expectedWidth,
            float expectedHeight)
        {
            Assert.That(window.Root.panel, Is.Not.Null);
            Assert.That(window.Root.worldBound.width, Is.EqualTo(expectedWidth).Within(2f));
            Assert.That(window.Root.worldBound.height, Is.EqualTo(expectedHeight).Within(2f));
            Assert.That(window.Grid.worldBound.width, Is.GreaterThan(0f));
            Assert.That(window.Grid.worldBound.height, Is.GreaterThan(0f));
            Assert.That(window.Grid.worldBound.xMin,
                Is.GreaterThanOrEqualTo(window.Surface.worldBound.xMin - 1f));
            Assert.That(window.Grid.worldBound.xMax,
                Is.LessThanOrEqualTo(window.Surface.worldBound.xMax + 1f));
            Assert.That(window.Grid.worldBound.yMax,
                Is.LessThanOrEqualTo(window.Surface.worldBound.yMax + 1f));
        }

        private static HashSet<VisualElement> RealizedRows(MultiColumnListView grid)
        {
            var rows = new HashSet<VisualElement>();
            foreach (var cell in grid.Query<Label>(
                className: "gamedb-editor__table-cell").ToList())
            {
                var row = cell.parent;
                while (row != null && row.parent != grid.contentContainer
                    && row.parent != grid)
                {
                    row = row.parent;
                }
                if (row != null && row != grid)
                {
                    rows.Add(row);
                }
            }
            return rows;
        }
    }

    public sealed class GameDBAttachedTableTestWindow : EditorWindow
    {
        internal const string RejectedValue = "reject-attached-value";
        private GameDBTableViewController m_controller;
        private GameDBEditorResponsiveLayout m_responsiveLayout;
        private GameDBAssetSession m_session;
        private GameDBWorkspaceTabViewState m_viewState;

        internal int CommitCount { get; private set; }

        internal VisualElement Root => rootVisualElement.Q<VisualElement>(
            "gamedb-editor-root");
        internal VisualElement Surface => rootVisualElement.Q<VisualElement>(
            "table-surface-host");
        internal MultiColumnListView Grid => rootVisualElement.Q<MultiColumnListView>(
            "table-row-grid");

        internal GameDBValueEditorCell FindTextCell(string rowKey)
        {
            return Grid.Query<GameDBValueEditorCell>().ToList().FirstOrDefault(cell =>
                Equals(cell.userData, rowKey) && cell.Control is TextField);
        }

        internal object Value(string rowKey, string fieldName)
        {
            return m_session.CreateSnapshot().Tables.Single(table => table.Name == "Table00")
                .Rows.Single(row => row.Key == rowKey).Values[fieldName];
        }

        public void CreateGUI()
        {
            GameDBEditorUiAssets.Build(rootVisualElement);
            var document = (GameDBDocument)GameDBRepresentativeFixture
                .CreateDocumentForEditorUiTests(documentId: "editor-ui");
            var opened = GameDBAssetSession.TryRestore(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                document.CaptureState(), "editor-ui");
            m_session = opened.Session;
            var placeholder = rootVisualElement.Q<Label>(
                "active-document-placeholder");
            rootVisualElement.Q<VisualElement>("workspace-state-host")
                .style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("document-shell")
                .style.display = DisplayStyle.Flex;
            m_responsiveLayout = new GameDBEditorResponsiveLayout(Root);
            m_controller = new GameDBTableViewController(
                rootVisualElement.Q<UnityEditor.UIElements.ToolbarSearchField>(
                    "table-search-field"),
                rootVisualElement.Q<ListView>("table-navigation-list"),
                Grid, placeholder, state => m_viewState = state, EditValue);
            m_viewState = new GameDBWorkspaceTabViewState("Table00", "Row0000");
            m_controller.Bind(m_viewState, m_session.CreateSnapshot());
        }

        private GameDBValueEditResult EditValue(GameDBValueEditIntent intent)
        {
            CommitCount++;
            if (Equals(intent.WireValue, RejectedValue))
            {
                return new GameDBValueEditResult(false, "Rejected by attached test.",
                    m_session.CreateSnapshot());
            }

            var result = new GameDBEditorCommandService().Execute(m_session,
                new SetValueCommand(intent.TableName, intent.RowKey,
                    intent.FieldName, intent.WireValue), intent.ExpectedRevision);
            if (result.Success)
            {
                m_viewState = m_controller.Bind(m_viewState, result.Snapshot);
            }
            return new GameDBValueEditResult(result.Success, result.Message, result.Snapshot);
        }

        private void OnDisable()
        {
            m_controller?.Dispose();
            m_responsiveLayout?.Dispose();
            m_session?.Dispose();
            m_controller = null;
            m_responsiveLayout = null;
            m_session = null;
            m_viewState = null;
        }
    }
}
