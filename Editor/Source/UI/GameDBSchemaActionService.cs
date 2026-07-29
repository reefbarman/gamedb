using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBEditorLibrary.UI
{
    internal enum GameDBSchemaActionStatus
    {
        Executed,
        Cancelled,
        TargetUnavailable,
        TargetChangedAfterConfirmation
    }

    internal sealed class GameDBSchemaActionResult
    {
        internal GameDBSchemaActionStatus Status { get; }
        internal GameDBEditorCommandResult CommandResult { get; }
        internal GameDBSnapshot Snapshot { get; }
        internal GameDBWorkspaceTabViewState ViewState { get; }
        internal bool Success => Status == GameDBSchemaActionStatus.Executed
            && CommandResult?.Success == true;

        private GameDBSchemaActionResult(GameDBSchemaActionStatus status,
            GameDBEditorCommandResult commandResult, GameDBSnapshot snapshot,
            GameDBWorkspaceTabViewState viewState)
        {
            Status = status;
            CommandResult = commandResult;
            Snapshot = snapshot;
            ViewState = viewState;
        }

        internal static GameDBSchemaActionResult Executed(
            GameDBEditorCommandResult result, GameDBWorkspaceTabViewState viewState)
        {
            return new GameDBSchemaActionResult(GameDBSchemaActionStatus.Executed,
                result, result?.Snapshot, viewState);
        }

        internal static GameDBSchemaActionResult Cancelled(GameDBSnapshot snapshot,
            GameDBWorkspaceTabViewState viewState)
        {
            return new GameDBSchemaActionResult(GameDBSchemaActionStatus.Cancelled,
                null, snapshot, viewState);
        }

        internal static GameDBSchemaActionResult TargetUnavailable(
            bool afterConfirmation = false)
        {
            return new GameDBSchemaActionResult(afterConfirmation
                    ? GameDBSchemaActionStatus.TargetChangedAfterConfirmation
                    : GameDBSchemaActionStatus.TargetUnavailable,
                null, null, null);
        }
    }

    internal sealed class GameDBSchemaActionService
    {
        private readonly GameDBEditorWorkspace m_workspace;
        private readonly GameDBEditorCommandService m_commandService;
        private readonly IGameDBEditorDestructiveActionPolicy m_destructivePolicy;
        private readonly Func<bool> m_dataOnlyEditing;

        internal GameDBSchemaActionService(GameDBEditorWorkspace workspace,
            IGameDBEditorDestructiveActionPolicy destructivePolicy = null,
            Func<bool> dataOnlyEditing = null,
            GameDBEditorCommandService commandService = null)
        {
            m_workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            m_destructivePolicy = destructivePolicy
                ?? new GameDBEditorDestructiveActionDialogPolicy();
            m_dataOnlyEditing = dataOnlyEditing ?? (() => false);
            m_commandService = commandService ?? new GameDBEditorCommandService();
        }

        internal GameDBSchemaActionResult SetDatabaseMetadata(
            GameDBEditorWorkspaceTab tab, string documentId, string expectedRevision,
            string scopeName, bool localizationDatabase)
        {
            return Execute(tab, documentId, expectedRevision,
                new SetDatabaseMetadataCommand(Trim(scopeName), localizationDatabase));
        }

        internal GameDBSchemaActionResult AddTable(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            KeyType keyType, string keyTypeArgument)
        {
            var name = Trim(tableName);
            return Execute(tab, documentId, expectedRevision,
                new AddTableCommand(name, keyType,
                    keyType == KeyType.@enum ? Trim(keyTypeArgument) : null),
                (state, _) => CopyView(state, selectedTableId: name,
                    selectedRowId: null, replaceTable: true, replaceRow: true));
        }

        internal GameDBSchemaActionResult RenameTable(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string currentName,
            string newName)
        {
            currentName = Trim(currentName);
            newName = Trim(newName);
            return Execute(tab, documentId, expectedRevision,
                new RenameTableCommand(currentName, newName),
                (state, _) => RenameTableState(state, currentName, newName),
                Confirmation(tab, GameDBCommandKind.RenameTable, "Rename Table",
                    $"Rename table '{currentName}' to '{newName}'? Table references will be updated.",
                    "Rename"));
        }

        internal GameDBSchemaActionResult DeleteTable(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            GameDBSnapshot snapshotForMessage)
        {
            tableName = Trim(tableName);
            var impactSnapshot = snapshotForMessage != null
                    && string.Equals(snapshotForMessage.Revision, expectedRevision,
                        StringComparison.OrdinalIgnoreCase)
                ? snapshotForMessage : IsCurrentTarget(tab, documentId)
                    ? tab.Session.CreateSnapshot() : null;
            var table = impactSnapshot?.Tables.FirstOrDefault(candidate =>
                candidate.Name == tableName);
            return Execute(tab, documentId, expectedRevision,
                new DeleteTableCommand(tableName),
                (state, result) => DeleteTableState(state, result.Snapshot, tableName),
                Confirmation(tab, GameDBCommandKind.DeleteTable, "Delete Table",
                    $"Delete table '{tableName}' with {table?.Fields.Count ?? 0} fields and {table?.Rows.Count ?? 0} rows? Referenced tables cannot be deleted.",
                    "Delete"));
        }

        internal GameDBSchemaActionResult AddField(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            string fieldName, GameDBFieldTypeSpec typeSpec)
        {
            return Execute(tab, documentId, expectedRevision,
                new AddFieldCommand(Trim(tableName), Trim(fieldName),
                    typeSpec ?? throw new ArgumentNullException(nameof(typeSpec))));
        }

        internal GameDBSchemaActionResult RenameField(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            string currentName, string newName)
        {
            tableName = Trim(tableName);
            currentName = Trim(currentName);
            newName = Trim(newName);
            return Execute(tab, documentId, expectedRevision,
                new RenameFieldCommand(tableName, currentName, newName),
                (state, _) => RenameFieldState(state, tableName, currentName, newName),
                Confirmation(tab, GameDBCommandKind.RenameField, "Rename Field",
                    $"Rename field '{tableName}.{currentName}' to '{newName}'?", "Rename"));
        }

        internal GameDBSchemaActionResult ReplaceField(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            string fieldName, GameDBFieldTypeSpec typeSpec)
        {
            tableName = Trim(tableName);
            fieldName = Trim(fieldName);
            return Execute(tab, documentId, expectedRevision,
                new ReplaceFieldCommand(tableName, fieldName,
                    typeSpec ?? throw new ArgumentNullException(nameof(typeSpec))),
                confirmation: Confirmation(tab, GameDBCommandKind.ReplaceField,
                    "Replace Field Type",
                    $"Replace the type of '{tableName}.{fieldName}'? Existing row values will be reset.",
                    "Replace"));
        }

        internal GameDBSchemaActionResult DeleteField(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, string tableName,
            string fieldName)
        {
            tableName = Trim(tableName);
            fieldName = Trim(fieldName);
            return Execute(tab, documentId, expectedRevision,
                new DeleteFieldCommand(tableName, fieldName),
                (state, _) => DeleteFieldState(state, tableName, fieldName),
                Confirmation(tab, GameDBCommandKind.DeleteField, "Delete Field",
                    $"Delete field '{tableName}.{fieldName}' and all of its row values?",
                    "Delete"));
        }

        private GameDBSchemaActionResult Execute(GameDBEditorWorkspaceTab tab,
            string documentId, string expectedRevision, GameDBCommand command,
            Func<GameDBWorkspaceTabViewState, GameDBEditorCommandResult,
                GameDBWorkspaceTabViewState> successViewState = null,
            GameDBDestructiveActionRequest confirmation = null)
        {
            if (!IsCurrentTarget(tab, documentId))
            {
                return GameDBSchemaActionResult.TargetUnavailable();
            }
            if (string.IsNullOrWhiteSpace(expectedRevision))
            {
                throw new ArgumentException(
                    "Schema actions require an expected document revision.",
                    nameof(expectedRevision));
            }

            var session = tab.Session;
            var viewStateBefore = tab.ViewState;
            var dataOnlyEditing = m_dataOnlyEditing();
            if (command.IsDestructive && !dataOnlyEditing)
            {
                if (confirmation == null || !m_destructivePolicy.Confirm(confirmation))
                {
                    return GameDBSchemaActionResult.Cancelled(
                        session.CreateSnapshot(), tab.ViewState);
                }
                if (!IsCurrentTarget(tab, documentId))
                {
                    return GameDBSchemaActionResult.TargetUnavailable(true);
                }
            }

            var result = m_commandService.Execute(session, command, expectedRevision,
                command.IsDestructive && !dataOnlyEditing,
                dataOnlyEditing ? GameDBEditorCommandService.DataOnlyOperations : null);
            var viewState = tab.ViewState;
            if (result.Success && successViewState != null)
            {
                viewState = successViewState(viewStateBefore, result);
                if (viewState != null && !viewState.HasSameValues(tab.ViewState)
                    && !m_workspace.TrySetTabViewState(tab.TabId, viewState))
                {
                    viewState = tab.ViewState;
                }
            }
            return GameDBSchemaActionResult.Executed(result, viewState);
        }

        private bool IsCurrentTarget(GameDBEditorWorkspaceTab tab, string documentId)
        {
            return tab != null && ReferenceEquals(m_workspace.ActiveTab, tab)
                && tab.Session != null && !tab.Session.IsDisposed
                && string.Equals(tab.Session.DocumentId, documentId,
                    StringComparison.Ordinal);
        }

        private static GameDBDestructiveActionRequest Confirmation(
            GameDBEditorWorkspaceTab tab, GameDBCommandKind kind, string title,
            string message, string confirmLabel)
        {
            return new GameDBDestructiveActionRequest(kind,
                tab?.Session.AssetPath, title, message, confirmLabel);
        }

        private static GameDBWorkspaceTabViewState RenameTableState(
            GameDBWorkspaceTabViewState state, string oldName, string newName)
        {
            return CopyView(state,
                selectedTableId: state.SelectedTableId == oldName ? newName : state.SelectedTableId,
                replaceTable: true,
                columns: state.Columns.Select(column => new GameDBWorkspaceColumnState(
                    column.FieldId, column.Width, column.Order,
                    column.TableId == oldName ? newName : column.TableId)));
        }

        private static GameDBWorkspaceTabViewState DeleteTableState(
            GameDBWorkspaceTabViewState state, GameDBSnapshot snapshot, string deletedName)
        {
            var selectedTable = state.SelectedTableId == deletedName
                ? snapshot.Tables.FirstOrDefault()?.Name : state.SelectedTableId;
            return CopyView(state, selectedTableId: selectedTable,
                selectedRowId: state.SelectedTableId == deletedName ? null : state.SelectedRowId,
                replaceTable: true, replaceRow: true,
                columns: state.Columns.Where(column => column.TableId != deletedName));
        }

        private static GameDBWorkspaceTabViewState RenameFieldState(
            GameDBWorkspaceTabViewState state, string tableName,
            string oldName, string newName)
        {
            var sorts = state.SelectedTableId == tableName
                ? state.Sorts.Select(sort => new GameDBWorkspaceSortState(
                    sort.FieldId == oldName ? newName : sort.FieldId, sort.Descending))
                : state.Sorts;
            return CopyView(state, sorts: sorts,
                columns: state.Columns.Select(column => new GameDBWorkspaceColumnState(
                    column.FieldId == oldName && column.TableId == tableName
                        ? newName : column.FieldId,
                    column.Width, column.Order, column.TableId)));
        }

        private static GameDBWorkspaceTabViewState DeleteFieldState(
            GameDBWorkspaceTabViewState state, string tableName, string fieldName)
        {
            var sorts = state.SelectedTableId == tableName
                ? state.Sorts.Where(sort => sort.FieldId != fieldName)
                : state.Sorts;
            return CopyView(state, sorts: sorts,
                columns: state.Columns.Where(column =>
                    column.TableId != tableName || column.FieldId != fieldName));
        }

        private static GameDBWorkspaceTabViewState CopyView(
            GameDBWorkspaceTabViewState state, string selectedTableId = null,
            string selectedRowId = null, bool replaceTable = false,
            bool replaceRow = false,
            IEnumerable<GameDBWorkspaceSortState> sorts = null,
            IEnumerable<GameDBWorkspaceColumnState> columns = null)
        {
            state = state ?? new GameDBWorkspaceTabViewState();
            return new GameDBWorkspaceTabViewState(
                replaceTable ? selectedTableId : state.SelectedTableId,
                replaceRow ? selectedRowId : state.SelectedRowId,
                state.SearchText, sorts ?? state.Sorts, columns ?? state.Columns,
                state.HorizontalScroll, state.VerticalScroll);
        }

        private static string Trim(string value)
        {
            return value?.Trim();
        }
    }
}
