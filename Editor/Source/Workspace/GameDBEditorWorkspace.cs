using GameDBEditorLibrary.Documents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace GameDBEditorLibrary.Workspace
{
    [Flags]
    internal enum GameDBTabCloseReason
    {
        None = 0,
        Dirty = 1,
        PostSavePending = 2,
        PersistenceUnknown = 4
    }

    internal enum GameDBTabCloseDecision
    {
        Save,
        Discard,
        Cancel
    }

    internal enum GameDBTabCloseStatus
    {
        Closed,
        NotFound,
        PolicyRequired,
        Cancelled,
        StateChanged,
        SaveFailed,
        RecoveryFailed,
        PlayModeBound
    }

    internal enum GameDBTabReorderStatus
    {
        Reordered,
        NoChange,
        NotFound,
        InvalidIndex,
        RecoveryFailed
    }

    internal enum GameDBWorkspaceDatabaseOpenStatus
    {
        Opened,
        ActivatedExisting,
        Busy,
        Invalid,
        RecoveryFailed
    }

    internal sealed class GameDBWorkspaceDatabaseOpenResult
    {
        internal GameDBWorkspaceDatabaseOpenStatus Status { get; }
        internal GameDBEditorWorkspaceTab Tab { get; }
        internal string AssetPath { get; }
        internal string Error { get; }
        internal GameDBWorkspaceRecoverySaveResult RecoveryOutcome { get; }
        internal bool Success => Status == GameDBWorkspaceDatabaseOpenStatus.Opened
            || Status == GameDBWorkspaceDatabaseOpenStatus.ActivatedExisting;

        internal GameDBWorkspaceDatabaseOpenResult(
            GameDBWorkspaceDatabaseOpenStatus status, GameDBEditorWorkspaceTab tab = null,
            string assetPath = null, string error = null,
            GameDBWorkspaceRecoverySaveResult recoveryOutcome = null)
        {
            Status = status;
            Tab = tab;
            AssetPath = assetPath;
            Error = error;
            RecoveryOutcome = recoveryOutcome;
        }
    }

    internal sealed class GameDBTabCloseRequest
    {
        internal string TabId { get; }
        internal string AssetPath { get; }
        internal GameDBDocumentSessionState State { get; }
        internal GameDBTabCloseReason Reasons { get; }
        internal bool CanSave => !Reasons.HasFlag(
            GameDBTabCloseReason.PersistenceUnknown);

        internal GameDBTabCloseRequest(string tabId, string assetPath,
            GameDBDocumentSessionState state, GameDBTabCloseReason reasons)
        {
            TabId = tabId;
            AssetPath = assetPath;
            State = state;
            Reasons = reasons;
        }
    }

    internal interface IGameDBTabClosePolicy
    {
        GameDBTabCloseDecision Decide(GameDBTabCloseRequest request);
    }

    internal sealed class GameDBTabCloseResult
    {
        internal GameDBTabCloseStatus Status { get; }
        internal GameDBSaveOutcome SaveOutcome { get; }
        internal GameDBWorkspaceRecoverySaveResult RecoveryOutcome { get; }
        internal bool Closed => Status == GameDBTabCloseStatus.Closed;
        internal bool SavedButNotClosed => SaveOutcome?.Success == true && !Closed;

        internal GameDBTabCloseResult(GameDBTabCloseStatus status,
            GameDBSaveOutcome saveOutcome = null,
            GameDBWorkspaceRecoverySaveResult recoveryOutcome = null)
        {
            Status = status;
            SaveOutcome = saveOutcome;
            RecoveryOutcome = recoveryOutcome;
        }
    }

    internal sealed class GameDBTabReorderResult
    {
        internal GameDBTabReorderStatus Status { get; }
        internal GameDBWorkspaceRecoverySaveResult RecoveryOutcome { get; }
        internal bool Changed => Status == GameDBTabReorderStatus.Reordered;

        internal GameDBTabReorderResult(GameDBTabReorderStatus status,
            GameDBWorkspaceRecoverySaveResult recoveryOutcome = null)
        {
            Status = status;
            RecoveryOutcome = recoveryOutcome;
        }
    }

    internal sealed class GameDBEditorWorkspaceTab
    {
        private readonly Action m_changed;
        private GameDBWorkspaceTabViewState m_viewState;
        private GameDBPlayModeBinding m_playModeBinding;
        private GameDBDocumentState m_prePlayModeState;

        internal string TabId { get; }
        internal GameDBAssetSession Session { get; }
        internal GameDBPlayModeBinding PlayModeBinding => m_playModeBinding;
        internal GameDBDocumentState PrePlayModeState => m_prePlayModeState;
        internal bool HasPlayModeState => m_prePlayModeState != null;
        internal GameDBWorkspaceTabViewState ViewState
        {
            get => m_viewState;
            set
            {
                m_viewState = value ?? new GameDBWorkspaceTabViewState();
                m_changed?.Invoke();
            }
        }

        internal GameDBEditorWorkspaceTab(string tabId, GameDBAssetSession session,
            GameDBWorkspaceTabViewState viewState = null, Action changed = null)
        {
            TabId = tabId ?? throw new ArgumentNullException(nameof(tabId));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            m_viewState = viewState ?? new GameDBWorkspaceTabViewState();
            m_changed = changed;
        }

        internal void SetViewState(GameDBWorkspaceTabViewState viewState,
            bool notifyPresentation)
        {
            m_viewState = viewState ?? throw new ArgumentNullException(nameof(viewState));
            if (notifyPresentation)
            {
                m_changed?.Invoke();
            }
        }

        internal void BeginPlayMode(GameDBDocumentState prePlayModeState,
            bool notifyPresentation = true)
        {
            if (m_prePlayModeState == null)
            {
                m_prePlayModeState = prePlayModeState
                    ?? throw new ArgumentNullException(nameof(prePlayModeState));
                Session.ResetHistory();
            }
            if (notifyPresentation)
            {
                m_changed?.Invoke();
            }
        }

        internal void SetPlayModeBinding(GameDBPlayModeBinding binding,
            bool notifyPresentation = true)
        {
            if (m_prePlayModeState == null)
            {
                throw new InvalidOperationException(
                    "Play Mode must begin before binding a runtime GameDB.");
            }
            m_playModeBinding = binding
                ?? throw new ArgumentNullException(nameof(binding));
            if (notifyPresentation)
            {
                m_changed?.Invoke();
            }
        }

        internal GameDBDocumentState ClearPlayModeBinding(
            bool notifyPresentation = true)
        {
            var prePlayModeState = m_prePlayModeState;
            m_playModeBinding = null;
            m_prePlayModeState = null;
            if (notifyPresentation)
            {
                m_changed?.Invoke();
            }
            return prePlayModeState;
        }
    }

    internal sealed class GameDBEditorWorkspace : IGameDBEditorFacadeTarget, IDisposable
    {
        private readonly GameDBDocumentLeaseRegistry m_leaseRegistry;
        private readonly GameDBWorkspaceRecoveryService m_recovery;
        private readonly List<GameDBEditorWorkspaceTab> m_tabs
            = new List<GameDBEditorWorkspaceTab>();
        private GameDBWorkspaceRegistration m_registration;
        private string m_activeTabId;
        private bool m_probeScheduled;
        private bool m_persisted;
        private bool m_disposed;
        private long m_topologyGeneration;
        private readonly List<GameDBWorkspaceRecoveryIssue> m_recoveryIssues
            = new List<GameDBWorkspaceRecoveryIssue>();

        internal string WorkspaceId { get; }
        internal IReadOnlyList<GameDBEditorWorkspaceTab> Tabs =>
            new ReadOnlyCollection<GameDBEditorWorkspaceTab>(m_tabs.ToArray());
        internal string ActiveTabId => m_activeTabId;
        internal GameDBEditorWorkspaceTab ActiveTab => GetActiveTab();
        internal bool IsDisposed => m_disposed;
        internal IReadOnlyList<GameDBWorkspaceRecoveryIssue> RecoveryIssues =>
            new ReadOnlyCollection<GameDBWorkspaceRecoveryIssue>(m_recoveryIssues.ToArray());
        internal GameDBDiskStateResult LastDiskState { get; private set; }
        internal GameDBDiskRefreshResult LastDiskRefresh { get; private set; }
        internal GameDBSaveOutcome LastSaveOutcome { get; private set; }
        internal string LastTabOperationError { get; private set; }
        internal event Action StateChanged;

        internal GameDBEditorWorkspace()
            : this(GameDBDocumentLeaseRegistry.Domain,
                GameDBWorkspaceRecoveryService.CreateDefault(),
                GameDBEditorDomainServices.ActiveWorkspaceHub)
        {
        }

        internal GameDBEditorWorkspace(GameDBDocumentLeaseRegistry leaseRegistry,
            GameDBWorkspaceRecoveryService recovery, GameDBActiveWorkspaceHub hub,
            string workspaceId = null)
        {
            m_leaseRegistry = leaseRegistry ?? throw new ArgumentNullException(nameof(leaseRegistry));
            m_recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            if (hub == null)
            {
                throw new ArgumentNullException(nameof(hub));
            }

            WorkspaceId = string.IsNullOrWhiteSpace(workspaceId)
                ? "workspace-" + Guid.NewGuid().ToString("N")
                : workspaceId;
            Restore();
            m_registration = hub.Register(this);
        }

        internal bool MarkFocused()
        {
            return !m_disposed && m_registration != null
                && m_registration.MarkFocused();
        }

        internal bool TrySetTabViewState(string tabId,
            GameDBWorkspaceTabViewState viewState, bool notifyPresentation = false)
        {
            ThrowIfDisposed();
            var tab = m_tabs.FirstOrDefault(candidate => candidate.TabId == tabId);
            if (tab == null || viewState == null)
            {
                return false;
            }

            tab.SetViewState(viewState, notifyPresentation);
            m_persisted = false;
            return true;
        }

        internal bool TryActivateTab(string tabId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(tabId))
            {
                return false;
            }

            var tab = m_tabs.FirstOrDefault(candidate => candidate.TabId == tabId);
            if (tab == null)
            {
                return false;
            }
            if (m_activeTabId == tab.TabId)
            {
                return true;
            }

            var recovery = SaveExactRecoverySnapshot(m_tabs, tab.TabId);
            if (!recovery.Success)
            {
                RecordTabOperationFailure(recovery);
                return false;
            }

            m_activeTabId = tab.TabId;
            LastDiskState = null;
            LastDiskRefresh = null;
            LastSaveOutcome = null;
            LastTabOperationError = null;
            m_persisted = true;
            NotifyStateChanged();
            RequestDiskProbe();
            return true;
        }

        internal GameDBTabCloseResult CloseTab(string tabId,
            IGameDBTabClosePolicy closePolicy)
        {
            ThrowIfDisposed();
            var index = m_tabs.FindIndex(tab => tab.TabId == tabId);
            if (index < 0)
            {
                return new GameDBTabCloseResult(GameDBTabCloseStatus.NotFound);
            }

            var tab = m_tabs[index];
            if (tab.HasPlayModeState)
            {
                return new GameDBTabCloseResult(GameDBTabCloseStatus.PlayModeBound);
            }
            var topologyGeneration = m_topologyGeneration;
            var state = tab.Session.GetState();
            var reasons = CloseReasons(state);
            GameDBSaveOutcome saveOutcome = null;
            if (reasons != GameDBTabCloseReason.None)
            {
                if (closePolicy == null)
                {
                    return new GameDBTabCloseResult(
                        GameDBTabCloseStatus.PolicyRequired);
                }
                var request = new GameDBTabCloseRequest(tab.TabId,
                    tab.Session.AssetPath, state, reasons);
                var decision = closePolicy.Decide(request);
                if (!request.State.Equals(tab.Session.GetState())
                    || !TopologyUnchanged(topologyGeneration, tabId, tab))
                {
                    return new GameDBTabCloseResult(
                        GameDBTabCloseStatus.StateChanged);
                }
                if (decision == GameDBTabCloseDecision.Cancel)
                {
                    return new GameDBTabCloseResult(GameDBTabCloseStatus.Cancelled);
                }
                if (decision == GameDBTabCloseDecision.Save)
                {
                    if (!request.CanSave)
                    {
                        return new GameDBTabCloseResult(
                            GameDBTabCloseStatus.SaveFailed);
                    }
                    saveOutcome = tab.Session.Save();
                    LastSaveOutcome = saveOutcome;
                    if (!saveOutcome.Success)
                    {
                        NotifyStateChanged();
                        return new GameDBTabCloseResult(
                            GameDBTabCloseStatus.SaveFailed, saveOutcome);
                    }
                    if (CloseReasons(tab.Session.GetState())
                        != GameDBTabCloseReason.None)
                    {
                        return new GameDBTabCloseResult(
                            GameDBTabCloseStatus.StateChanged, saveOutcome);
                    }
                }
            }

            if (!TopologyUnchanged(topologyGeneration, tabId, tab))
            {
                return new GameDBTabCloseResult(
                    GameDBTabCloseStatus.StateChanged, saveOutcome);
            }
            index = m_tabs.IndexOf(tab);
            var remaining = m_tabs.Where(candidate => !ReferenceEquals(candidate, tab))
                .ToArray();
            var nextActiveTabId = m_activeTabId == tab.TabId
                ? remaining.Length == 0
                    ? null
                    : remaining[Math.Min(index, remaining.Length - 1)].TabId
                : m_activeTabId;
            var recovery = SaveExactRecoverySnapshot(remaining, nextActiveTabId);
            if (!recovery.Success)
            {
                RecordTabOperationFailure(recovery);
                return new GameDBTabCloseResult(
                    GameDBTabCloseStatus.RecoveryFailed, saveOutcome, recovery);
            }

            m_tabs.RemoveAt(index);
            m_topologyGeneration++;
            Unsubscribe(tab.Session);
            tab.Session.Dispose();
            m_activeTabId = nextActiveTabId;
            LastDiskState = null;
            LastDiskRefresh = null;
            LastSaveOutcome = null;
            LastTabOperationError = null;
            m_persisted = true;
            NotifyStateChanged();
            if (m_activeTabId != null)
            {
                RequestDiskProbe();
            }
            return new GameDBTabCloseResult(
                GameDBTabCloseStatus.Closed, saveOutcome, recovery);
        }

        internal GameDBTabReorderResult ReorderTab(string tabId, int targetIndex)
        {
            ThrowIfDisposed();
            var sourceIndex = m_tabs.FindIndex(tab => tab.TabId == tabId);
            if (sourceIndex < 0)
            {
                return new GameDBTabReorderResult(GameDBTabReorderStatus.NotFound);
            }
            if (targetIndex < 0 || targetIndex >= m_tabs.Count)
            {
                return new GameDBTabReorderResult(
                    GameDBTabReorderStatus.InvalidIndex);
            }
            if (sourceIndex == targetIndex)
            {
                return new GameDBTabReorderResult(GameDBTabReorderStatus.NoChange);
            }

            var reordered = m_tabs.ToList();
            var tab = reordered[sourceIndex];
            reordered.RemoveAt(sourceIndex);
            reordered.Insert(targetIndex, tab);
            var recovery = SaveExactRecoverySnapshot(reordered, m_activeTabId);
            if (!recovery.Success)
            {
                RecordTabOperationFailure(recovery);
                return new GameDBTabReorderResult(
                    GameDBTabReorderStatus.RecoveryFailed, recovery);
            }

            m_tabs.Clear();
            m_tabs.AddRange(reordered);
            m_topologyGeneration++;
            LastTabOperationError = null;
            m_persisted = true;
            NotifyStateChanged();
            return new GameDBTabReorderResult(
                GameDBTabReorderStatus.Reordered, recovery);
        }

        internal void RequestDiskProbe()
        {
            ThrowIfDisposed();
            if (m_probeScheduled)
            {
                return;
            }

            m_probeScheduled = true;
            EditorApplication.delayCall += ProbeActiveDocument;
        }

        internal GameDBHistoryResult UndoActiveDocument()
        {
            return MoveActiveDocumentHistory(false);
        }

        internal GameDBHistoryResult RedoActiveDocument()
        {
            return MoveActiveDocumentHistory(true);
        }

        internal GameDBDiskRefreshResult ReloadActiveDocument(string expectedRevision,
            bool discardWorkingCopy)
        {
            ThrowIfDisposed();
            var active = GetActiveTab();
            if (active == null)
            {
                LastDiskRefresh = null;
                return null;
            }
            if (active.HasPlayModeState)
            {
                throw new InvalidOperationException(
                    "Disk reload is unavailable while editing a runtime GameDB.");
            }

            LastDiskRefresh = active.Session.RefreshFromDisk(
                expectedRevision, discardWorkingCopy);
            LastDiskState = active.Session.ProbeDiskState();
            if (LastDiskRefresh.Success)
            {
                m_persisted = false;
            }
            NotifyStateChanged();
            return LastDiskRefresh;
        }

        internal GameDBWorkspaceRecoverySaveResult PersistRecovery()
        {
            ThrowIfDisposed();
            if (m_persisted)
            {
                return new GameDBWorkspaceRecoverySaveResult(true);
            }

            var result = SaveRecoverySnapshot(m_tabs, m_activeTabId);
            AddRecoveryIssues(result.Issues);
            if (result.Success)
            {
                m_persisted = true;
            }
            else if (!string.IsNullOrWhiteSpace(result.Error))
            {
                m_recoveryIssues.Add(new GameDBWorkspaceRecoveryIssue(null,
                    result.Error, null));
            }
            return result;
        }

        internal GameDBWorkspaceDatabaseOpenResult TryOpenDatabase(string assetPath)
        {
            return TryAddDatabase(assetPath, (path, tabId) =>
                GameDBAssetSession.TryOpen(m_leaseRegistry, path, tabId));
        }

        internal bool TryGetRegisteredDatabasePath(string assetPath,
            out string registeredPath, out string error)
        {
            ThrowIfDisposed();
            try
            {
                registeredPath = m_leaseRegistry.PairStore.Resolve(assetPath).RelativePath;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                registeredPath = null;
                error = exception.Message;
                return false;
            }
        }

        internal GameDBWorkspaceDatabaseOpenResult TryCreateDatabase(string assetPath,
            string scopeName, bool localization)
        {
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                return InvalidDatabaseOperation(assetPath,
                    "A database scope name is required.");
            }

            return TryAddDatabase(assetPath, (path, tabId) =>
                GameDBAssetSession.TryCreateNew(m_leaseRegistry, path,
                    scopeName.Trim(), localization, tabId));
        }

        public bool LoadGameDB(string gameDBPath)
        {
            return TryOpenDatabase(ToAssetPath(gameDBPath)).Success;
        }

        public bool SaveGameDB()
        {
            return SaveActiveDocument(true)?.Success ?? false;
        }

        internal GameDBSaveOutcome SaveActiveDocument()
        {
            return SaveActiveDocument(false);
        }

        public void AddRowToTable(string table, string key,
            Dictionary<string, object> data)
        {
            ThrowIfDisposed();
            var active = GetActiveTab()
                ?? throw new InvalidOperationException("No active GameDB document.");
            var snapshot = active.Session.CreateSnapshot();
            var tableSnapshot = snapshot.Tables.FirstOrDefault(candidate => candidate.Name == table);
            if (tableSnapshot == null)
            {
                throw new ArgumentOutOfRangeException(nameof(table), table,
                    "No table found in GameDB");
            }
            if (tableSnapshot.Rows.Any(row => row.Key == key))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key,
                    $"Key already exists in {table} Table");
            }
            foreach (var fieldName in data.Keys)
            {
                if (!tableSnapshot.Fields.Any(field => field.Name == fieldName))
                {
                    throw new ArgumentOutOfRangeException("Field", fieldName,
                        $"No field exists in {table} Table");
                }
            }

            var result = active.Session.ApplyTransaction(new GameDBCommand[]
            {
                new AddRowCommand(table, key, data)
            });
            if (!result.Success)
            {
                throw new InvalidCastException(result.Message);
            }
            m_persisted = false;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            GameDBWorkspaceRecoverySaveResult persisted = null;
            try
            {
                if (!m_persisted)
                {
                    persisted = PersistRecovery();
                }
            }
            finally
            {
                m_disposed = true;
                EditorApplication.delayCall -= ProbeActiveDocument;
                m_probeScheduled = false;
                m_registration?.Dispose();
                m_registration = null;
                foreach (var tab in m_tabs)
                {
                    Unsubscribe(tab.Session);
                    tab.Session.Dispose();
                }
                m_tabs.Clear();
                m_activeTabId = null;
            }

            if (persisted != null && !persisted.Success)
            {
                throw new InvalidOperationException(persisted.Error
                    ?? "Failed to persist GameDB workspace recovery.");
            }
        }

        private void Restore()
        {
            var loaded = m_recovery.Load();
            AddRecoveryIssues(loaded.Issues);
            if (!loaded.Success && !string.IsNullOrWhiteSpace(loaded.Error))
            {
                m_recoveryIssues.Add(new GameDBWorkspaceRecoveryIssue(null,
                    loaded.Error, loaded.QuarantinePath));
            }
            var restored = m_recovery.RestoreAssetSessions(loaded.Snapshot, m_leaseRegistry);
            AddRecoveryIssues(restored.Issues);
            foreach (var tab in restored.Tabs)
            {
                AddTab(new GameDBEditorWorkspaceTab(tab.TabId, tab.Session,
                    tab.ViewState));
            }
            m_activeTabId = restored.ActiveTabId;
            m_persisted = false;
        }

        internal void ProbeActiveDocument()
        {
            m_probeScheduled = false;
            if (m_disposed)
            {
                return;
            }

            var active = GetActiveTab();
            if (active?.HasPlayModeState == true)
            {
                LastDiskRefresh = null;
                return;
            }
            var expectedRevision = active?.Session.GetState().CurrentRevision;
            var next = active?.Session.ProbeDiskState();
            var refreshed = false;
            if (active != null && next?.State == GameDBDiskState.Modified)
            {
                LastDiskRefresh = active.Session.RefreshFromDisk(expectedRevision, false);
                next = active.Session.ProbeDiskState();
                refreshed = LastDiskRefresh.Success;
                if (refreshed)
                {
                    m_persisted = false;
                }
            }
            else
            {
                LastDiskRefresh = null;
            }
            if (!SameDiskState(LastDiskState, next) || refreshed)
            {
                LastDiskState = next;
                NotifyStateChanged();
            }
        }

        private GameDBWorkspaceDatabaseOpenResult TryAddDatabase(string assetPath,
            Func<string, string, GameDBAssetSessionOpenResult> open)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return InvalidDatabaseOperation(assetPath,
                    "A database path is required.");
            }

            try
            {
                var existing = m_tabs.FirstOrDefault(tab =>
                    m_leaseRegistry.RefersToSameAsset(tab.Session.AssetPath, assetPath));
                if (existing != null)
                {
                    if (existing.TabId != m_activeTabId)
                    {
                        var activationRecovery = SaveExactRecoverySnapshot(m_tabs, existing.TabId);
                        if (!activationRecovery.Success)
                        {
                            RecordTabOperationFailure(activationRecovery);
                            return new GameDBWorkspaceDatabaseOpenResult(
                                GameDBWorkspaceDatabaseOpenStatus.RecoveryFailed,
                                existing, existing.Session.AssetPath,
                                LastTabOperationError, activationRecovery);
                        }
                        m_activeTabId = existing.TabId;
                        m_persisted = true;
                        LastTabOperationError = null;
                        NotifyStateChanged();
                        RequestDiskProbe();
                    }
                    return new GameDBWorkspaceDatabaseOpenResult(
                        GameDBWorkspaceDatabaseOpenStatus.ActivatedExisting,
                        existing, existing.Session.AssetPath);
                }

                var tabId = "tab-" + Guid.NewGuid().ToString("N");
                var opened = open(assetPath, tabId);
                if (opened.Status == GameDBAssetSessionOpenStatus.Busy)
                {
                    LastTabOperationError =
                        $"'{opened.CanonicalAssetPath}' is already open in another workspace.";
                    NotifyStateChanged();
                    return new GameDBWorkspaceDatabaseOpenResult(
                        GameDBWorkspaceDatabaseOpenStatus.Busy, null,
                        opened.CanonicalAssetPath, LastTabOperationError);
                }

                var tab = new GameDBEditorWorkspaceTab(tabId, opened.Session);
                var topologyRecovery = SaveExactRecoverySnapshot(
                    m_tabs.Concat(new[] { tab }), tabId);
                if (!topologyRecovery.Success)
                {
                    opened.Session.Dispose();
                    RecordTabOperationFailure(topologyRecovery);
                    return new GameDBWorkspaceDatabaseOpenResult(
                        GameDBWorkspaceDatabaseOpenStatus.RecoveryFailed, null,
                        opened.CanonicalAssetPath, LastTabOperationError, topologyRecovery);
                }

                AddTab(tab);
                m_topologyGeneration++;
                m_activeTabId = tabId;
                LastDiskState = null;
                LastDiskRefresh = null;
                LastSaveOutcome = null;
                LastTabOperationError = null;
                m_persisted = true;
                NotifyStateChanged();
                RequestDiskProbe();
                return new GameDBWorkspaceDatabaseOpenResult(
                    GameDBWorkspaceDatabaseOpenStatus.Opened,
                    m_tabs[m_tabs.Count - 1], opened.CanonicalAssetPath,
                    recoveryOutcome: topologyRecovery);
            }
            catch (Exception exception)
            {
                return InvalidDatabaseOperation(assetPath, exception.Message);
            }
        }

        private GameDBWorkspaceDatabaseOpenResult InvalidDatabaseOperation(
            string assetPath, string error)
        {
            LastTabOperationError = error;
            NotifyStateChanged();
            return new GameDBWorkspaceDatabaseOpenResult(
                GameDBWorkspaceDatabaseOpenStatus.Invalid,
                assetPath: assetPath, error: error);
        }

        private void AddTab(GameDBEditorWorkspaceTab tab)
        {
            tab.Session.Changed += OnSessionChanged;
            tab.Session.StateChanged += OnSessionStateChanged;
            m_tabs.Add(new GameDBEditorWorkspaceTab(tab.TabId, tab.Session,
                tab.ViewState, MarkRecoveryDirty));
        }

        private void Unsubscribe(GameDBAssetSession session)
        {
            session.Changed -= OnSessionChanged;
            session.StateChanged -= OnSessionStateChanged;
        }

        private void OnSessionChanged(GameDBDocumentChange change)
        {
            LastSaveOutcome = null;
            MarkRecoveryDirty();
        }

        private void OnSessionStateChanged(GameDBDocumentStateChange change)
        {
            LastSaveOutcome = null;
            MarkRecoveryDirty();
        }

        private GameDBWorkspaceRecoverySaveResult SaveRecoverySnapshot(
            IEnumerable<GameDBEditorWorkspaceTab> tabs, string activeTabId)
        {
            var snapshot = new GameDBWorkspaceRecoverySnapshot(tabs.Select(tab =>
                new GameDBWorkspaceRecoveryTab(tab.TabId,
                    tab.PrePlayModeState ?? tab.Session.CaptureState(), tab.ViewState)), activeTabId);
            return m_recovery.Save(snapshot);
        }

        private GameDBWorkspaceRecoverySaveResult SaveExactRecoverySnapshot(
            IEnumerable<GameDBEditorWorkspaceTab> tabs, string activeTabId)
        {
            var snapshot = new GameDBWorkspaceRecoverySnapshot(tabs.Select(tab =>
                new GameDBWorkspaceRecoveryTab(tab.TabId,
                    tab.PrePlayModeState ?? tab.Session.CaptureState(), tab.ViewState)), activeTabId);
            return m_recovery.SaveExact(snapshot);
        }

        private bool TopologyUnchanged(long generation, string tabId,
            GameDBEditorWorkspaceTab tab)
        {
            return m_topologyGeneration == generation
                && ReferenceEquals(m_tabs.FirstOrDefault(candidate =>
                    candidate.TabId == tabId), tab);
        }

        private void RecordTabOperationFailure(
            GameDBWorkspaceRecoverySaveResult recovery)
        {
            AddRecoveryIssues(recovery.Issues);
            LastTabOperationError = recovery.Error
                ?? "The workspace tab change could not be persisted.";
            NotifyStateChanged();
        }

        private static GameDBTabCloseReason CloseReasons(
            GameDBDocumentSessionState state)
        {
            var reasons = GameDBTabCloseReason.None;
            if (state.IsDirty)
            {
                reasons |= GameDBTabCloseReason.Dirty;
            }
            if (state.HasPendingPostSaveWork)
            {
                reasons |= GameDBTabCloseReason.PostSavePending;
            }
            if (state.PersistenceStateUnknown)
            {
                reasons |= GameDBTabCloseReason.PersistenceUnknown;
            }
            return reasons;
        }

        private GameDBHistoryResult MoveActiveDocumentHistory(bool redo)
        {
            ThrowIfDisposed();
            var active = GetActiveTab();
            if (active == null)
            {
                return null;
            }

            var result = redo ? active.Session.Redo() : active.Session.Undo();
            if (result.Success)
            {
                LastDiskRefresh = null;
                m_persisted = false;
            }
            NotifyStateChanged();
            return result;
        }

        private GameDBSaveOutcome SaveActiveDocument(bool forceWrite)
        {
            ThrowIfDisposed();
            var active = GetActiveTab();
            if (active == null)
            {
                LastSaveOutcome = null;
                return null;
            }
            if (active.HasPlayModeState)
            {
                throw new InvalidOperationException(
                    "Disk save is unavailable while editing a runtime GameDB. Use Reload In-Game.");
            }

            LastSaveOutcome = active.Session.Save(
                new GameDBSaveOptions { ForceWrite = forceWrite });
            if (LastSaveOutcome.Success)
            {
                m_persisted = false;
            }
            NotifyStateChanged();
            return LastSaveOutcome;
        }

        private void AddRecoveryIssues(IEnumerable<GameDBWorkspaceRecoveryIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (!m_recoveryIssues.Any(existing => existing.TabId == issue.TabId
                    && existing.Message == issue.Message
                    && existing.QuarantinePath == issue.QuarantinePath))
                {
                    m_recoveryIssues.Add(issue);
                }
            }
        }

        private void MarkRecoveryDirty()
        {
            m_persisted = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            var subscribers = StateChanged;
            if (subscribers == null)
            {
                return;
            }

            foreach (Action subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private GameDBEditorWorkspaceTab GetActiveTab()
        {
            return m_tabs.FirstOrDefault(tab => tab.TabId == m_activeTabId);
        }

        private static bool SameDiskState(GameDBDiskStateResult first,
            GameDBDiskStateResult second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            return first != null && second != null
                && first.State == second.State
                && first.Message == second.Message
                && first.ObservedToken == second.ObservedToken
                && first.RecoveryArtifacts.SequenceEqual(second.RecoveryArtifacts);
        }

        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("GameDB path is required.", nameof(path));
            }
            var assetPath = path.Replace('\\', '/').TrimStart('/');
            return assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? assetPath
                : "Assets/" + assetPath;
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(GameDBEditorWorkspace));
            }
        }
    }
}
