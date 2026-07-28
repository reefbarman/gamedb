using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal interface IGameDBEditorReloadPolicy
    {
        bool ConfirmDiscard(string assetPath, GameDBDocumentSessionState state);
    }

    internal sealed class GameDBEditorReloadDialogPolicy : IGameDBEditorReloadPolicy
    {
        public bool ConfirmDiscard(string assetPath, GameDBDocumentSessionState state)
        {
            var reason = state.PersistenceStateUnknown
                ? "Its persistence state is unknown."
                : "It has unsaved changes.";
            return EditorUtility.DisplayDialog("Reload GameDB From Disk",
                $"Reload '{assetPath}' from disk and discard the current draft? {reason}",
                "Reload From Disk", "Cancel");
        }
    }

    internal sealed class GameDBEditorWindowController : IDisposable
    {
        private sealed class SettingsPathBinding
        {
            internal Button RemoveButton { get; }
            internal Action Remove { get; }

            internal SettingsPathBinding(Button removeButton, Action remove)
            {
                RemoveButton = removeButton;
                Remove = remove;
            }
        }

        private sealed class TabBinding
        {
            internal string TabId { get; }
            internal VisualElement Root { get; }
            internal ToolbarButton Button { get; }
            internal ToolbarButton MoveLeftButton { get; }
            internal ToolbarButton MoveRightButton { get; }
            internal ToolbarButton CloseButton { get; }
            internal Action Activate { get; }
            internal Action MoveLeft { get; }
            internal Action MoveRight { get; }
            internal Action Close { get; }

            internal TabBinding(string tabId, VisualElement root,
                ToolbarButton button, ToolbarButton moveLeftButton,
                ToolbarButton moveRightButton, ToolbarButton closeButton,
                Action activate, Action moveLeft, Action moveRight, Action close)
            {
                TabId = tabId;
                Root = root;
                Button = button;
                MoveLeftButton = moveLeftButton;
                MoveRightButton = moveRightButton;
                CloseButton = closeButton;
                Activate = activate;
                MoveLeft = moveLeft;
                MoveRight = moveRight;
                Close = close;
            }
        }

        private readonly GameDBEditorWorkspace m_workspace;
        private readonly IGameDBTabClosePolicy m_closePolicy;
        private readonly GameDBProjectSettingsService m_projectSettings;
        private readonly IGameDBEditorDatabaseDialogs m_databaseDialogs;
        private readonly ToolbarButton m_createButton;
        private readonly ToolbarButton m_openButton;
        private readonly ToolbarButton m_settingsButton;
        private readonly VisualElement m_tabStrip;
        private readonly VisualElement m_workspaceState;
        private readonly VisualElement m_documentShell;
        private readonly VisualElement m_documentWarningHost;
        private readonly Label m_globalStatus;
        private readonly Label m_documentPath;
        private readonly Label m_documentSummary;
        private readonly Label m_documentPlaceholder;
        private readonly ToolbarSearchField m_tableSearch;
        private readonly ListView m_tableNavigation;
        private readonly MultiColumnListView m_tableGrid;
        private readonly GameDBTableViewController m_tableView;
        private readonly GameDBEditorCommandService m_commandService;
        private readonly GameDBSchemaControlsController m_schemaControls;
        private readonly GameDBCollectionEditorController m_collectionEditor;
        private readonly GameDBEditorResponsiveLayout m_responsiveLayout;
        private readonly IGameDBEditorReloadPolicy m_reloadPolicy;
        private readonly IGameDBEditorDestructiveActionPolicy m_destructivePolicy;
        private readonly IGameDBEditorOutputService m_outputService;
        private readonly Func<IReadOnlyList<string>> m_availableEnumTypes;
        private readonly GameDBRuntimeRegistry m_runtimeRegistry;
        private readonly GameDBPlayModeService m_playModeService;
        private readonly Func<bool> m_isPlaying;
        private readonly VisualElement m_root;
        private readonly ToolbarButton m_undoButton;
        private readonly ToolbarButton m_redoButton;
        private readonly ToolbarButton m_saveButton;
        private readonly ToolbarButton m_reloadButton;
        private readonly ToolbarButton m_generateButton;
        private readonly ToolbarButton m_buildButton;
        private readonly VisualElement m_playModeToolbar;
        private readonly DropdownField m_runtimeTarget;
        private readonly Button m_loadRuntimeButton;
        private readonly Button m_reloadInGameButton;
        private readonly Label m_playModeStatus;
        private readonly VisualElement m_modalHost;
        private readonly VisualElement m_settingsPanel;
        private readonly Label m_settingsError;
        private readonly ScrollView m_registeredPaths;
        private readonly Button m_registerButton;
        private readonly ListView m_importedEnumTypes;
        private readonly TextField m_exportPath;
        private readonly TextField m_buildPath;
        private readonly VisualElement m_settingsValidationHost;
        private readonly Button m_saveSettingsButton;
        private readonly Button m_closeSettingsButton;
        private readonly List<TabBinding> m_tabBindings = new List<TabBinding>();
        private readonly List<SettingsPathBinding> m_settingsPathBindings
            = new List<SettingsPathBinding>();
        private IReadOnlyList<GameDBRuntimeTargetDescriptor> m_runtimeTargets
            = Array.Empty<GameDBRuntimeTargetDescriptor>();
        private IReadOnlyList<string> m_enumTypeNames = Array.Empty<string>();
        private readonly HashSet<string> m_selectedEnumTypeNames
            = new HashSet<string>(StringComparer.Ordinal);
        private string m_selectedRuntimeTargetId;
        private string m_playModeMessage;
        private string m_outputMessage;
        private bool m_outputSucceeded;
        private bool m_restoringPrePlayModeState;
        private bool m_disposed;

        internal GameDBEditorWindowController(VisualElement root,
            GameDBEditorWorkspace workspace,
            IGameDBTabClosePolicy closePolicy = null,
            GameDBProjectSettingsService projectSettings = null,
            IGameDBEditorDatabaseDialogs databaseDialogs = null,
            IGameDBEditorDestructiveActionPolicy destructiveActionPolicy = null,
            IGameDBEditorReloadPolicy reloadPolicy = null,
            GameDBRuntimeRegistry runtimeRegistry = null,
            Func<bool> isPlaying = null,
            IGameDBEditorOutputService outputService = null,
            Func<IReadOnlyList<string>> availableEnumTypes = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            m_root = root;
            m_workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            m_closePolicy = closePolicy ?? new GameDBEditorTabCloseDialogPolicy();
            m_reloadPolicy = reloadPolicy ?? new GameDBEditorReloadDialogPolicy();
            m_destructivePolicy = destructiveActionPolicy
                ?? new GameDBEditorDestructiveActionDialogPolicy();
            m_outputService = outputService ?? new GameDBEditorOutputService();
            m_availableEnumTypes = availableEnumTypes ?? GetAvailableEnumTypeNames;
            m_runtimeRegistry = runtimeRegistry ?? GameDBEditorDomainServices.RuntimeRegistry;
            m_playModeService = new GameDBPlayModeService(m_runtimeRegistry);
            m_isPlaying = isPlaying ?? (() => Application.isPlaying);
            m_projectSettings = projectSettings ?? GameDBEditorDomainServices.ProjectSettings;
            m_databaseDialogs = databaseDialogs ?? new GameDBEditorNativeDatabaseDialogs();
            GameDBEditorUiAssets.ValidateRequiredElements(root);
            m_createButton = root.Q<ToolbarButton>("create-database-button");
            m_openButton = root.Q<ToolbarButton>("open-database-button");
            m_settingsButton = root.Q<ToolbarButton>("settings-button");
            m_tabStrip = root.Q<VisualElement>("document-tab-strip");
            m_workspaceState = root.Q<VisualElement>("workspace-state-host");
            m_documentShell = root.Q<VisualElement>("document-shell");
            m_documentWarningHost = root.Q<VisualElement>("document-warning-host");
            m_globalStatus = root.Q<Label>("global-status-label");
            m_documentPath = root.Q<Label>("active-document-path-label");
            m_documentSummary = root.Q<Label>("active-document-summary-label");
            m_documentPlaceholder = root.Q<Label>("active-document-placeholder");
            m_tableSearch = root.Q<ToolbarSearchField>("table-search-field");
            m_tableNavigation = root.Q<ListView>("table-navigation-list");
            m_tableGrid = root.Q<MultiColumnListView>("table-row-grid");
            m_commandService = new GameDBEditorCommandService();
            m_tableView = new GameDBTableViewController(m_tableSearch,
                m_tableNavigation, m_tableGrid, m_documentPlaceholder,
                SetTableViewState, SetValue, OpenCollectionEditor);
            m_schemaControls = new GameDBSchemaControlsController(root,
                m_workspace, m_destructivePolicy, Render, IsDataOnlyEditing);
            m_collectionEditor = new GameDBCollectionEditorController(root,
                m_workspace, Render, IsDataOnlyEditing);
            m_responsiveLayout = new GameDBEditorResponsiveLayout(
                root.Q<VisualElement>("gamedb-editor-root"));
            m_undoButton = root.Q<ToolbarButton>("undo-button");
            m_redoButton = root.Q<ToolbarButton>("redo-button");
            m_saveButton = root.Q<ToolbarButton>("save-button");
            m_reloadButton = root.Q<ToolbarButton>("reload-button");
            m_generateButton = root.Q<ToolbarButton>("generate-button");
            m_buildButton = root.Q<ToolbarButton>("build-button");
            m_playModeToolbar = root.Q<VisualElement>("play-mode-toolbar");
            m_runtimeTarget = root.Q<DropdownField>("runtime-target-field");
            m_loadRuntimeButton = root.Q<Button>("load-runtime-button");
            m_reloadInGameButton = root.Q<Button>("reload-in-game-button");
            m_playModeStatus = root.Q<Label>("play-mode-status-label");
            m_modalHost = root.Q<VisualElement>("modal-host");
            m_settingsPanel = root.Q<VisualElement>("settings-panel");
            m_settingsError = root.Q<Label>("settings-error-label");
            m_registeredPaths = root.Q<ScrollView>("registered-database-paths");
            m_registerButton = root.Q<Button>("register-database-button");
            m_importedEnumTypes = root.Q<ListView>("imported-enum-types");
            m_importedEnumTypes.makeItem = CreateImportedEnumToggle;
            m_importedEnumTypes.bindItem = BindImportedEnum;
            m_exportPath = root.Q<TextField>("export-path-field");
            m_buildPath = root.Q<TextField>("build-path-field");
            m_settingsValidationHost = root.Q<VisualElement>("settings-validation-host");
            m_saveSettingsButton = root.Q<Button>("save-settings-button");
            m_closeSettingsButton = root.Q<Button>("close-settings-button");
            m_createButton.clicked += CreateDatabase;
            m_openButton.clicked += OpenDatabase;
            m_settingsButton.clicked += OpenSettings;
            m_undoButton.clicked += UndoActiveDocument;
            m_redoButton.clicked += RedoActiveDocument;
            m_saveButton.clicked += SaveActiveDocument;
            m_reloadButton.clicked += ReloadActiveDocument;
            m_generateButton.clicked += GenerateActiveDocument;
            m_buildButton.clicked += BuildActiveDocument;
            m_runtimeTarget.RegisterValueChangedCallback(OnRuntimeTargetChanged);
            m_loadRuntimeButton.clicked += LoadRuntimeData;
            m_reloadInGameButton.clicked += ReloadInGame;
            m_runtimeRegistry.Changed += OnRuntimeRegistryChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            m_registerButton.clicked += RegisterSelectedDatabase;
            m_saveSettingsButton.clicked += SaveSettings;
            m_closeSettingsButton.clicked += CloseSettings;
            m_root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_workspace.StateChanged += Render;
            m_projectSettings.Changed += OnProjectSettingsChanged;
            try
            {
                CloseSettings();
                RenderSettings(m_projectSettings.Load());
                Render();
            }
            catch
            {
                m_tableView.Dispose();
                m_schemaControls.Dispose();
                m_collectionEditor.Dispose();
                m_responsiveLayout?.Dispose();
                DetachCallbacksAndBindings();
                throw;
            }
        }

        internal void Render()
        {
            if (m_disposed)
            {
                return;
            }

            ReconcileTabs();
            ClearRecoveryMessages(m_workspaceState);
            m_documentWarningHost.Clear();
            var active = m_workspace.ActiveTab;
            var hasActiveDocument = active != null;
            var playing = m_isPlaying();
            foreach (var tab in m_workspace.Tabs)
            {
                tab.Session.SetAllowedOperations(playing
                    ? GameDBEditorCommandService.DataOnlyOperations : null);
                if (playing && !tab.HasPlayModeState)
                {
                    tab.BeginPlayMode(tab.Session.CaptureState(), false);
                }
            }
            if (!playing)
            {
                RestorePrePlayModeStates();
                m_selectedRuntimeTargetId = null;
            }
            m_playModeToolbar.style.display = playing && hasActiveDocument
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_workspaceState.style.display = hasActiveDocument
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            m_documentShell.style.display = hasActiveDocument
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            m_globalStatus.text = hasActiveDocument
                ? m_workspace.Tabs.Count + (m_workspace.Tabs.Count == 1
                    ? " database open"
                    : " databases open")
                : "GameDB workspace";
            m_undoButton.SetEnabled(false);
            m_redoButton.SetEnabled(false);
            m_reloadButton.SetEnabled(false);
            m_generateButton.SetEnabled(false);
            m_buildButton.SetEnabled(false);

            var warningHost = hasActiveDocument
                ? m_documentWarningHost
                : m_workspaceState;
            RenderWarnings(warningHost);
            if (!hasActiveDocument)
            {
                m_saveButton.SetEnabled(false);
                m_undoButton.text = "Undo";
                m_redoButton.text = "Redo";
                m_reloadButton.text = "Reload";
                m_documentPath.text = string.Empty;
                m_documentPath.tooltip = string.Empty;
                m_documentSummary.text = string.Empty;
                m_tableView.Bind(null, null);
                m_schemaControls.Bind(null, null);
                BindPlayModeControls(null, playing);
                return;
            }

            BindPlayModeControls(active, playing);
            var state = active.Session.GetState();
            var history = active.Session.GetHistoryState();
            var snapshot = active.Session.CreateSnapshot();
            m_undoButton.text = HistoryButtonText("Undo", history.UndoLabel);
            m_undoButton.tooltip = history.CanUndo ? m_undoButton.text : "Nothing to undo";
            m_undoButton.SetEnabled(history.CanUndo);
            m_redoButton.text = HistoryButtonText("Redo", history.RedoLabel);
            m_redoButton.tooltip = history.CanRedo ? m_redoButton.text : "Nothing to redo";
            m_redoButton.SetEnabled(history.CanRedo);
            m_saveButton.SetEnabled(!playing
                && (state.IsDirty || state.HasPendingPostSaveWork));
            m_reloadButton.text = state.IsDirty || state.PersistenceStateUnknown
                ? "Revert" : "Reload";
            var baseline = state.BaselineDiskToken;
            var hasCompleteBaseline = baseline.DataExists && baseline.SchemaExists;
            var diskChanged = m_workspace.LastDiskState != null
                && m_workspace.LastDiskState.State != GameDBDiskState.Unchanged;
            m_reloadButton.SetEnabled(!playing && !state.HasPendingPostSaveWork
                && (hasCompleteBaseline && state.IsDirty
                    || diskChanged || state.PersistenceStateUnknown));
            var projectSettings = m_projectSettings.GetSnapshot();
            m_generateButton.SetEnabled(!playing
                && !string.IsNullOrWhiteSpace(projectSettings.ExportPath));
            m_buildButton.SetEnabled(!playing
                && !string.IsNullOrWhiteSpace(projectSettings.BuildPath));
            m_documentPath.text = active.Session.AssetPath;
            m_documentPath.tooltip = active.Session.AssetPath;
            m_documentSummary.text = BuildDocumentSummary(snapshot.ScopeName,
                snapshot.Tables.Count, state);
            var resolvedViewState = m_tableView.Bind(active.ViewState, snapshot);
            if (!resolvedViewState.HasSameValues(active.ViewState))
            {
                m_workspace.TrySetTabViewState(active.TabId, resolvedViewState);
            }
            m_schemaControls.Bind(m_workspace.ActiveTab, snapshot);
        }

        internal bool ActivateTab(string tabId)
        {
            if (m_disposed)
            {
                return false;
            }
            var changed = m_workspace.TryActivateTab(tabId);
            if (changed)
            {
                m_outputMessage = null;
                Render();
            }
            return changed;
        }

        internal GameDBTabCloseResult CloseTab(string tabId)
        {
            if (m_disposed)
            {
                return new GameDBTabCloseResult(GameDBTabCloseStatus.Cancelled);
            }
            var result = m_workspace.CloseTab(tabId, m_closePolicy);
            if (result.Status == GameDBTabCloseStatus.Closed)
            {
                m_outputMessage = null;
                Render();
            }
            return result;
        }

        internal GameDBTabReorderResult MoveTab(string tabId, int offset)
        {
            if (m_disposed)
            {
                return new GameDBTabReorderResult(GameDBTabReorderStatus.NoChange);
            }
            var index = m_workspace.Tabs.ToList().FindIndex(tab => tab.TabId == tabId);
            return index < 0
                ? new GameDBTabReorderResult(GameDBTabReorderStatus.NotFound)
                : m_workspace.ReorderTab(tabId, index + offset);
        }

        internal GameDBWorkspaceDatabaseOpenResult ChooseAndCreateDatabase()
        {
            return m_disposed
                ? null
                : CreateDatabase(m_databaseDialogs.SelectCreateDatabase());
        }

        internal GameDBWorkspaceDatabaseOpenResult ChooseAndOpenDatabase()
        {
            return m_disposed
                ? null
                : OpenDatabase(m_databaseDialogs.SelectOpenDatabase());
        }

        internal GameDBProjectSettingsResult ChooseAndRegisterDatabase()
        {
            return m_disposed
                ? null
                : RegisterDatabase(m_databaseDialogs.SelectRegisterDatabase());
        }

        internal GameDBWorkspaceDatabaseOpenResult CreateDatabase(
            GameDBCreateDatabaseSelection selection)
        {
            if (m_disposed || selection == null)
            {
                return null;
            }
            return m_workspace.TryCreateDatabase(selection.AssetPath,
                selection.ScopeName, selection.Localization);
        }

        internal GameDBWorkspaceDatabaseOpenResult OpenDatabase(string assetPath)
        {
            return m_disposed || string.IsNullOrWhiteSpace(assetPath)
                ? null
                : m_workspace.TryOpenDatabase(assetPath);
        }

        internal GameDBProjectSettingsResult RegisterDatabase(string assetPath)
        {
            if (m_disposed || string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }
            var snapshot = m_projectSettings.GetSnapshot();
            if (!m_workspace.TryGetRegisteredDatabasePath(assetPath,
                out var registeredPath, out var error))
            {
                var invalid = new GameDBProjectSettingsResult(false, false,
                    snapshot, error);
                RenderSettings(invalid, true);
                return invalid;
            }

            var paths = snapshot.RegisteredDatabasePaths.Concat(new[] { registeredPath });
            var result = m_projectSettings.Update(paths,
                snapshot.ImportedEnumTypeNames, snapshot.ExportPath, snapshot.BuildPath);
            RenderSettings(result, true);
            return result;
        }

        internal GameDBProjectSettingsResult UnregisterDatabase(string registeredPath)
        {
            if (m_disposed || string.IsNullOrWhiteSpace(registeredPath))
            {
                return null;
            }
            var snapshot = m_projectSettings.GetSnapshot();
            var paths = snapshot.RegisteredDatabasePaths.Where(path =>
                !string.Equals(path, registeredPath, StringComparison.Ordinal));
            var result = m_projectSettings.Update(paths,
                snapshot.ImportedEnumTypeNames, snapshot.ExportPath, snapshot.BuildPath);
            RenderSettings(result, true);
            return result;
        }

        internal GameDBProjectSettingsResult UpdateProjectSettings(
            string exportPath, string buildPath,
            IEnumerable<string> importedEnumTypeNames = null)
        {
            if (m_disposed)
            {
                return null;
            }
            var snapshot = m_projectSettings.GetSnapshot();
            var result = m_projectSettings.Update(snapshot.RegisteredDatabasePaths,
                importedEnumTypeNames ?? snapshot.ImportedEnumTypeNames,
                exportPath, buildPath);
            RenderSettings(result, !result.Success);
            return result;
        }

        internal void OpenSettings()
        {
            if (m_disposed)
            {
                return;
            }
            m_collectionEditor.Cancel();
            RenderSettings(m_projectSettings.Load());
            m_settingsPanel.style.display = DisplayStyle.Flex;
            m_modalHost.style.display = DisplayStyle.Flex;
            m_modalHost.pickingMode = PickingMode.Position;
        }

        internal void CloseSettings()
        {
            m_settingsPanel.style.display = DisplayStyle.None;
            if (!m_collectionEditor.IsOpen)
            {
                m_modalHost.style.display = DisplayStyle.None;
                m_modalHost.pickingMode = PickingMode.Ignore;
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            foreach (var tab in m_workspace.Tabs)
            {
                if (!tab.Session.IsDisposed)
                {
                    tab.Session.SetAllowedOperations(null);
                }
            }
            m_disposed = true;
            m_tableView.Dispose();
            m_schemaControls.Dispose();
            m_collectionEditor.Dispose();
            m_responsiveLayout.Dispose();
            DetachCallbacksAndBindings();
        }

        private void DetachCallbacksAndBindings()
        {
            m_workspace.StateChanged -= Render;
            m_projectSettings.Changed -= OnProjectSettingsChanged;
            m_runtimeRegistry.Changed -= OnRuntimeRegistryChanged;
            EditorApplication.delayCall -= RenderFromRuntimeRegistry;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            m_createButton.clicked -= CreateDatabase;
            m_openButton.clicked -= OpenDatabase;
            m_settingsButton.clicked -= OpenSettings;
            m_undoButton.clicked -= UndoActiveDocument;
            m_redoButton.clicked -= RedoActiveDocument;
            m_saveButton.clicked -= SaveActiveDocument;
            m_reloadButton.clicked -= ReloadActiveDocument;
            m_generateButton.clicked -= GenerateActiveDocument;
            m_buildButton.clicked -= BuildActiveDocument;
            m_runtimeTarget.UnregisterValueChangedCallback(OnRuntimeTargetChanged);
            m_loadRuntimeButton.clicked -= LoadRuntimeData;
            m_reloadInGameButton.clicked -= ReloadInGame;
            m_registerButton.clicked -= RegisterSelectedDatabase;
            m_saveSettingsButton.clicked -= SaveSettings;
            m_closeSettingsButton.clicked -= CloseSettings;
            m_root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            CloseSettings();
            ClearTabBindings();
            ClearSettingsPathBindings();
        }

        private void OpenCollectionEditor(GameDBCollectionEditRequest request)
        {
            CloseSettings();
            m_collectionEditor.Open(request);
        }

        private GameDBValueEditResult SetValue(GameDBValueEditIntent intent)
        {
            if (m_disposed || intent == null)
            {
                return new GameDBValueEditResult(false,
                    "The GameDB editor is no longer available.", null);
            }

            var session = m_workspace.ActiveTab?.Session;
            if (session == null)
            {
                return new GameDBValueEditResult(false,
                    "No active GameDB document is available.", null);
            }

            var result = m_commandService.Execute(session,
                new SetValueCommand(intent.TableName, intent.RowKey,
                    intent.FieldName, intent.WireValue), intent.ExpectedRevision,
                allowedOperations: IsDataOnlyEditing()
                    ? GameDBEditorCommandService.DataOnlyOperations : null);
            return new GameDBValueEditResult(result.Success,
                result.Message, result.Snapshot);
        }

        private bool IsDataOnlyEditing()
        {
            return !m_disposed && m_isPlaying();
        }

        private void BindPlayModeControls(GameDBEditorWorkspaceTab active, bool playing)
        {
            if (!playing || active == null)
            {
                m_runtimeTargets = Array.Empty<GameDBRuntimeTargetDescriptor>();
                m_runtimeTarget.choices = new List<string>();
                m_runtimeTarget.SetValueWithoutNotify(null);
                m_loadRuntimeButton.SetEnabled(false);
                m_reloadInGameButton.SetEnabled(false);
                m_playModeStatus.text = string.Empty;
                return;
            }

            var registry = m_playModeService.GetTargets();
            m_runtimeTargets = registry.Targets;
            var binding = active.PlayModeBinding;
            if (binding != null && !m_playModeService.IsCurrent(binding))
            {
                binding = null;
                m_playModeMessage = "The selected runtime GameDB is no longer available. Select it again.";
            }

            if (binding != null)
            {
                m_selectedRuntimeTargetId = binding.TargetId;
            }
            if (m_runtimeTargets.All(target => target.TargetId != m_selectedRuntimeTargetId))
            {
                m_selectedRuntimeTargetId = m_runtimeTargets.FirstOrDefault()?.TargetId;
            }

            m_runtimeTarget.choices = m_runtimeTargets.Select(target => target.DisplayName).ToList();
            var selectedIndex = m_runtimeTargets.ToList().FindIndex(target =>
                target.TargetId == m_selectedRuntimeTargetId);
            m_runtimeTarget.SetValueWithoutNotify(selectedIndex < 0
                ? null : m_runtimeTargets[selectedIndex].DisplayName);
            m_loadRuntimeButton.SetEnabled(selectedIndex >= 0);
            m_reloadInGameButton.SetEnabled(binding != null);
            m_playModeStatus.text = m_playModeMessage
                ?? (binding == null
                    ? m_runtimeTargets.Count == 0
                        ? "Waiting for a runtime GameDB to register."
                        : "Select a runtime GameDB and load its published data."
                    : "Editing runtime data only. Disk save and schema changes are disabled.");
        }

        private void OnRuntimeTargetChanged(ChangeEvent<string> change)
        {
            if (m_disposed || !m_isPlaying())
            {
                return;
            }
            var selected = m_runtimeTargets.FirstOrDefault(target =>
                target.DisplayName == change.newValue);
            m_selectedRuntimeTargetId = selected?.TargetId;
            m_playModeMessage = null;
            m_loadRuntimeButton.SetEnabled(selected != null);
        }

        private void LoadRuntimeData()
        {
            var active = m_workspace.ActiveTab;
            var target = m_runtimeTargets.FirstOrDefault(candidate =>
                candidate.TargetId == m_selectedRuntimeTargetId);
            if (!m_isPlaying() || active == null || target == null)
            {
                return;
            }

            m_collectionEditor.Cancel();
            var result = m_playModeService.LoadRuntimeData(active.Session,
                target.TargetId, target.Epoch, active.Session.GetState().CurrentRevision);
            if (result.Success)
            {
                active.SetPlayModeBinding(result.Binding, false);
                m_playModeMessage = "Runtime data loaded. Editing runtime data only.";
            }
            else
            {
                m_playModeMessage = result.Message;
            }
            Render();
        }

        private void ReloadInGame()
        {
            var active = m_workspace.ActiveTab;
            if (!m_isPlaying() || active?.PlayModeBinding == null)
            {
                return;
            }

            m_collectionEditor.Cancel();
            var result = m_playModeService.ReloadInGame(active.Session,
                active.PlayModeBinding, active.Session.GetState().CurrentRevision);
            m_playModeMessage = result.Success
                ? "Reloaded the active runtime GameDB."
                : result.Message;
            Render();
        }

        private void OnRuntimeRegistryChanged(GameDBRuntimeRegistrySnapshot snapshot)
        {
            EditorApplication.delayCall -= RenderFromRuntimeRegistry;
            EditorApplication.delayCall += RenderFromRuntimeRegistry;
        }

        private void RenderFromRuntimeRegistry()
        {
            if (!m_disposed)
            {
                Render();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!m_disposed && (state == PlayModeStateChange.EnteredPlayMode
                || state == PlayModeStateChange.EnteredEditMode))
            {
                Render();
            }
        }

        private void RestorePrePlayModeStates()
        {
            if (m_restoringPrePlayModeState)
            {
                return;
            }
            m_restoringPrePlayModeState = true;
            string restoreError = null;
            try
            {
                foreach (var tab in m_workspace.Tabs)
                {
                    if (!tab.HasPlayModeState)
                    {
                        continue;
                    }
                    var prePlayModeState = tab.PrePlayModeState;
                    var currentRevision = tab.Session.GetState().CurrentRevision;
                    var restored = tab.Session.ReplaceWorkingState(prePlayModeState.DataJson,
                        prePlayModeState.SchemaJson, currentRevision,
                    GameDBDocumentChangeOrigin.RuntimeImport);
                    if (!restored.Success)
                    {
                        restoreError = "Could not restore the pre-Play Mode document '"
                            + tab.Session.AssetPath + "': " + restored.Message;
                        continue;
                    }
                    tab.ClearPlayModeBinding(false);
                }
                m_playModeMessage = restoreError;
            }
            finally
            {
                m_restoringPrePlayModeState = false;
            }
        }

        private void SetTableViewState(GameDBWorkspaceTabViewState viewState)
        {
            if (m_disposed || viewState == null)
            {
                return;
            }
            var tab = m_workspace.ActiveTab;
            if (tab != null)
            {
                m_workspace.TrySetTabViewState(tab.TabId, viewState);
            }
        }

        private void ReconcileTabs()
        {
            var tabs = m_workspace.Tabs;
            var requiresRebuild = tabs.Count != m_tabBindings.Count;
            if (!requiresRebuild)
            {
                for (var index = 0; index < tabs.Count; index++)
                {
                    if (m_tabBindings[index].TabId != tabs[index].TabId)
                    {
                        requiresRebuild = true;
                        break;
                    }
                }
            }

            if (requiresRebuild)
            {
                ClearTabBindings();
                m_tabStrip.Clear();
                foreach (var tab in tabs)
                {
                    var tabId = tab.TabId;
                    Action activate = () => ActivateTab(tabId);
                    Action moveLeft = () => MoveTab(tabId, -1);
                    Action moveRight = () => MoveTab(tabId, 1);
                    Action close = () => CloseTab(tabId);
                    var root = new VisualElement
                    {
                        name = "document-tab-container-" + tabId
                    };
                    root.AddToClassList("gamedb-editor__tab-container");
                    var button = new ToolbarButton(activate)
                    {
                        name = "document-tab-" + tabId
                    };
                    button.AddToClassList("gamedb-editor__tab");
                    var left = TabActionButton("document-tab-move-left-" + tabId,
                        "‹", "Move tab left", moveLeft);
                    var right = TabActionButton("document-tab-move-right-" + tabId,
                        "›", "Move tab right", moveRight);
                    var closeButton = TabActionButton("document-tab-close-" + tabId,
                        "×", "Close tab", close);
                    root.Add(button);
                    root.Add(left);
                    root.Add(right);
                    root.Add(closeButton);
                    m_tabBindings.Add(new TabBinding(tabId, root, button,
                        left, right, closeButton, activate, moveLeft, moveRight, close));
                    m_tabStrip.Add(root);
                }
            }

            for (var index = 0; index < tabs.Count; index++)
            {
                var tab = tabs[index];
                var button = m_tabBindings[index].Button;
                button.text = BuildTabText(tab);
                button.tooltip = tab.Session.AssetPath;
                button.EnableInClassList("gamedb-editor__tab--active",
                    tab.TabId == m_workspace.ActiveTabId);
                m_tabBindings[index].MoveLeftButton.SetEnabled(index > 0);
                m_tabBindings[index].MoveRightButton.SetEnabled(index < tabs.Count - 1);
            }
        }

        private void ClearTabBindings()
        {
            foreach (var binding in m_tabBindings)
            {
                binding.Button.clicked -= binding.Activate;
                binding.MoveLeftButton.clicked -= binding.MoveLeft;
                binding.MoveRightButton.clicked -= binding.MoveRight;
                binding.CloseButton.clicked -= binding.Close;
            }
            m_tabBindings.Clear();
        }

        private void CreateDatabase()
        {
            ChooseAndCreateDatabase();
        }

        private void OpenDatabase()
        {
            ChooseAndOpenDatabase();
        }

        private void RegisterSelectedDatabase()
        {
            ChooseAndRegisterDatabase();
        }

        internal void SaveSettings()
        {
            if (m_disposed)
            {
                return;
            }
            UpdateProjectSettings(m_exportPath.value, m_buildPath.value,
                m_selectedEnumTypeNames);
        }

        private void UndoActiveDocument()
        {
            m_collectionEditor.Cancel();
            m_workspace.UndoActiveDocument();
        }

        private void RedoActiveDocument()
        {
            m_collectionEditor.Cancel();
            m_workspace.RedoActiveDocument();
        }

        private void ReloadActiveDocument()
        {
            var active = m_workspace.ActiveTab;
            if (active == null)
            {
                return;
            }
            var state = active.Session.GetState();
            var expectedRevision = state.CurrentRevision;
            var discard = state.IsDirty || state.PersistenceStateUnknown;
            if (discard && !m_reloadPolicy.ConfirmDiscard(active.Session.AssetPath, state))
            {
                return;
            }
            m_collectionEditor.Cancel();
            m_workspace.ReloadActiveDocument(expectedRevision, discard);
        }

        private void SaveActiveDocument()
        {
            m_workspace.SaveActiveDocument();
        }

        internal void GenerateActiveDocument()
        {
            if (m_disposed)
            {
                return;
            }
            var settings = m_projectSettings.GetSnapshot();
            var active = m_workspace.ActiveTab;
            var result = m_outputService.Generate(active, settings.ExportPath);
            if (result.RequiresConfirmation && active != null
                && m_destructivePolicy.Confirm(new GameDBDestructiveActionRequest(null,
                    active.Session.AssetPath, "Replace Generated Code",
                    $"Replace the existing generated scope directory under '{result.OutputPath}'?",
                    "Replace"))
                && ReferenceEquals(active, m_workspace.ActiveTab) && !active.Session.IsDisposed)
            {
                result = m_outputService.Generate(active, settings.ExportPath, true);
            }
            m_outputMessage = result.Message;
            m_outputSucceeded = result.Success;
            Render();
        }

        internal void BuildActiveDocument()
        {
            if (m_disposed)
            {
                return;
            }
            var settings = m_projectSettings.GetSnapshot();
            var result = m_outputService.Build(m_workspace.ActiveTab, settings.BuildPath);
            m_outputMessage = result.Message;
            m_outputSucceeded = result.Success;
            Render();
        }

        private static IReadOnlyList<string> GetAvailableEnumTypeNames()
        {
            AssemblyExplorer.Instance.Load();
            return AssemblyExplorer.Instance.EnumTypes
                .Select(type => type.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.keyCode != KeyCode.Z)
            {
                return;
            }
            if (evt.shiftKey)
            {
                RedoActiveDocument();
            }
            else
            {
                UndoActiveDocument();
            }
            evt.StopPropagation();
        }

        private void OnProjectSettingsChanged(GameDBProjectSettingsChange change)
        {
            if (!m_disposed)
            {
                RenderSettings(new GameDBProjectSettingsResult(true, true,
                    change.Current, null), true);
                Render();
            }
        }

        private void RenderSettings(GameDBProjectSettingsResult result,
            bool preserveDraft = false)
        {
            preserveDraft = preserveDraft
                && m_settingsPanel.style.display.value == DisplayStyle.Flex;
            var draftExportPath = preserveDraft ? m_exportPath.value : null;
            var draftBuildPath = preserveDraft ? m_buildPath.value : null;
            var draftEnums = preserveDraft
                ? m_selectedEnumTypeNames.ToArray()
                : Array.Empty<string>();
            ClearSettingsPathBindings();
            m_registeredPaths.Clear();
            m_settingsValidationHost.Clear();
            var snapshot = result.Snapshot;
            m_settingsError.text = result.Success
                ? string.Join("\n", result.NotificationErrors)
                : result.Error ?? "GameDB project settings could not be loaded.";
            m_settingsError.style.display = string.IsNullOrWhiteSpace(m_settingsError.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (snapshot == null)
            {
                m_enumTypeNames = Array.Empty<string>();
                m_importedEnumTypes.itemsSource = null;
                m_exportPath.SetValueWithoutNotify(string.Empty);
                m_buildPath.SetValueWithoutNotify(string.Empty);
                return;
            }

            IReadOnlyList<string> availableEnums;
            try
            {
                availableEnums = m_availableEnumTypes() ?? Array.Empty<string>();
            }
            catch (Exception exception)
            {
                availableEnums = Array.Empty<string>();
                m_settingsError.text = string.IsNullOrWhiteSpace(m_settingsError.text)
                    ? "Imported enum discovery failed: " + exception.Message
                    : m_settingsError.text + "\nImported enum discovery failed: " + exception.Message;
                m_settingsError.style.display = DisplayStyle.Flex;
            }
            m_enumTypeNames = availableEnums.Concat(snapshot.ImportedEnumTypeNames)
                .Concat(draftEnums)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            m_selectedEnumTypeNames.Clear();
            foreach (var name in preserveDraft
                ? draftEnums
                : snapshot.ImportedEnumTypeNames)
            {
                m_selectedEnumTypeNames.Add(name);
            }
            m_importedEnumTypes.itemsSource = m_enumTypeNames.ToArray();
            m_importedEnumTypes.Rebuild();
            m_exportPath.SetValueWithoutNotify(preserveDraft
                ? draftExportPath : snapshot.ExportPath);
            m_buildPath.SetValueWithoutNotify(preserveDraft
                ? draftBuildPath : snapshot.BuildPath);
            foreach (var path in snapshot.RegisteredDatabasePaths)
            {
                var capturedPath = path;
                var row = new VisualElement();
                row.AddToClassList("gamedb-editor__registered-path-row");
                var label = new Label(path) { tooltip = path };
                label.AddToClassList("gamedb-editor__registered-path-label");
                Action remove = () => UnregisterDatabase(capturedPath);
                var removeButton = new Button(remove) { text = "Unregister" };
                row.Add(label);
                row.Add(removeButton);
                m_registeredPaths.Add(row);
                m_settingsPathBindings.Add(new SettingsPathBinding(removeButton, remove));
            }
            foreach (var issue in snapshot.ValidationIssues)
            {
                var message = issue.Kind == GameDBProjectSettingsIssueKind.MissingDatabasePath
                    ? $"Registered database is missing: {issue.Value}"
                    : $"Imported enum type could not be resolved: {issue.Value}";
                AddWarning(m_settingsValidationHost, message);
            }
        }

        private void ClearSettingsPathBindings()
        {
            foreach (var binding in m_settingsPathBindings)
            {
                binding.RemoveButton.clicked -= binding.Remove;
            }
            m_settingsPathBindings.Clear();
        }

        private void RenderWarnings(VisualElement host)
        {
            foreach (var issue in m_workspace.RecoveryIssues)
            {
                var message = issue.QuarantinePath == null
                    ? issue.Message
                    : issue.Message + " Quarantined at: " + issue.QuarantinePath;
                AddWarning(host, message);
            }
            if (m_workspace.LastDiskState != null
                && m_workspace.LastDiskState.State != GameDBDiskState.Unchanged)
            {
                AddWarning(host, m_workspace.LastDiskState.Message
                    ?? "The active GameDB files changed on disk.");
            }
            if (m_workspace.LastDiskRefresh != null
                && !m_workspace.LastDiskRefresh.Success)
            {
                AddWarning(host, m_workspace.LastDiskRefresh.Message
                    ?? "The active GameDB document could not be reloaded from disk.");
            }
            if (m_workspace.LastSaveOutcome != null
                && !m_workspace.LastSaveOutcome.Success)
            {
                AddWarning(host, m_workspace.LastSaveOutcome.Message
                    ?? "The active GameDB document could not be saved.");
            }
            if (!string.IsNullOrWhiteSpace(m_workspace.LastTabOperationError))
            {
                AddWarning(host, m_workspace.LastTabOperationError);
            }
            if (!string.IsNullOrWhiteSpace(m_playModeMessage))
            {
                AddWarning(host, m_playModeMessage);
            }
            if (!string.IsNullOrWhiteSpace(m_outputMessage))
            {
                AddMessage(host, m_outputMessage, m_outputSucceeded
                    ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
            }
        }

        private VisualElement CreateImportedEnumToggle()
        {
            var toggle = new Toggle();
            toggle.RegisterValueChangedCallback(OnImportedEnumChanged);
            return toggle;
        }

        private void BindImportedEnum(VisualElement element, int index)
        {
            var toggle = (Toggle)element;
            var name = m_enumTypeNames[index];
            toggle.text = name;
            toggle.SetValueWithoutNotify(m_selectedEnumTypeNames.Contains(name));
            toggle.userData = name;
        }

        private void OnImportedEnumChanged(ChangeEvent<bool> evt)
        {
            if (evt.currentTarget is Toggle toggle && toggle.userData is string name)
            {
                SetImportedEnumEnabled(name, evt.newValue);
            }
        }

        internal void SetImportedEnumEnabled(string name, bool enabled)
        {
            if (m_disposed || string.IsNullOrWhiteSpace(name)
                || !m_enumTypeNames.Contains(name, StringComparer.Ordinal))
            {
                return;
            }
            if (enabled)
            {
                m_selectedEnumTypeNames.Add(name);
            }
            else
            {
                m_selectedEnumTypeNames.Remove(name);
            }
        }

        private static void AddWarning(VisualElement host, string message)
        {
            AddMessage(host, message, HelpBoxMessageType.Warning);
        }

        private static void AddMessage(VisualElement host, string message,
            HelpBoxMessageType type)
        {
            var warning = new HelpBox(message, type);
            warning.AddToClassList("gamedb-editor__recovery-message");
            host.Add(warning);
        }

        private static void ClearRecoveryMessages(VisualElement host)
        {
            foreach (var stale in host.Query<VisualElement>(
                className: "gamedb-editor__recovery-message").ToList())
            {
                stale.RemoveFromHierarchy();
            }
        }

        private static ToolbarButton TabActionButton(string name, string text,
            string tooltip, Action clicked)
        {
            var button = new ToolbarButton(clicked)
            {
                name = name,
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("gamedb-editor__tab-action");
            return button;
        }

        private static string HistoryButtonText(string action, string label)
        {
            return string.IsNullOrWhiteSpace(label) ? action : action + " " + label;
        }

        private static string BuildTabText(GameDBEditorWorkspaceTab tab)
        {
            var name = Path.GetFileNameWithoutExtension(tab.Session.AssetPath);
            return tab.Session.GetState().IsDirty ? name + " *" : name;
        }

        private static string BuildDocumentSummary(string scopeName, int tableCount,
            GameDBDocumentSessionState state)
        {
            var scope = string.IsNullOrWhiteSpace(scopeName) ? "Unnamed scope" : scopeName;
            var status = state.PersistenceStateUnknown
                ? "Recovery required"
                : state.HasPendingPostSaveWork
                    ? "Post-save work pending"
                    : state.IsDirty ? "Unsaved changes" : "Saved";
            return $"{scope} • {tableCount} table{(tableCount == 1 ? string.Empty : "s")} • {status}";
        }
    }

    internal sealed class GameDBEditorTabCloseDialogPolicy : IGameDBTabClosePolicy
    {
        public GameDBTabCloseDecision Decide(GameDBTabCloseRequest request)
        {
            if (!request.CanSave)
            {
                return EditorUtility.DisplayDialog("Close GameDB Tab",
                    $"Persistence state for '{request.AssetPath}' is unknown. "
                    + "Discarding closes this recovered draft without modifying its database files.",
                    "Discard", "Cancel")
                    ? GameDBTabCloseDecision.Discard
                    : GameDBTabCloseDecision.Cancel;
            }

            var message = request.Reasons.HasFlag(GameDBTabCloseReason.PostSavePending)
                ? $"'{request.AssetPath}' has unfinished post-save work or unsaved changes."
                : $"'{request.AssetPath}' has unsaved changes.";
            var result = EditorUtility.DisplayDialogComplex("Close GameDB Tab",
                message, "Save", "Cancel", "Discard");
            return result == 0
                ? GameDBTabCloseDecision.Save
                : result == 2
                    ? GameDBTabCloseDecision.Discard
                    : GameDBTabCloseDecision.Cancel;
        }
    }
}
