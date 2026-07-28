using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameDBEditorLibrary.Workspace
{
    internal sealed class GameDBWorkspaceSortState
    {
        internal string FieldId { get; }
        internal bool Descending { get; }

        internal GameDBWorkspaceSortState(string fieldId, bool descending)
        {
            FieldId = fieldId ?? string.Empty;
            Descending = descending;
        }
    }

    internal sealed class GameDBWorkspaceColumnState
    {
        internal string TableId { get; }
        internal string FieldId { get; }
        internal float Width { get; }
        internal int Order { get; }

        internal GameDBWorkspaceColumnState(string fieldId, float width, int order,
            string tableId = null)
        {
            TableId = tableId;
            FieldId = fieldId ?? string.Empty;
            Width = width;
            Order = order;
        }
    }

    internal sealed class GameDBWorkspaceTabViewState
    {
        internal string SelectedTableId { get; }
        internal string SelectedRowId { get; }
        internal string SearchText { get; }
        internal IReadOnlyList<GameDBWorkspaceSortState> Sorts { get; }
        internal IReadOnlyList<GameDBWorkspaceColumnState> Columns { get; }
        internal float HorizontalScroll { get; }
        internal float VerticalScroll { get; }

        internal GameDBWorkspaceTabViewState(string selectedTableId = null,
            string selectedRowId = null, string searchText = null,
            IEnumerable<GameDBWorkspaceSortState> sorts = null,
            IEnumerable<GameDBWorkspaceColumnState> columns = null,
            float horizontalScroll = 0f, float verticalScroll = 0f)
        {
            var sortValues = (sorts ?? Array.Empty<GameDBWorkspaceSortState>()).ToArray();
            var columnValues = (columns ?? Array.Empty<GameDBWorkspaceColumnState>()).ToArray();
            if (sortValues.Any(sort => sort == null))
            {
                throw new ArgumentException("Sort states cannot contain null values.", nameof(sorts));
            }
            if (columnValues.Any(column => column == null))
            {
                throw new ArgumentException("Column states cannot contain null values.", nameof(columns));
            }
            if (!IsFinite(horizontalScroll) || !IsFinite(verticalScroll)
                || columnValues.Any(column => !IsFinite(column.Width)))
            {
                throw new ArgumentOutOfRangeException(nameof(columns),
                    "Recovery layout values must be finite.");
            }

            SelectedTableId = selectedTableId;
            SelectedRowId = selectedRowId;
            SearchText = searchText ?? string.Empty;
            Sorts = new ReadOnlyCollection<GameDBWorkspaceSortState>(sortValues
                .Select(sort => new GameDBWorkspaceSortState(sort.FieldId, sort.Descending))
                .ToArray());
            Columns = new ReadOnlyCollection<GameDBWorkspaceColumnState>(columnValues
                .Select(column => new GameDBWorkspaceColumnState(
                    column.FieldId, column.Width, column.Order, column.TableId))
                .ToArray());
            HorizontalScroll = horizontalScroll;
            VerticalScroll = verticalScroll;
        }

        internal bool HasSameValues(GameDBWorkspaceTabViewState other)
        {
            return other != null
                && SelectedTableId == other.SelectedTableId
                && SelectedRowId == other.SelectedRowId
                && SearchText == other.SearchText
                && HorizontalScroll == other.HorizontalScroll
                && VerticalScroll == other.VerticalScroll
                && Sorts.Select(sort => new { sort.FieldId, sort.Descending })
                    .SequenceEqual(other.Sorts.Select(sort =>
                        new { sort.FieldId, sort.Descending }))
                && Columns.Select(column => new
                {
                    column.TableId,
                    column.FieldId,
                    column.Width,
                    column.Order
                }).SequenceEqual(other.Columns.Select(column => new
                {
                    column.TableId,
                    column.FieldId,
                    column.Width,
                    column.Order
                }));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class GameDBWorkspaceRecoveryTab
    {
        private readonly GameDBDocumentState m_documentState;

        internal string TabId { get; }
        internal GameDBDocumentState DocumentState => CopyDocumentState(m_documentState);
        internal GameDBWorkspaceTabViewState ViewState { get; }

        internal GameDBWorkspaceRecoveryTab(string tabId, GameDBDocumentState documentState,
            GameDBWorkspaceTabViewState viewState = null)
        {
            if (string.IsNullOrWhiteSpace(tabId))
            {
                throw new ArgumentException("Recovery tab identity is required.", nameof(tabId));
            }

            TabId = tabId;
            m_documentState = CopyDocumentState(documentState
                ?? throw new ArgumentNullException(nameof(documentState)));
            ViewState = viewState ?? new GameDBWorkspaceTabViewState();
        }

        internal static GameDBDocumentState CopyDocumentState(GameDBDocumentState state)
        {
            return new GameDBDocumentState
            {
                Version = state.Version,
                DocumentId = state.DocumentId,
                AssetPath = state.AssetPath,
                DataJson = state.DataJson,
                SchemaJson = state.SchemaJson,
                BaselineRevision = state.BaselineRevision,
                BaselineDiskToken = state.BaselineDiskToken,
                DataImportPending = state.DataImportPending,
                SchemaImportPending = state.SchemaImportPending,
                CallbackPending = state.CallbackPending,
                PendingScopeName = state.PendingScopeName,
                PersistenceStateUnknown = state.PersistenceStateUnknown,
                WasDirty = state.WasDirty
            };
        }
    }

    internal sealed class GameDBWorkspaceRecoverySnapshot
    {
        internal const int CurrentVersion = 1;

        internal int Version { get; }
        internal string ActiveTabId { get; }
        internal IReadOnlyList<GameDBWorkspaceRecoveryTab> Tabs { get; }

        internal GameDBWorkspaceRecoverySnapshot(
            IEnumerable<GameDBWorkspaceRecoveryTab> tabs, string activeTabId = null,
            int version = CurrentVersion)
        {
            Version = version;
            var tabValues = (tabs ?? Array.Empty<GameDBWorkspaceRecoveryTab>()).ToArray();
            if (tabValues.Any(tab => tab == null))
            {
                throw new ArgumentException("Recovery tabs cannot contain null values.", nameof(tabs));
            }
            var copies = tabValues.Select(tab => new GameDBWorkspaceRecoveryTab(
                    tab.TabId, tab.DocumentState, tab.ViewState))
                .ToArray();
            if (copies.Select(tab => tab.TabId).Distinct(StringComparer.Ordinal).Count()
                != copies.Length)
            {
                throw new ArgumentException("Recovery tab identities must be unique.", nameof(tabs));
            }

            Tabs = new ReadOnlyCollection<GameDBWorkspaceRecoveryTab>(copies);
            ActiveTabId = copies.Any(tab => tab.TabId == activeTabId) ? activeTabId : null;
        }
    }

    internal sealed class GameDBWorkspaceRecoveryIssue
    {
        internal string TabId { get; }
        internal string Message { get; }
        internal string QuarantinePath { get; }

        internal GameDBWorkspaceRecoveryIssue(string tabId, string message,
            string quarantinePath)
        {
            TabId = tabId;
            Message = message;
            QuarantinePath = quarantinePath;
        }
    }

    internal sealed class GameDBWorkspaceRecoveryLoadResult
    {
        internal bool Success { get; }
        internal GameDBWorkspaceRecoverySnapshot Snapshot { get; }
        internal string Error { get; }
        internal string QuarantinePath { get; }
        internal IReadOnlyList<GameDBWorkspaceRecoveryIssue> Issues { get; }

        internal GameDBWorkspaceRecoveryLoadResult(bool success,
            GameDBWorkspaceRecoverySnapshot snapshot, string error = null,
            string quarantinePath = null,
            IEnumerable<GameDBWorkspaceRecoveryIssue> issues = null)
        {
            Success = success;
            Snapshot = snapshot;
            Error = error;
            QuarantinePath = quarantinePath;
            Issues = new ReadOnlyCollection<GameDBWorkspaceRecoveryIssue>(
                (issues ?? Array.Empty<GameDBWorkspaceRecoveryIssue>()).ToArray());
        }
    }

    internal sealed class GameDBWorkspaceRecoverySaveResult
    {
        internal bool Success { get; }
        internal string Error { get; }
        internal IReadOnlyList<GameDBWorkspaceRecoveryIssue> Issues { get; }

        internal GameDBWorkspaceRecoverySaveResult(bool success, string error = null,
            IEnumerable<GameDBWorkspaceRecoveryIssue> issues = null)
        {
            Success = success;
            Error = error;
            Issues = new ReadOnlyCollection<GameDBWorkspaceRecoveryIssue>(
                (issues ?? Array.Empty<GameDBWorkspaceRecoveryIssue>()).ToArray());
        }
    }

    internal sealed class GameDBWorkspaceRestoredTab
    {
        internal string TabId { get; }
        internal GameDBAssetSession Session { get; }
        internal GameDBWorkspaceTabViewState ViewState { get; }

        internal GameDBWorkspaceRestoredTab(string tabId, GameDBAssetSession session,
            GameDBWorkspaceTabViewState viewState)
        {
            TabId = tabId;
            Session = session;
            ViewState = viewState;
        }
    }

    internal sealed class GameDBWorkspaceRestoreResult
    {
        internal string ActiveTabId { get; }
        internal IReadOnlyList<GameDBWorkspaceRestoredTab> Tabs { get; }
        internal IReadOnlyList<GameDBWorkspaceRecoveryIssue> Issues { get; }

        internal GameDBWorkspaceRestoreResult(string activeTabId,
            IEnumerable<GameDBWorkspaceRestoredTab> tabs,
            IEnumerable<GameDBWorkspaceRecoveryIssue> issues)
        {
            var restored = (tabs ?? Array.Empty<GameDBWorkspaceRestoredTab>()).ToArray();
            ActiveTabId = restored.Any(tab => tab.TabId == activeTabId)
                ? activeTabId
                : restored.FirstOrDefault()?.TabId;
            Tabs = new ReadOnlyCollection<GameDBWorkspaceRestoredTab>(restored);
            Issues = new ReadOnlyCollection<GameDBWorkspaceRecoveryIssue>(
                (issues ?? Array.Empty<GameDBWorkspaceRecoveryIssue>()).ToArray());
        }
    }

    internal interface IGameDBWorkspaceRecoveryStore
    {
        bool Exists { get; }
        string ReadAllText();
        void WriteAtomically(string contents);
        string QuarantinePrimary();
        string WriteQuarantine(string label, string contents);
    }

    internal sealed class GameDBWorkspaceRecoveryFileStore : IGameDBWorkspaceRecoveryStore
    {
        private readonly string m_path;

        internal GameDBWorkspaceRecoveryFileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Recovery path is required.", nameof(path));
            }

            m_path = Path.GetFullPath(path);
        }

        public bool Exists => File.Exists(m_path);

        public string ReadAllText()
        {
            return File.ReadAllText(m_path);
        }

        public void WriteAtomically(string contents)
        {
            WriteAtomic(m_path, contents);
        }

        public string QuarantinePrimary()
        {
            if (!File.Exists(m_path))
            {
                return null;
            }

            var quarantinePath = CreateQuarantinePath("workspace");
            Directory.CreateDirectory(Path.GetDirectoryName(m_path));
            File.Move(m_path, quarantinePath);
            return quarantinePath;
        }

        public string WriteQuarantine(string label, string contents)
        {
            var quarantinePath = CreateQuarantinePath(label);
            WriteAtomic(quarantinePath, contents);
            return quarantinePath;
        }

        private string CreateQuarantinePath(string label)
        {
            var safeLabel = new string((label ?? "recovery")
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray()).Trim('-');
            if (string.IsNullOrEmpty(safeLabel))
            {
                safeLabel = "recovery";
            }

            return m_path + "." + safeLabel + ".quarantine."
                + Guid.NewGuid().ToString("N") + ".json";
        }

        private static void WriteAtomic(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var operationId = Guid.NewGuid().ToString("N");
            var temporaryPath = path + "." + operationId + ".tmp";
            var backupPath = path + "." + operationId + ".bak";
            var destinationExisted = File.Exists(path);
            try
            {
                File.WriteAllText(temporaryPath, contents);
                if (destinationExisted)
                {
                    File.Replace(temporaryPath, path, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
                TryDelete(backupPath);
            }
            catch
            {
                if (destinationExisted && !File.Exists(path) && File.Exists(backupPath))
                {
                    File.Move(backupPath, path);
                }
                throw;
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class GameDBWorkspaceRecoveryService
    {
        private readonly IGameDBWorkspaceRecoveryStore m_store;

        internal GameDBWorkspaceRecoveryService(IGameDBWorkspaceRecoveryStore store)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
        }

        internal static GameDBWorkspaceRecoveryService CreateDefault()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "Library", "GameDB", "WorkspaceRecovery.json"));
            return new GameDBWorkspaceRecoveryService(
                new GameDBWorkspaceRecoveryFileStore(path));
        }

        internal GameDBWorkspaceRecoverySaveResult Save(
            GameDBWorkspaceRecoverySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Version != GameDBWorkspaceRecoverySnapshot.CurrentVersion)
            {
                return new GameDBWorkspaceRecoverySaveResult(false,
                    $"Unsupported workspace recovery version: {snapshot.Version}.");
            }

            var issues = new List<GameDBWorkspaceRecoveryIssue>();
            var validTabs = new List<GameDBWorkspaceRecoveryTab>();
            foreach (var tab in snapshot.Tabs)
            {
                try
                {
                    ValidateDocumentState(tab.DocumentState);
                    SerializeTab(tab);
                    validTabs.Add(tab);
                }
                catch (Exception exception)
                {
                    issues.Add(QuarantineRecoveryTab(tab, exception.Message));
                }
            }

            var activeTabId = validTabs.Any(tab => tab.TabId == snapshot.ActiveTabId)
                ? snapshot.ActiveTabId
                : null;
            var validSnapshot = new GameDBWorkspaceRecoverySnapshot(validTabs, activeTabId);
            try
            {
                m_store.WriteAtomically(SerializeSnapshot(validSnapshot));
                return new GameDBWorkspaceRecoverySaveResult(true, issues: issues);
            }
            catch (Exception exception)
            {
                return new GameDBWorkspaceRecoverySaveResult(false,
                    $"Failed to save GameDB workspace recovery: {exception.Message}", issues);
            }
        }

        internal GameDBWorkspaceRecoverySaveResult SaveExact(
            GameDBWorkspaceRecoverySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (snapshot.Version != GameDBWorkspaceRecoverySnapshot.CurrentVersion)
            {
                return new GameDBWorkspaceRecoverySaveResult(false,
                    $"Unsupported workspace recovery version: {snapshot.Version}.");
            }

            try
            {
                foreach (var tab in snapshot.Tabs)
                {
                    ValidateDocumentState(tab.DocumentState);
                    SerializeTab(tab);
                }
                m_store.WriteAtomically(SerializeSnapshot(snapshot));
                return new GameDBWorkspaceRecoverySaveResult(true);
            }
            catch (Exception exception)
            {
                return new GameDBWorkspaceRecoverySaveResult(false,
                    $"Failed to save exact GameDB workspace recovery: {exception.Message}");
            }
        }

        internal GameDBWorkspaceRecoveryLoadResult Load()
        {
            if (!m_store.Exists)
            {
                return new GameDBWorkspaceRecoveryLoadResult(true,
                    new GameDBWorkspaceRecoverySnapshot(Array.Empty<GameDBWorkspaceRecoveryTab>()));
            }

            string contents;
            try
            {
                contents = m_store.ReadAllText();
            }
            catch (Exception exception)
            {
                return QuarantineFailedPayload(
                    $"Failed to read GameDB workspace recovery: {exception.Message}");
            }

            IDictionary<string, object> root;
            IReadOnlyList<object> tabValues;
            string activeTabId;
            try
            {
                root = RequireObject(JsonSerialization.Deserialize(contents), "workspace recovery");
                var version = RequireInt(root, "version");
                if (version != GameDBWorkspaceRecoverySnapshot.CurrentVersion)
                {
                    throw new FormatException($"Unsupported workspace recovery version: {version}.");
                }
                tabValues = ReadArray(root, "tabs");
                activeTabId = ReadOptionalString(root, "activeTabId");
            }
            catch (Exception exception)
            {
                return QuarantineFailedPayload(
                    $"Failed to load GameDB workspace recovery: {exception.Message}");
            }

            var issues = new List<GameDBWorkspaceRecoveryIssue>();
            var tabs = new List<GameDBWorkspaceRecoveryTab>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var malformedTabs = 0;
            var allMalformedTabsQuarantined = true;
            for (var index = 0; index < tabValues.Count; index++)
            {
                var tabValue = tabValues[index];
                var tabId = TryReadTabId(tabValue) ?? $"tab-{index + 1}";
                try
                {
                    var tab = ParseTab(RequireObject(tabValue, $"tab {index + 1}"));
                    if (!ids.Add(tab.TabId))
                    {
                        throw new FormatException($"Duplicate recovery tab identity: {tab.TabId}.");
                    }
                    tabs.Add(tab);
                }
                catch (Exception exception)
                {
                    malformedTabs++;
                    var issue = QuarantineTab(tabId, tabValue, exception.Message);
                    allMalformedTabsQuarantined &= issue.QuarantinePath != null;
                    issues.Add(issue);
                }
            }

            if (activeTabId != null && !tabs.Any(tab => tab.TabId == activeTabId))
            {
                issues.Add(new GameDBWorkspaceRecoveryIssue(activeTabId,
                    "The active recovery tab was missing or could not be restored.", null));
                activeTabId = null;
            }

            var snapshot = new GameDBWorkspaceRecoverySnapshot(tabs, activeTabId);
            if (malformedTabs > 0 && allMalformedTabsQuarantined)
            {
                try
                {
                    m_store.WriteAtomically(SerializeSnapshot(snapshot));
                }
                catch (Exception exception)
                {
                    issues.Add(new GameDBWorkspaceRecoveryIssue(null,
                        $"Failed to rewrite GameDB workspace recovery after quarantine: {exception.Message}",
                        null));
                }
            }

            return new GameDBWorkspaceRecoveryLoadResult(true, snapshot, issues: issues);
        }

        internal GameDBWorkspaceRestoreResult RestoreAssetSessions(
            GameDBWorkspaceRecoverySnapshot snapshot,
            GameDBDocumentLeaseRegistry registry)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var restored = new List<GameDBWorkspaceRestoredTab>();
            var issues = new List<GameDBWorkspaceRecoveryIssue>();
            try
            {
                foreach (var tab in snapshot.Tabs)
                {
                    try
                    {
                        var opened = GameDBAssetSession.TryRestore(registry,
                            tab.DocumentState, tab.TabId);
                        if (opened.Status == GameDBAssetSessionOpenStatus.Busy)
                        {
                            throw new InvalidOperationException(
                                $"Database is already open by session '{opened.ExistingSessionId}'.");
                        }
                        restored.Add(new GameDBWorkspaceRestoredTab(
                            tab.TabId, opened.Session, tab.ViewState));
                    }
                    catch (Exception exception)
                    {
                        issues.Add(QuarantineRecoveryTab(tab, exception.Message));
                    }
                }
            }
            catch
            {
                foreach (var tab in restored)
                {
                    tab.Session.Dispose();
                }
                throw;
            }

            if (snapshot.ActiveTabId != null
                && !restored.Any(tab => tab.TabId == snapshot.ActiveTabId))
            {
                issues.Add(new GameDBWorkspaceRecoveryIssue(snapshot.ActiveTabId,
                    "The active recovery tab could not be restored; the first restored tab was activated.",
                    null));
            }

            return new GameDBWorkspaceRestoreResult(snapshot.ActiveTabId, restored, issues);
        }

        private GameDBWorkspaceRecoveryLoadResult QuarantineFailedPayload(string error)
        {
            string quarantinePath = null;
            try
            {
                quarantinePath = m_store.QuarantinePrimary();
            }
            catch (Exception exception)
            {
                error += $" Quarantine failed: {exception.Message}";
            }

            return new GameDBWorkspaceRecoveryLoadResult(false,
                new GameDBWorkspaceRecoverySnapshot(Array.Empty<GameDBWorkspaceRecoveryTab>()),
                error, quarantinePath);
        }

        private GameDBWorkspaceRecoveryIssue QuarantineRecoveryTab(
            GameDBWorkspaceRecoveryTab tab, string message)
        {
            string contents;
            try
            {
                contents = SerializeTab(tab);
            }
            catch (Exception exception)
            {
                return new GameDBWorkspaceRecoveryIssue(tab.TabId,
                    $"{message} Recovery fragment serialization failed: {exception.Message}", null);
            }
            return QuarantineTab(tab.TabId, contents, message);
        }

        private GameDBWorkspaceRecoveryIssue QuarantineTab(string tabId,
            object tabValue, string message)
        {
            return QuarantineTab(tabId,
                JsonHelper.FormatJson(JsonSerialization.Serialize(tabValue)), message);
        }

        private GameDBWorkspaceRecoveryIssue QuarantineTab(string tabId,
            string contents, string message)
        {
            string quarantinePath = null;
            try
            {
                quarantinePath = m_store.WriteQuarantine(tabId, contents);
            }
            catch (Exception exception)
            {
                message += $" Quarantine failed: {exception.Message}";
            }
            return new GameDBWorkspaceRecoveryIssue(tabId, message, quarantinePath);
        }

        private static string SerializeSnapshot(GameDBWorkspaceRecoverySnapshot snapshot)
        {
            var root = new Dictionary<string, object>
            {
                { "version", snapshot.Version },
                { "activeTabId", snapshot.ActiveTabId },
                { "tabs", snapshot.Tabs.Select(SerializeTabObject).ToArray() }
            };
            return JsonHelper.FormatJson(JsonSerialization.Serialize(root));
        }

        private static string SerializeTab(GameDBWorkspaceRecoveryTab tab)
        {
            return JsonHelper.FormatJson(JsonSerialization.Serialize(SerializeTabObject(tab)));
        }

        private static IDictionary<string, object> SerializeTabObject(
            GameDBWorkspaceRecoveryTab tab)
        {
            return new Dictionary<string, object>
            {
                { "tabId", tab.TabId },
                { "document", SerializeDocumentState(tab.DocumentState) },
                { "view", SerializeViewState(tab.ViewState) }
            };
        }

        private static IDictionary<string, object> SerializeDocumentState(
            GameDBDocumentState state)
        {
            return new Dictionary<string, object>
            {
                { "version", state.Version },
                { "documentId", state.DocumentId },
                { "assetPath", state.AssetPath },
                { "dataJson", state.DataJson },
                { "schemaJson", state.SchemaJson },
                { "baselineRevision", state.BaselineRevision },
                { "baselineDiskToken", new Dictionary<string, object>
                    {
                        { "dataExists", state.BaselineDiskToken.DataExists },
                        { "schemaExists", state.BaselineDiskToken.SchemaExists },
                        { "dataSha256", state.BaselineDiskToken.DataSha256 },
                        { "schemaSha256", state.BaselineDiskToken.SchemaSha256 }
                    }
                },
                { "dataImportPending", state.DataImportPending },
                { "schemaImportPending", state.SchemaImportPending },
                { "callbackPending", state.CallbackPending },
                { "pendingScopeName", state.PendingScopeName },
                { "persistenceStateUnknown", state.PersistenceStateUnknown },
                { "wasDirty", state.WasDirty }
            };
        }

        private static IDictionary<string, object> SerializeViewState(
            GameDBWorkspaceTabViewState state)
        {
            return new Dictionary<string, object>
            {
                { "selectedTableId", state.SelectedTableId },
                { "selectedRowId", state.SelectedRowId },
                { "searchText", state.SearchText },
                { "sorts", state.Sorts.Select(sort => new Dictionary<string, object>
                    {
                        { "fieldId", sort.FieldId }, { "descending", sort.Descending }
                    }).ToArray()
                },
                { "columns", state.Columns.Select(column => new Dictionary<string, object>
                    {
                        { "tableId", column.TableId }, { "fieldId", column.FieldId },
                        { "width", column.Width }, { "order", column.Order }
                    }).ToArray()
                },
                { "horizontalScroll", state.HorizontalScroll },
                { "verticalScroll", state.VerticalScroll }
            };
        }

        private static GameDBWorkspaceRecoveryTab ParseTab(IDictionary<string, object> source)
        {
            return new GameDBWorkspaceRecoveryTab(RequireString(source, "tabId"),
                ParseDocumentState(RequireObject(ReadRequired(source, "document"), "document")),
                ParseViewState(RequireObject(ReadRequired(source, "view"), "view")));
        }

        private static void ValidateDocumentState(GameDBDocumentState state)
        {
            if (state.Version != GameDBDocumentState.CurrentVersion)
            {
                throw new FormatException($"Unsupported document state version: {state.Version}.");
            }
            if (string.IsNullOrWhiteSpace(state.DocumentId)
                || string.IsNullOrWhiteSpace(state.AssetPath)
                || string.IsNullOrWhiteSpace(state.DataJson)
                || string.IsNullOrWhiteSpace(state.SchemaJson))
            {
                throw new FormatException(
                    "Document recovery state requires identity, asset path, data JSON, and schema JSON.");
            }
        }

        private static GameDBDocumentState ParseDocumentState(
            IDictionary<string, object> source)
        {
            var token = RequireObject(ReadRequired(source, "baselineDiskToken"),
                "baseline disk token");
            var state = new GameDBDocumentState
            {
                Version = RequireInt(source, "version"),
                DocumentId = RequireString(source, "documentId"),
                AssetPath = RequireString(source, "assetPath"),
                DataJson = RequireString(source, "dataJson"),
                SchemaJson = RequireString(source, "schemaJson"),
                BaselineRevision = ReadOptionalString(source, "baselineRevision"),
                BaselineDiskToken = new GameDBDiskToken
                {
                    DataExists = RequireBool(token, "dataExists"),
                    SchemaExists = RequireBool(token, "schemaExists"),
                    DataSha256 = ReadOptionalString(token, "dataSha256"),
                    SchemaSha256 = ReadOptionalString(token, "schemaSha256")
                },
                DataImportPending = RequireBool(source, "dataImportPending"),
                SchemaImportPending = RequireBool(source, "schemaImportPending"),
                CallbackPending = RequireBool(source, "callbackPending"),
                PendingScopeName = ReadOptionalString(source, "pendingScopeName"),
                PersistenceStateUnknown = RequireBool(source, "persistenceStateUnknown"),
                WasDirty = RequireBool(source, "wasDirty")
            };
            ValidateDocumentState(state);
            return state;
        }

        private static GameDBWorkspaceTabViewState ParseViewState(
            IDictionary<string, object> source)
        {
            var sorts = ReadArray(source, "sorts").Select(value =>
            {
                var sort = RequireObject(value, "sort");
                return new GameDBWorkspaceSortState(
                    RequireString(sort, "fieldId"), RequireBool(sort, "descending"));
            });
            var columns = ReadArray(source, "columns").Select(value =>
            {
                var column = RequireObject(value, "column");
                return new GameDBWorkspaceColumnState(RequireString(column, "fieldId"),
                    RequireFloat(column, "width"), RequireInt(column, "order"),
                    ReadOptionalString(column, "tableId"));
            });
            return new GameDBWorkspaceTabViewState(
                ReadOptionalString(source, "selectedTableId"),
                ReadOptionalString(source, "selectedRowId"),
                ReadOptionalString(source, "searchText"), sorts, columns,
                RequireFloat(source, "horizontalScroll"),
                RequireFloat(source, "verticalScroll"));
        }

        private static string TryReadTabId(object value)
        {
            var tabId = value is IDictionary<string, object> source
                && source.TryGetValue("tabId", out var rawTabId)
                ? rawTabId as string
                : null;
            return string.IsNullOrWhiteSpace(tabId) ? null : tabId;
        }

        private static object ReadRequired(IDictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value))
            {
                throw new FormatException($"Recovery value '{key}' is required.");
            }
            return value;
        }

        private static IDictionary<string, object> RequireObject(object value, string label)
        {
            if (!(value is IDictionary<string, object> result))
            {
                throw new FormatException($"Recovery {label} must be a JSON object.");
            }
            return result;
        }

        private static IReadOnlyList<object> ReadArray(
            IDictionary<string, object> source, string key)
        {
            var value = ReadRequired(source, key);
            if (!(value is IList<object> result))
            {
                throw new FormatException($"Recovery value '{key}' must be an array.");
            }
            return new ReadOnlyCollection<object>(result.ToArray());
        }

        private static string RequireString(IDictionary<string, object> source, string key)
        {
            var value = ReadRequired(source, key) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException($"Recovery value '{key}' must be a non-empty string.");
            }
            return value;
        }

        private static string ReadOptionalString(
            IDictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }
            if (!(value is string text))
            {
                throw new FormatException($"Recovery value '{key}' must be a string or null.");
            }
            return text;
        }

        private static bool RequireBool(IDictionary<string, object> source, string key)
        {
            var value = ReadRequired(source, key);
            if (!(value is bool result))
            {
                throw new FormatException($"Recovery value '{key}' must be a boolean.");
            }
            return result;
        }

        private static int RequireInt(IDictionary<string, object> source, string key)
        {
            var value = ReadRequired(source, key);
            if (!(value is long number) || number < int.MinValue || number > int.MaxValue)
            {
                throw new FormatException($"Recovery value '{key}' must be a 32-bit integer.");
            }
            return (int)number;
        }

        private static float RequireFloat(IDictionary<string, object> source, string key)
        {
            var value = ReadRequired(source, key);
            double number;
            if (value is long integer)
            {
                number = integer;
            }
            else if (value is double real)
            {
                number = real;
            }
            else
            {
                throw new FormatException($"Recovery value '{key}' must be numeric.");
            }
            if (double.IsNaN(number) || double.IsInfinity(number)
                || number < -float.MaxValue || number > float.MaxValue)
            {
                throw new FormatException($"Recovery value '{key}' is outside the supported range.");
            }
            return (float)number;
        }
    }
}
