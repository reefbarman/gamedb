using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBDestructiveActionRequest
    {
        internal GameDBCommandKind? Kind { get; }
        internal string AssetPath { get; }
        internal string Title { get; }
        internal string Message { get; }
        internal string ConfirmLabel { get; }

        internal GameDBDestructiveActionRequest(GameDBCommandKind? kind,
            string assetPath, string title, string message, string confirmLabel)
        {
            Kind = kind;
            AssetPath = assetPath;
            Title = title;
            Message = message;
            ConfirmLabel = confirmLabel;
        }
    }

    internal interface IGameDBEditorDestructiveActionPolicy
    {
        bool Confirm(GameDBDestructiveActionRequest request);
    }

    internal sealed class GameDBEditorDestructiveActionDialogPolicy
        : IGameDBEditorDestructiveActionPolicy
    {
        public bool Confirm(GameDBDestructiveActionRequest request)
        {
            return EditorUtility.DisplayDialog(request.Title, request.Message,
                request.ConfirmLabel, "Cancel");
        }
    }

    internal sealed class GameDBSchemaControlsController : IDisposable
    {
        private static readonly IReadOnlyList<string> ScalarFieldTypes =
            Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                .Where(type => type != FieldType.dictionary)
                .Select(type => type.ToString()).ToArray();

        private readonly GameDBEditorWorkspace m_workspace;
        private readonly GameDBEditorCommandService m_commandService;
        private readonly IGameDBEditorDestructiveActionPolicy m_destructivePolicy;
        private readonly Action m_refreshPresentation;
        private readonly Func<bool> m_dataOnlyEditing;
        private readonly TextField m_scope;
        private readonly Toggle m_localization;
        private readonly Button m_applyMetadata;
        private readonly Label m_selectedTable;
        private readonly TextField m_tableName;
        private readonly DropdownField m_tableKeyType;
        private readonly TextField m_tableKeyArgument;
        private readonly Button m_addTable;
        private readonly Button m_renameTable;
        private readonly Button m_deleteTable;
        private readonly ListView m_fields;
        private readonly Label m_selectedFieldType;
        private readonly Label m_collectionNote;
        private readonly TextField m_fieldName;
        private readonly DropdownField m_fieldType;
        private readonly TextField m_fieldEnumArgument;
        private readonly DropdownField m_fieldTableArgument;
        private readonly Button m_addField;
        private readonly Button m_renameField;
        private readonly Button m_replaceField;
        private readonly Button m_deleteField;
        private readonly VisualElement m_messageHost;
        private GameDBSnapshot m_snapshot;
        private string m_boundTabId;
        private string m_boundTableName;
        private string m_selectedFieldName;
        private bool m_binding;
        private bool m_disposed;

        internal GameDBSchemaControlsController(VisualElement root,
            GameDBEditorWorkspace workspace,
            IGameDBEditorDestructiveActionPolicy destructivePolicy = null,
            Action refreshPresentation = null,
            Func<bool> dataOnlyEditing = null)
        {
            m_workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            m_commandService = new GameDBEditorCommandService();
            m_destructivePolicy = destructivePolicy
                ?? new GameDBEditorDestructiveActionDialogPolicy();
            m_refreshPresentation = refreshPresentation;
            m_dataOnlyEditing = dataOnlyEditing ?? (() => false);
            m_scope = Required<TextField>(root, "database-scope-field");
            m_localization = Required<Toggle>(root, "database-localization-toggle");
            m_applyMetadata = Required<Button>(root, "apply-database-metadata-button");
            m_selectedTable = Required<Label>(root, "selected-table-label");
            m_tableName = Required<TextField>(root, "table-name-field");
            m_tableKeyType = Required<DropdownField>(root, "table-key-type-field");
            m_tableKeyArgument = Required<TextField>(root, "table-key-type-argument-field");
            m_addTable = Required<Button>(root, "add-table-button");
            m_renameTable = Required<Button>(root, "rename-table-button");
            m_deleteTable = Required<Button>(root, "delete-table-button");
            m_fields = Required<ListView>(root, "field-navigation-list");
            m_selectedFieldType = Required<Label>(root, "selected-field-type-label");
            m_collectionNote = Required<Label>(root, "field-collection-note");
            m_fieldName = Required<TextField>(root, "field-name-field");
            m_fieldType = Required<DropdownField>(root, "field-type-field");
            m_fieldEnumArgument = Required<TextField>(root, "field-enum-type-argument-field");
            m_fieldTableArgument = Required<DropdownField>(root, "field-table-reference-argument-field");
            m_addField = Required<Button>(root, "add-field-button");
            m_renameField = Required<Button>(root, "rename-field-button");
            m_replaceField = Required<Button>(root, "replace-field-button");
            m_deleteField = Required<Button>(root, "delete-field-button");
            m_messageHost = Required<VisualElement>(root, "editor-action-message-host");

            m_tableKeyType.choices = Enum.GetNames(typeof(KeyType)).ToList();
            m_fieldType.choices = ScalarFieldTypes.ToList();
            m_fields.makeItem = () => new Label();
            m_fields.bindItem = BindFieldItem;
            m_fields.selectionChanged += OnFieldSelectionChanged;
            m_tableKeyType.RegisterValueChangedCallback(OnTableKeyTypeChanged);
            m_fieldType.RegisterValueChangedCallback(OnFieldTypeChanged);
            m_applyMetadata.clicked += ApplyMetadataFromControls;
            m_addTable.clicked += AddTableFromControls;
            m_renameTable.clicked += RenameTableFromControls;
            m_deleteTable.clicked += DeleteTableFromControls;
            m_addField.clicked += AddFieldFromControls;
            m_renameField.clicked += RenameFieldFromControls;
            m_replaceField.clicked += ReplaceFieldFromControls;
            m_deleteField.clicked += DeleteFieldFromControls;
        }

        internal void Bind(GameDBEditorWorkspaceTab tab, GameDBSnapshot snapshot)
        {
            if (m_disposed)
            {
                return;
            }
            m_binding = true;
            try
            {
                m_snapshot = snapshot;
                var tabChanged = m_boundTabId != tab?.TabId;
                var tableName = tab?.ViewState?.SelectedTableId;
                var tableChanged = tabChanged || m_boundTableName != tableName;
                m_boundTabId = tab?.TabId;
                m_boundTableName = tableName;
                if (tableChanged)
                {
                    m_selectedFieldName = null;
                    ClearMessage();
                }

                if (snapshot == null || tab == null)
                {
                    ClearControls();
                    return;
                }

                m_scope.SetValueWithoutNotify(snapshot.ScopeName ?? string.Empty);
                m_localization.SetValueWithoutNotify(snapshot.LocalizationDatabase);
                var table = snapshot.Tables.FirstOrDefault(candidate => candidate.Name == tableName);
                if (table == null)
                {
                    table = snapshot.Tables.FirstOrDefault();
                    m_boundTableName = table?.Name;
                }
                BindTable(table, tableChanged);
                m_applyMetadata.SetEnabled(true);
                ApplyEditingMode();
            }
            finally
            {
                m_binding = false;
            }
        }

        internal GameDBEditorCommandResult SetDatabaseMetadata(
            string scopeName, bool localizationDatabase)
        {
            return Execute(new SetDatabaseMetadataCommand(Trim(scopeName),
                localizationDatabase));
        }

        internal GameDBEditorCommandResult AddTable(string tableName,
            KeyType keyType, string keyTypeArgument)
        {
            var name = Trim(tableName);
            return Execute(new AddTableCommand(name, keyType,
                keyType == KeyType.@enum ? Trim(keyTypeArgument) : null),
                onSuccess: (tab, result) => SetViewState(tab,
                    CopyView(tab.ViewState, selectedTableId: name,
                        selectedRowId: null, replaceTable: true, replaceRow: true)));
        }

        internal GameDBEditorCommandResult RenameTable(string currentName, string newName)
        {
            currentName = Trim(currentName);
            newName = Trim(newName);
            return Execute(new RenameTableCommand(currentName, newName),
                Confirmation(GameDBCommandKind.RenameTable, "Rename Table",
                    $"Rename table '{currentName}' to '{newName}'? Table references will be updated.",
                    "Rename"),
                (tab, result) => SetViewState(tab,
                    RenameTableState(tab.ViewState, currentName, newName)));
        }

        internal GameDBEditorCommandResult DeleteTable(string tableName)
        {
            tableName = Trim(tableName);
            var table = m_snapshot?.Tables.FirstOrDefault(candidate => candidate.Name == tableName);
            return Execute(new DeleteTableCommand(tableName),
                Confirmation(GameDBCommandKind.DeleteTable, "Delete Table",
                    $"Delete table '{tableName}' with {table?.Fields.Count ?? 0} fields and {table?.Rows.Count ?? 0} rows? Referenced tables cannot be deleted.",
                    "Delete"),
                (tab, result) => SetViewState(tab,
                    DeleteTableState(tab.ViewState, result.Snapshot, tableName)));
        }

        internal GameDBEditorCommandResult AddField(string tableName, string fieldName,
            FieldType fieldType, string typeArgument)
        {
            fieldName = Trim(fieldName);
            return Execute(new AddFieldCommand(Trim(tableName), fieldName,
                ScalarSpec(fieldType, typeArgument)), onSuccess: (tab, result) =>
                m_selectedFieldName = fieldName);
        }

        internal GameDBEditorCommandResult RenameField(string tableName,
            string currentName, string newName)
        {
            tableName = Trim(tableName);
            currentName = Trim(currentName);
            newName = Trim(newName);
            return Execute(new RenameFieldCommand(tableName, currentName, newName),
                Confirmation(GameDBCommandKind.RenameField, "Rename Field",
                    $"Rename field '{tableName}.{currentName}' to '{newName}'?", "Rename"),
                (tab, result) =>
                {
                    m_selectedFieldName = newName;
                    SetViewState(tab, RenameFieldState(tab.ViewState,
                        tableName, currentName, newName));
                });
        }

        internal GameDBEditorCommandResult ReplaceField(string tableName,
            string fieldName, FieldType fieldType, string typeArgument)
        {
            tableName = Trim(tableName);
            fieldName = Trim(fieldName);
            return Execute(new ReplaceFieldCommand(tableName, fieldName,
                ScalarSpec(fieldType, typeArgument)),
                Confirmation(GameDBCommandKind.ReplaceField, "Replace Field Type",
                    $"Replace the type of '{tableName}.{fieldName}'? Existing row values will be reset.",
                    "Replace"));
        }

        internal GameDBEditorCommandResult DeleteField(string tableName, string fieldName)
        {
            tableName = Trim(tableName);
            fieldName = Trim(fieldName);
            return Execute(new DeleteFieldCommand(tableName, fieldName),
                Confirmation(GameDBCommandKind.DeleteField, "Delete Field",
                    $"Delete field '{tableName}.{fieldName}' and all of its row values?", "Delete"),
                (tab, result) =>
                {
                    m_selectedFieldName = result.Snapshot.Tables
                        .FirstOrDefault(table => table.Name == tableName)?.Fields
                        .FirstOrDefault()?.Name;
                    SetViewState(tab, DeleteFieldState(tab.ViewState, tableName, fieldName));
                });
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            m_fields.selectionChanged -= OnFieldSelectionChanged;
            m_tableKeyType.UnregisterValueChangedCallback(OnTableKeyTypeChanged);
            m_fieldType.UnregisterValueChangedCallback(OnFieldTypeChanged);
            m_applyMetadata.clicked -= ApplyMetadataFromControls;
            m_addTable.clicked -= AddTableFromControls;
            m_renameTable.clicked -= RenameTableFromControls;
            m_deleteTable.clicked -= DeleteTableFromControls;
            m_addField.clicked -= AddFieldFromControls;
            m_renameField.clicked -= RenameFieldFromControls;
            m_replaceField.clicked -= ReplaceFieldFromControls;
            m_deleteField.clicked -= DeleteFieldFromControls;
            m_fields.makeItem = null;
            m_fields.bindItem = null;
            m_fields.itemsSource = null;
            m_snapshot = null;
        }

        private void BindTable(GameDBTableSnapshot table, bool tableChanged)
        {
            var hasTable = table != null;
            m_selectedTable.text = hasTable ? table.Name : "No table selected";
            m_tableName.SetValueWithoutNotify(table?.Name ?? string.Empty);
            if (tableChanged)
            {
                m_tableKeyType.SetValueWithoutNotify(
                    (table?.KeyType ?? KeyType.@string).ToString());
                m_tableKeyArgument.SetValueWithoutNotify(table?.KeyType == KeyType.@enum
                    ? table.KeyTypeArgument ?? string.Empty : string.Empty);
            }
            m_fields.itemsSource = table?.Fields;
            m_fields.RefreshItems();
            if (hasTable && (string.IsNullOrEmpty(m_selectedFieldName)
                || table.Fields.All(field => field.Name != m_selectedFieldName)))
            {
                m_selectedFieldName = table.Fields.FirstOrDefault()?.Name;
            }
            var fieldIndex = hasTable
                ? table.Fields.FindIndex(field => field.Name == m_selectedFieldName)
                : -1;
            m_fields.SetSelectionWithoutNotify(fieldIndex < 0
                ? Array.Empty<int>() : new[] { fieldIndex });
            BindField(fieldIndex < 0 ? null : table.Fields[fieldIndex]);

            m_renameTable.SetEnabled(hasTable);
            m_deleteTable.SetEnabled(hasTable);
            m_fieldTableArgument.choices = m_snapshot.Tables.Select(candidate => candidate.Name).ToList();
            UpdateArgumentVisibility();
        }

        private void BindField(GameDBFieldSnapshot field)
        {
            var hasField = field != null;
            m_selectedFieldType.text = hasField ? FormatFieldType(field) : string.Empty;
            m_fieldName.SetValueWithoutNotify(field?.Name ?? string.Empty);
            var collection = hasField && (field.IsArray || field.FieldType == FieldType.dictionary);
            m_collectionNote.style.display = collection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasField || collection)
            {
                m_fieldType.index = -1;
            }
            else
            {
                m_fieldType.SetValueWithoutNotify(field.FieldType.ToString());
            }
            m_fieldEnumArgument.SetValueWithoutNotify(
                field?.FieldType == FieldType.@enum ? field.TypeArgument ?? string.Empty : string.Empty);
            m_fieldTableArgument.SetValueWithoutNotify(
                field?.FieldType == FieldType.tableRef ? field.TypeArgument ?? string.Empty : string.Empty);
            m_renameField.SetEnabled(hasField);
            m_deleteField.SetEnabled(hasField);
            UpdateArgumentVisibility();
        }

        private GameDBEditorCommandResult Execute(GameDBCommand command,
            GameDBDestructiveActionRequest confirmation = null,
            Action<GameDBEditorWorkspaceTab, GameDBEditorCommandResult> onSuccess = null)
        {
            if (m_disposed || command == null)
            {
                return null;
            }
            var tab = m_workspace.ActiveTab;
            if (tab == null)
            {
                return null;
            }
            var session = tab.Session;
            var before = session.CreateSnapshot();
            var viewStateBefore = tab.ViewState;
            if (command.IsDestructive)
            {
                if (confirmation == null || !m_destructivePolicy.Confirm(confirmation))
                {
                    return null;
                }
                if (!ReferenceEquals(m_workspace.ActiveTab, tab) || session.IsDisposed)
                {
                    ShowError("The active GameDB document changed while confirmation was open. Retry the action.");
                    m_refreshPresentation?.Invoke();
                    return null;
                }
            }
            var result = m_commandService.Execute(session, command,
                before.Revision, command.IsDestructive,
                m_dataOnlyEditing() ? GameDBEditorCommandService.DataOnlyOperations : null);
            if (result.Success)
            {
                if (onSuccess != null)
                {
                    tab.SetViewState(viewStateBefore, false);
                    onSuccess(tab, result);
                }
                ClearMessage();
            }
            else
            {
                ShowError(result.Message);
            }
            Bind(tab, result.Snapshot);
            if (result.Success)
            {
                m_refreshPresentation?.Invoke();
            }
            return result;
        }

        private GameDBDestructiveActionRequest Confirmation(GameDBCommandKind kind,
            string title, string message, string confirmLabel)
        {
            return new GameDBDestructiveActionRequest(kind,
                m_workspace.ActiveTab?.Session.AssetPath, title, message, confirmLabel);
        }

        private void SetViewState(GameDBEditorWorkspaceTab tab,
            GameDBWorkspaceTabViewState state)
        {
            if (tab != null && state != null && !state.HasSameValues(tab.ViewState))
            {
                m_workspace.TrySetTabViewState(tab.TabId, state);
            }
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
            if (state.SelectedTableId != tableName)
            {
                return state;
            }
            return CopyView(state,
                sorts: state.Sorts.Select(sort => new GameDBWorkspaceSortState(
                    sort.FieldId == oldName ? newName : sort.FieldId, sort.Descending)),
                columns: state.Columns.Select(column => new GameDBWorkspaceColumnState(
                    column.FieldId == oldName && column.TableId == tableName
                        ? newName : column.FieldId,
                    column.Width, column.Order, column.TableId)));
        }

        private static GameDBWorkspaceTabViewState DeleteFieldState(
            GameDBWorkspaceTabViewState state, string tableName, string fieldName)
        {
            if (state.SelectedTableId != tableName)
            {
                return state;
            }
            return CopyView(state,
                sorts: state.Sorts.Where(sort => sort.FieldId != fieldName),
                columns: state.Columns.Where(column =>
                    column.TableId != tableName || column.FieldId != fieldName));
        }

        private static GameDBWorkspaceTabViewState CopyView(
            GameDBWorkspaceTabViewState state, string selectedTableId = null,
            string selectedRowId = null, bool replaceTable = false, bool replaceRow = false,
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

        private static GameDBFieldTypeSpec ScalarSpec(FieldType fieldType,
            string typeArgument)
        {
            if (fieldType == FieldType.dictionary)
            {
                throw new ArgumentException(
                    "Dictionary fields are available in the collection editor slice.",
                    nameof(fieldType));
            }
            return new GameDBFieldTypeSpec(fieldType, false,
                fieldType == FieldType.@enum || fieldType == FieldType.tableRef
                    ? Trim(typeArgument) : null);
        }

        private static string FormatFieldType(GameDBFieldSnapshot field)
        {
            if (field.FieldType == FieldType.dictionary)
            {
                return "dictionary";
            }
            return field.FieldType + (field.IsArray ? "[]" : string.Empty)
                + (string.IsNullOrEmpty(field.TypeArgument)
                    ? string.Empty : $" ({field.TypeArgument})");
        }

        private void OnFieldSelectionChanged(IEnumerable<object> selection)
        {
            if (m_binding || m_disposed)
            {
                return;
            }
            var field = selection.OfType<GameDBFieldSnapshot>().FirstOrDefault();
            m_selectedFieldName = field?.Name;
            m_binding = true;
            try
            {
                BindField(field);
            }
            finally
            {
                m_binding = false;
            }
        }

        private void BindFieldItem(VisualElement element, int index)
        {
            var label = (Label)element;
            var field = m_fields.itemsSource?[index] as GameDBFieldSnapshot;
            label.text = field?.Name ?? string.Empty;
            label.tooltip = field == null ? string.Empty : FormatFieldType(field);
        }

        private void OnTableKeyTypeChanged(ChangeEvent<string> _) => UpdateArgumentVisibility();
        private void OnFieldTypeChanged(ChangeEvent<string> _) => UpdateArgumentVisibility();

        private void UpdateArgumentVisibility()
        {
            m_tableKeyArgument.style.display = m_tableKeyType.value == KeyType.@enum.ToString()
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_fieldEnumArgument.style.display = m_fieldType.value == FieldType.@enum.ToString()
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_fieldTableArgument.style.display = m_fieldType.value == FieldType.tableRef.ToString()
                ? DisplayStyle.Flex : DisplayStyle.None;
            var hasScalarType = Enum.TryParse(m_fieldType.value, out FieldType fieldType)
                && fieldType != FieldType.dictionary;
            m_addField.SetEnabled(m_boundTableName != null && hasScalarType);
            m_replaceField.SetEnabled(m_selectedFieldName != null && hasScalarType);
        }

        private void ApplyEditingMode()
        {
            var schemaEnabled = !m_dataOnlyEditing();
            m_scope.SetEnabled(schemaEnabled);
            m_localization.SetEnabled(schemaEnabled);
            m_applyMetadata.SetEnabled(schemaEnabled);
            m_tableName.SetEnabled(schemaEnabled);
            m_tableKeyType.SetEnabled(schemaEnabled);
            m_tableKeyArgument.SetEnabled(schemaEnabled);
            m_addTable.SetEnabled(schemaEnabled);
            m_renameTable.SetEnabled(schemaEnabled && m_boundTableName != null);
            m_deleteTable.SetEnabled(schemaEnabled && m_boundTableName != null);
            m_fieldName.SetEnabled(schemaEnabled);
            m_fieldType.SetEnabled(schemaEnabled);
            m_fieldEnumArgument.SetEnabled(schemaEnabled);
            m_fieldTableArgument.SetEnabled(schemaEnabled);
            var hasScalarType = Enum.TryParse(m_fieldType.value, out FieldType fieldType)
                && fieldType != FieldType.dictionary;
            m_addField.SetEnabled(schemaEnabled && m_boundTableName != null && hasScalarType);
            m_renameField.SetEnabled(schemaEnabled && m_selectedFieldName != null);
            m_replaceField.SetEnabled(schemaEnabled && m_selectedFieldName != null
                && hasScalarType);
            m_deleteField.SetEnabled(schemaEnabled && m_selectedFieldName != null);
        }

        private void ClearControls()
        {
            m_snapshot = null;
            m_scope.SetValueWithoutNotify(string.Empty);
            m_localization.SetValueWithoutNotify(false);
            m_selectedTable.text = "No table selected";
            m_selectedFieldType.text = string.Empty;
            m_fields.itemsSource = null;
            m_fields.RefreshItems();
            m_applyMetadata.SetEnabled(false);
            m_addField.SetEnabled(false);
            m_renameField.SetEnabled(false);
            m_replaceField.SetEnabled(false);
            m_deleteField.SetEnabled(false);
            m_renameTable.SetEnabled(false);
            m_deleteTable.SetEnabled(false);
            ClearMessage();
        }

        private void ShowError(string message)
        {
            m_messageHost.Clear();
            m_messageHost.Add(new HelpBox(message ?? "The GameDB action failed.",
                HelpBoxMessageType.Error));
        }

        private void ClearMessage() => m_messageHost.Clear();

        private void ApplyMetadataFromControls() =>
            SetDatabaseMetadata(m_scope.value, m_localization.value);
        private void AddTableFromControls() => AddTable(m_tableName.value,
            Parse<KeyType>(m_tableKeyType.value), m_tableKeyArgument.value);
        private void RenameTableFromControls() =>
            RenameTable(m_boundTableName, m_tableName.value);
        private void DeleteTableFromControls() => DeleteTable(m_boundTableName);
        private void AddFieldFromControls() => AddField(m_boundTableName,
            m_fieldName.value, Parse<FieldType>(m_fieldType.value), FieldArgument());
        private void RenameFieldFromControls() => RenameField(m_boundTableName,
            m_selectedFieldName, m_fieldName.value);
        private void ReplaceFieldFromControls() => ReplaceField(m_boundTableName,
            m_selectedFieldName, Parse<FieldType>(m_fieldType.value), FieldArgument());
        private void DeleteFieldFromControls() =>
            DeleteField(m_boundTableName, m_selectedFieldName);

        private string FieldArgument()
        {
            return m_fieldType.value == FieldType.@enum.ToString()
                ? m_fieldEnumArgument.value
                : m_fieldType.value == FieldType.tableRef.ToString()
                    ? m_fieldTableArgument.value : null;
        }

        private static T Parse<T>(string value) where T : struct
        {
            return Enum.TryParse(value, out T parsed) ? parsed : default;
        }

        private static string Trim(string value) => value?.Trim();

        private static T Required<T>(VisualElement root, string name)
            where T : VisualElement
        {
            return root?.Q<T>(name) ?? throw new InvalidOperationException(
                $"Required GameDB schema control '{name}' was not found.");
        }
    }
}
