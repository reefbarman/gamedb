using GameDBEditorLibrary.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorWindowShellTests
    {
        private static readonly IReadOnlyDictionary<string, Type> RequiredElements
            = new Dictionary<string, Type>
            {
                { "gamedb-editor-root", typeof(VisualElement) },
                { "global-toolbar", typeof(Toolbar) },
                { "create-database-button", typeof(ToolbarButton) },
                { "open-database-button", typeof(ToolbarButton) },
                { "global-status-label", typeof(Label) },
                { "settings-button", typeof(ToolbarButton) },
                { "document-tab-strip", typeof(VisualElement) },
                { "workspace-state-host", typeof(VisualElement) },
                { "document-shell", typeof(VisualElement) },
                { "document-status-host", typeof(VisualElement) },
                { "active-document-path-label", typeof(Label) },
                { "active-document-summary-label", typeof(Label) },
                { "document-warning-host", typeof(VisualElement) },
                { "document-toolbar", typeof(Toolbar) },
                { "undo-button", typeof(ToolbarButton) },
                { "redo-button", typeof(ToolbarButton) },
                { "save-button", typeof(ToolbarButton) },
                { "reload-button", typeof(ToolbarButton) },
                { "generate-button", typeof(ToolbarButton) },
                { "build-button", typeof(ToolbarButton) },
                { "workspace-content", typeof(VisualElement) },
                { "table-navigation-host", typeof(VisualElement) },
                { "table-create-button", typeof(Button) },
                { "table-navigation-list", typeof(ListView) },
                { "table-surface-host", typeof(VisualElement) },
                { "table-toolbar", typeof(Toolbar) },
                { "table-add-row-button", typeof(ToolbarButton) },
                { "table-delete-row-button", typeof(ToolbarButton) },
                { "table-search-field", typeof(ToolbarSearchField) },
                { "table-columns-button", typeof(ToolbarButton) },
                { "table-inspector-toggle-button", typeof(ToolbarButton) },
                { "table-action-message-host", typeof(VisualElement) },
                { "table-empty-state", typeof(VisualElement) },
                { "table-empty-message", typeof(Label) },
                { "table-empty-action", typeof(Button) },
                { "table-row-grid", typeof(MultiColumnListView) },
                { "inspector-scrim", typeof(VisualElement) },
                { "inspector-host", typeof(VisualElement) },
                { "inspector-back-button", typeof(Button) },
                { "inspector-eyebrow-label", typeof(Label) },
                { "inspector-title-label", typeof(Label) },
                { "inspector-close-button", typeof(Button) },
                { "inspector-content-host", typeof(VisualElement) },
                { "inspector-table-view", typeof(VisualElement) },
                { "inspector-table-summary", typeof(Label) },
                { "table-rename-action", typeof(Button) },
                { "table-delete-action", typeof(Button) },
                { "field-create-button", typeof(Button) },
                { "field-navigation-list", typeof(ListView) },
                { "inspector-field-view", typeof(ScrollView) },
                { "inspector-field-type-label", typeof(Label) },
                { "inspector-field-detail-label", typeof(Label) },
                { "field-rename-action", typeof(Button) },
                { "field-change-type-action", typeof(Button) },
                { "field-delete-action", typeof(Button) },
                { "inspector-task-scroll", typeof(ScrollView) },
                { "inspector-task-context-label", typeof(Label) },
                { "inspector-task-form-host", typeof(VisualElement) },
                { "field-type-editor-host", typeof(VisualElement) },
                { "inspector-task-message-host", typeof(VisualElement) },
                { "inspector-action-message-host", typeof(VisualElement) },
                { "inspector-database-foldout", typeof(VisualElement) },
                { "database-foldout-toggle", typeof(Button) },
                { "database-foldout-scroll", typeof(ScrollView) },
                { "database-summary-label", typeof(Label) },
                { "database-edit-action", typeof(Button) },
                { "inspector-navigation-decision", typeof(VisualElement) },
                { "inspector-navigation-message", typeof(Label) },
                { "inspector-navigation-cancel", typeof(Button) },
                { "inspector-navigation-discard", typeof(Button) },
                { "inspector-navigation-save", typeof(Button) },
                { "inspector-task-footer", typeof(VisualElement) },
                { "inspector-task-cancel", typeof(Button) },
                { "inspector-task-primary", typeof(Button) },
                { "popover-layer", typeof(VisualElement) },
                { "add-row-popover", typeof(VisualElement) },
                { "add-row-popover-title", typeof(Label) },
                { "add-row-key-control-host", typeof(VisualElement) },
                { "add-row-validation-message", typeof(Label) },
                { "add-row-cancel-button", typeof(Button) },
                { "add-row-confirm-button", typeof(Button) },
                { "modal-host", typeof(VisualElement) },
                { "settings-panel", typeof(VisualElement) },
                { "collection-editor-panel", typeof(VisualElement) },
                { "collection-editor-title", typeof(Label) },
                { "collection-editor-context", typeof(Label) },
                { "collection-editor-error-host", typeof(VisualElement) },
                { "collection-editor-list", typeof(ListView) },
                { "collection-add-button", typeof(Button) },
                { "collection-reload-button", typeof(Button) },
                { "collection-apply-button", typeof(Button) },
                { "collection-cancel-button", typeof(Button) },
                { "settings-error-label", typeof(Label) },
                { "registered-database-empty-label", typeof(Label) },
                { "registered-database-paths", typeof(ScrollView) },
                { "registration-path-field", typeof(TextField) },
                { "register-database-button", typeof(Button) },
                { "register-current-database-button", typeof(Button) },
                { "imported-enum-types", typeof(ListView) },
                { "export-path-field", typeof(TextField) },
                { "build-path-field", typeof(TextField) },
                { "settings-validation-host", typeof(VisualElement) },
                { "save-settings-button", typeof(Button) },
                { "close-settings-button", typeof(Button) }
            };

        [Test]
        public void PackageUiAssets_LoadFromLogicalPaths()
        {
            AssertAssetPath<VisualTreeAsset>(GameDBEditorUiAssets.WindowUxmlPath);
            AssertAssetPath<StyleSheet>(GameDBEditorUiAssets.TokensUssPath);
            AssertAssetPath<StyleSheet>(GameDBEditorUiAssets.WindowUssPath);
        }

        [Test]
        public void WindowUxml_ContainsRequiredNamedContractExactlyOnce()
        {
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);

            foreach (var required in RequiredElements)
            {
                var matches = root.Query<VisualElement>(name: required.Key).ToList();
                Assert.That(matches, Has.Count.EqualTo(1), required.Key);
                Assert.That(matches[0], Is.InstanceOf(required.Value), required.Key);
            }
            Assert.That(root.Query<MultiColumnListView>().ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Q<MultiColumnListView>("table-row-grid")
                .horizontalScrollingEnabled, Is.True);
            Assert.That(root.Q<MultiColumnListView>("table-row-grid").sortingMode,
                Is.EqualTo(ColumnSortingMode.Custom));
            Assert.That(root.Q<VisualElement>("popover-layer").parent,
                Is.SameAs(root.Q<VisualElement>("modal-host").parent));
            Assert.That(root.Q<VisualElement>("table-empty-state").parent,
                Is.SameAs(root.Q<Toolbar>("table-toolbar").parent));
            Assert.That(root.Query<ListView>().ToList(), Has.Count.EqualTo(4));
            Assert.That(root.Q<VisualElement>("inspector-task-footer").parent,
                Is.SameAs(root.Q<VisualElement>("inspector-content-host").parent));
            Assert.That(root.Q<VisualElement>("field-type-editor-host").parent,
                Is.SameAs(root.Q<ScrollView>("inspector-task-scroll").contentContainer));
            Assert.That(root.Q<ListView>("field-navigation-list"), Is.Not.Null);
            Assert.That(root.Q<ListView>("collection-editor-list"), Is.Not.Null);
            Assert.That(root.Q<ListView>("imported-enum-types"), Is.Not.Null);
            Assert.That(root.Q<TreeView>(), Is.Null);
            Assert.That(root.Q<IMGUIContainer>(), Is.Null);
        }

        [Test]
        public void ResponsiveLayout_AppliesBreakpointClassesAndStopsAfterDispose()
        {
            var root = new VisualElement();
            var layout = new GameDBEditorResponsiveLayout(root);

            layout.Apply(GameDBEditorResponsiveLayout.CompactWidth);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.False);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.False);

            layout.Apply(GameDBEditorResponsiveLayout.CompactWidth - 1f);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.True);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.False);

            layout.Apply(GameDBEditorResponsiveLayout.NarrowWidth - 1f);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.True);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.True);

            layout.Dispose();
            layout.Apply(1000f);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.CompactClass), Is.True);
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.NarrowClass), Is.True);
        }

        [Test]
        public void ResponsiveLayout_ControlsWidePaneAndCompactInspectorDrawer()
        {
            var host = new VisualElement();
            GameDBEditorUiAssets.Build(host);
            var root = host.Q<VisualElement>("gamedb-editor-root");
            var navigation = root.Q<VisualElement>("table-navigation-host");
            var surface = root.Q<VisualElement>("table-surface-host");
            var layout = new GameDBEditorResponsiveLayout(root);
            try
            {
                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth);
                Assert.That(layout.IsInspectorOpen, Is.True);
                Assert.That(root.ClassListContains(
                    GameDBEditorResponsiveLayout.InspectorOpenClass), Is.True);
                Assert.That(navigation.enabledSelf, Is.True);
                Assert.That(surface.enabledSelf, Is.True);

                layout.ToggleInspector();
                Assert.That(layout.IsInspectorOpen, Is.False);
                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth - 1f);
                Assert.That(layout.IsInspectorOpen, Is.False,
                    "Compact mode should not silently reopen a collapsed wide Inspector.");

                layout.ToggleInspector();
                Assert.That(layout.IsInspectorOpen, Is.True);
                Assert.That(navigation.enabledSelf, Is.False);
                Assert.That(surface.enabledSelf, Is.False);

                layout.CloseInspector();
                Assert.That(layout.IsInspectorOpen, Is.False);
                Assert.That(navigation.enabledSelf, Is.True);
                Assert.That(surface.enabledSelf, Is.True);

                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth);
                Assert.That(layout.IsInspectorOpen, Is.False,
                    "Returning wide should restore the user's collapsed pane preference.");
                layout.ToggleInspector();
                Assert.That(layout.IsInspectorOpen, Is.True);
                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth - 1f);
                Assert.That(layout.IsInspectorOpen, Is.False);
                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth);
                Assert.That(layout.IsInspectorOpen, Is.True,
                    "Returning wide should restore the user's open pane preference.");

                layout.Apply(GameDBEditorResponsiveLayout.CompactWidth - 1f);
                layout.ToggleInspector();
                Assert.That(layout.IsInspectorOpen, Is.True);
                Assert.That(navigation.enabledSelf, Is.False);
                Assert.That(surface.enabledSelf, Is.False);
            }
            finally
            {
                layout.Dispose();
            }
            Assert.That(root.ClassListContains(
                GameDBEditorResponsiveLayout.InspectorOpenClass), Is.False);
            Assert.That(navigation.enabledSelf, Is.True);
            Assert.That(surface.enabledSelf, Is.True);
        }

        [Test]
        public void ResponsiveLayout_CloseGuardCanDeferInspectorDismissal()
        {
            var host = new VisualElement();
            GameDBEditorUiAssets.Build(host);
            var root = host.Q<VisualElement>("gamedb-editor-root");
            var layout = new GameDBEditorResponsiveLayout(root);
            try
            {
                var requests = 0;
                layout.SetCloseRequested(() => requests++);

                layout.CloseInspector();

                Assert.That(requests, Is.EqualTo(1));
                Assert.That(layout.IsInspectorOpen, Is.True);
            }
            finally
            {
                layout.Dispose();
            }
        }

        [Test]
        public void WindowShellBuilder_RebuildsWithoutDuplicateChildrenOrStyles()
        {
            var root = new VisualElement();

            GameDBEditorUiAssets.Build(root);
            GameDBEditorUiAssets.Build(root);

            Assert.That(root.Query<VisualElement>(name: "gamedb-editor-root").ToList(),
                Has.Count.EqualTo(1));
            Assert.That(root.styleSheets.count, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(root.styleSheets[0]),
                Is.EqualTo(GameDBEditorUiAssets.TokensUssPath));
            Assert.That(AssetDatabase.GetAssetPath(root.styleSheets[1]),
                Is.EqualTo(GameDBEditorUiAssets.WindowUssPath));
        }

        private static void AssertAssetPath<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(path));
        }
    }
}
