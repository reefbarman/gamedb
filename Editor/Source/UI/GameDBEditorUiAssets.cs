using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal static class GameDBEditorUiAssets
    {
        internal const string WindowUxmlPath =
            "Packages/com.reefbarman.gamedb/Editor/UI/GameDBEditorWindow.uxml";
        internal const string TokensUssPath =
            "Packages/com.reefbarman.gamedb/Editor/UI/GameDBTokens.uss";
        internal const string WindowUssPath =
            "Packages/com.reefbarman.gamedb/Editor/UI/GameDBEditorWindow.uss";

        private static readonly IReadOnlyDictionary<string, Type> RequiredElements
            = new Dictionary<string, Type>
            {
                { "gamedb-editor-root", typeof(VisualElement) },
                { "global-toolbar", typeof(UnityEditor.UIElements.Toolbar) },
                { "create-database-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "open-database-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "global-status-label", typeof(Label) },
                { "settings-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "document-tab-strip", typeof(VisualElement) },
                { "workspace-state-host", typeof(VisualElement) },
                { "document-shell", typeof(VisualElement) },
                { "document-status-host", typeof(VisualElement) },
                { "active-document-path-label", typeof(Label) },
                { "active-document-summary-label", typeof(Label) },
                { "document-warning-host", typeof(VisualElement) },
                { "play-mode-toolbar", typeof(VisualElement) },
                { "runtime-target-field", typeof(DropdownField) },
                { "load-runtime-button", typeof(Button) },
                { "reload-in-game-button", typeof(Button) },
                { "play-mode-status-label", typeof(Label) },
                { "document-toolbar", typeof(UnityEditor.UIElements.Toolbar) },
                { "undo-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "redo-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "save-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "reload-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "generate-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "build-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "workspace-content", typeof(VisualElement) },
                { "table-navigation-host", typeof(VisualElement) },
                { "table-create-button", typeof(Button) },
                { "table-navigation-list", typeof(ListView) },
                { "table-surface-host", typeof(VisualElement) },
                { "table-toolbar", typeof(UnityEditor.UIElements.Toolbar) },
                { "table-add-row-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "table-delete-row-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "table-search-field", typeof(UnityEditor.UIElements.ToolbarSearchField) },
                { "table-columns-button", typeof(UnityEditor.UIElements.ToolbarButton) },
                { "table-inspector-toggle-button", typeof(UnityEditor.UIElements.ToolbarButton) },
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
                { "close-settings-button", typeof(Button) },
                { "collection-editor-panel", typeof(VisualElement) },
                { "collection-editor-title", typeof(Label) },
                { "collection-editor-context", typeof(Label) },
                { "collection-editor-error-host", typeof(VisualElement) },
                { "collection-editor-list", typeof(ListView) },
                { "collection-add-button", typeof(Button) },
                { "collection-reload-button", typeof(Button) },
                { "collection-apply-button", typeof(Button) },
                { "collection-cancel-button", typeof(Button) }
            };

        internal static void Build(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var tree = LoadRequired<VisualTreeAsset>(WindowUxmlPath);
            var tokens = LoadRequired<StyleSheet>(TokensUssPath);
            var window = LoadRequired<StyleSheet>(WindowUssPath);
            root.Clear();
            RemoveStyle(root, tokens);
            RemoveStyle(root, window);
            root.styleSheets.Add(tokens);
            root.styleSheets.Add(window);
            tree.CloneTree(root);
            ValidateRequiredElements(root);
        }

        internal static bool IsBuilt(VisualElement root)
        {
            return root?.Q<VisualElement>("gamedb-editor-root") != null;
        }


        internal static void ShowError(VisualElement root, Exception exception)
        {
            root.Clear();
            root.styleSheets.Clear();
            root.Add(new HelpBox(
                "GameDB editor UI could not be created: " + exception.Message,
                HelpBoxMessageType.Error));
        }

        internal static void ValidateRequiredElements(VisualElement root)
        {
            foreach (var required in RequiredElements)
            {
                var matches = new List<VisualElement>();
                CollectNamed(root, required.Key, matches);
                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Required GameDB UI element '{required.Key}' was not found in '{WindowUxmlPath}'.");
                }
                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Required GameDB UI element '{required.Key}' appears {matches.Count} times in '{WindowUxmlPath}'.");
                }
                if (!required.Value.IsInstanceOfType(matches[0]))
                {
                    throw new InvalidOperationException(
                        $"Required GameDB UI element '{required.Key}' in '{WindowUxmlPath}' must be a {required.Value.Name}, but was {matches[0].GetType().Name}.");
                }
            }
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required GameDB UI asset '{path}' could not be loaded as {typeof(T).Name}.");
            }
            return asset;
        }

        private static void RemoveStyle(VisualElement root, StyleSheet styleSheet)
        {
            if (root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Remove(styleSheet);
            }
        }

        private static void CollectNamed(VisualElement element, string name,
            ICollection<VisualElement> matches)
        {
            if (element.name == name)
            {
                matches.Add(element);
            }
            for (var index = 0; index < element.hierarchy.childCount; index++)
            {
                CollectNamed(element.hierarchy[index], name, matches);
            }
        }
    }
}
