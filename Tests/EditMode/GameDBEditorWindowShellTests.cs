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
                { "table-search-field", typeof(ToolbarSearchField) },
                { "table-navigation-list", typeof(ListView) },
                { "table-surface-host", typeof(VisualElement) },
                { "active-document-placeholder", typeof(Label) },
                { "table-row-grid", typeof(MultiColumnListView) },
                { "inspector-host", typeof(VisualElement) },
                { "schema-action-scroll", typeof(ScrollView) },
                { "database-scope-field", typeof(TextField) },
                { "database-localization-toggle", typeof(Toggle) },
                { "apply-database-metadata-button", typeof(Button) },
                { "table-name-field", typeof(TextField) },
                { "table-key-type-field", typeof(DropdownField) },
                { "field-navigation-list", typeof(ListView) },
                { "field-name-field", typeof(TextField) },
                { "field-type-field", typeof(DropdownField) },
                { "row-key-field", typeof(TextField) },
                { "editor-action-message-host", typeof(VisualElement) },
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
                { "registered-database-paths", typeof(ScrollView) },
                { "register-database-button", typeof(Button) },
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
            Assert.That(root.Query<ListView>().ToList(), Has.Count.EqualTo(4));
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
