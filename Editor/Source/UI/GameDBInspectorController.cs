using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBInspectorController : IDisposable
    {
        private readonly GameDBEditorWorkspace m_workspace;
        private readonly GameDBSchemaActionService m_actions;
        private readonly Func<IReadOnlyList<string>> m_importedEnumTypes;
        private readonly Action m_refreshPresentation;
        private readonly Func<bool> m_dataOnlyEditing;
        private readonly Action<VisualElement> m_ensureOpen;
        private readonly Button m_tableCreate;
        private readonly Button m_back;
        private readonly Label m_eyebrow;
        private readonly Label m_title;
        private readonly VisualElement m_tableView;
        private readonly Label m_tableSummary;
        private readonly Button m_tableRename;
        private readonly Button m_tableDelete;
        private readonly Button m_fieldCreate;
        private readonly ListView m_fields;
        private readonly ScrollView m_fieldView;
        private readonly Label m_fieldType;
        private readonly Label m_fieldDetail;
        private readonly Button m_fieldRename;
        private readonly Button m_fieldChangeType;
        private readonly Button m_fieldDelete;
        private readonly ScrollView m_taskView;
        private readonly Label m_taskContext;
        private readonly VisualElement m_taskForm;
        private readonly VisualElement m_typeEditorHost;
        private readonly VisualElement m_taskMessage;
        private readonly VisualElement m_actionMessage;
        private readonly VisualElement m_databaseCard;
        private readonly Button m_databaseToggle;
        private readonly ScrollView m_databaseScroll;
        private readonly Label m_databaseSummary;
        private readonly Button m_databaseEdit;
        private readonly VisualElement m_decision;
        private readonly Button m_navigationCancel;
        private readonly Button m_navigationDiscard;
        private readonly Button m_navigationSave;
        private readonly VisualElement m_taskFooter;
        private readonly Button m_taskCancel;
        private readonly Button m_taskPrimary;
        private readonly GameDBFieldTypeEditor m_typeEditor;
        private readonly GameDBInspectorState m_state = new GameDBInspectorState();
        private GameDBEditorWorkspaceTab m_tab;
        private GameDBSnapshot m_snapshot;
        private string m_selectedFieldName;
        private TextField m_nameField;
        private DropdownField m_keyTypeField;
        private DropdownField m_keyArgumentField;
        private TextField m_scopeField;
        private Toggle m_localizationToggle;
        private bool m_databaseExpanded;
        private Action m_pendingContinuation;
        private GameDBInspectorContext m_actionMessageContext;
        private bool m_binding;
        private bool m_disposed;

        internal bool HasDirtyTask => m_state.Task?.IsDirty == true;
        internal GameDBInspectorContext Context => m_state.Context;

        internal GameDBInspectorController(VisualElement root,
            GameDBEditorWorkspace workspace,
            IGameDBEditorDestructiveActionPolicy destructivePolicy = null,
            Func<IReadOnlyList<string>> importedEnumTypes = null,
            Action refreshPresentation = null,
            Func<bool> dataOnlyEditing = null,
            Action<VisualElement> ensureOpen = null)
        {
            m_workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            m_actions = new GameDBSchemaActionService(workspace, destructivePolicy,
                dataOnlyEditing);
            m_importedEnumTypes = importedEnumTypes ?? (() => Array.Empty<string>());
            m_refreshPresentation = refreshPresentation;
            m_dataOnlyEditing = dataOnlyEditing ?? (() => false);
            m_ensureOpen = ensureOpen;
            m_tableCreate = Required<Button>(root, "table-create-button");
            m_back = Required<Button>(root, "inspector-back-button");
            m_eyebrow = Required<Label>(root, "inspector-eyebrow-label");
            m_title = Required<Label>(root, "inspector-title-label");
            m_tableView = Required<VisualElement>(root, "inspector-table-view");
            m_tableSummary = Required<Label>(root, "inspector-table-summary");
            m_tableRename = Required<Button>(root, "table-rename-action");
            m_tableDelete = Required<Button>(root, "table-delete-action");
            m_fieldCreate = Required<Button>(root, "field-create-button");
            m_fields = Required<ListView>(root, "field-navigation-list");
            m_fieldView = Required<ScrollView>(root, "inspector-field-view");
            m_fieldType = Required<Label>(root, "inspector-field-type-label");
            m_fieldDetail = Required<Label>(root, "inspector-field-detail-label");
            m_fieldRename = Required<Button>(root, "field-rename-action");
            m_fieldChangeType = Required<Button>(root, "field-change-type-action");
            m_fieldDelete = Required<Button>(root, "field-delete-action");
            m_taskView = Required<ScrollView>(root, "inspector-task-scroll");
            m_taskContext = Required<Label>(root, "inspector-task-context-label");
            m_taskForm = Required<VisualElement>(root, "inspector-task-form-host");
            m_typeEditorHost = Required<VisualElement>(root, "field-type-editor-host");
            m_taskMessage = Required<VisualElement>(root, "inspector-task-message-host");
            m_actionMessage = Required<VisualElement>(root, "inspector-action-message-host");
            m_databaseCard = Required<VisualElement>(root, "inspector-database-foldout");
            m_databaseToggle = Required<Button>(root, "database-foldout-toggle");
            m_databaseScroll = Required<ScrollView>(root, "database-foldout-scroll");
            m_databaseSummary = Required<Label>(root, "database-summary-label");
            m_databaseEdit = Required<Button>(root, "database-edit-action");
            m_decision = Required<VisualElement>(root, "inspector-navigation-decision");
            m_navigationCancel = Required<Button>(root,
                "inspector-navigation-cancel");
            m_navigationDiscard = Required<Button>(root,
                "inspector-navigation-discard");
            m_navigationSave = Required<Button>(root,
                "inspector-navigation-save");
            m_taskFooter = Required<VisualElement>(root, "inspector-task-footer");
            m_taskCancel = Required<Button>(root, "inspector-task-cancel");
            m_taskPrimary = Required<Button>(root, "inspector-task-primary");
            m_typeEditor = new GameDBFieldTypeEditor(m_typeEditorHost);
            m_fields.makeItem = () => new Label();
            m_fields.bindItem = BindFieldItem;
            m_fields.selectionChanged += OnFieldSelectionChanged;
            m_tableCreate.clicked += StartCreateTable;
            m_back.clicked += ShowTableContext;
            m_tableRename.clicked += StartRenameTable;
            m_tableDelete.clicked += DeleteTable;
            m_fieldCreate.clicked += StartCreateField;
            m_fieldRename.clicked += StartRenameField;
            m_fieldChangeType.clicked += StartChangeFieldType;
            m_fieldDelete.clicked += DeleteField;
            m_databaseToggle.clicked += ToggleDatabase;
            m_databaseEdit.clicked += StartEditDatabase;
            m_navigationCancel.clicked += CancelPendingNavigation;
            m_navigationDiscard.clicked += DiscardAndContinue;
            m_navigationSave.clicked += SaveAndContinue;
            m_taskCancel.clicked += CancelTask;
            m_taskPrimary.clicked += SubmitTask;
            m_typeEditor.Changed += OnTypeChanged;
            ShowEmpty();
        }

        internal void Bind(GameDBEditorWorkspaceTab tab, GameDBSnapshot snapshot)
        {
            if (m_disposed)
            {
                return;
            }
            m_tab = tab;
            m_snapshot = snapshot;
            if (tab == null || snapshot == null)
            {
                m_pendingContinuation = null;
                m_state.Reset();
                m_selectedFieldName = null;
                ShowEmpty();
                return;
            }
            if (m_state.Task != null)
            {
                if (m_state.Task.Context.TabId != tab.TabId
                    || m_state.Task.Context.DocumentId != tab.Session.DocumentId)
                {
                    m_pendingContinuation = null;
                    m_state.Reset();
                }
                else
                {
                    m_state.Task.RecheckStaleness(snapshot);
                    var enumChoices = EnumChoices();
                    m_typeEditor.UpdateChoices(enumChoices, TableChoices());
                    if (m_keyArgumentField != null)
                    {
                        var value = m_keyArgumentField.value;
                        m_binding = true;
                        try
                        {
                            m_keyArgumentField.choices = enumChoices.ToList();
                            m_keyArgumentField.SetValueWithoutNotify(
                                enumChoices.Contains(value, StringComparer.Ordinal)
                                    ? value : null);
                        }
                        finally
                        {
                            m_binding = false;
                        }
                    }
                    m_databaseSummary.text =
                        $"{snapshot.ScopeName} · {(snapshot.LocalizationDatabase ? "Localization" : "Standard")}";
                    RenderTaskState();
                    return;
                }
            }
            var table = SelectedTable();
            if (table == null)
            {
                m_state.SetContext(GameDBInspectorContext.Database(
                    tab.TabId, tab.Session.DocumentId));
                m_selectedFieldName = null;
            }
            else if (!string.IsNullOrEmpty(m_selectedFieldName)
                && table.Fields.Any(field => field.Name == m_selectedFieldName))
            {
                m_state.SetContext(GameDBInspectorContext.Field(tab.TabId,
                    tab.Session.DocumentId, table.Name, m_selectedFieldName));
            }
            else
            {
                m_selectedFieldName = null;
                m_state.SetContext(GameDBInspectorContext.Table(tab.TabId,
                    tab.Session.DocumentId, table.Name));
            }
            RenderContext();
        }

        internal void RequestCreateTable()
        {
            if (!m_disposed)
            {
                StartCreateTable();
            }
        }

        internal void RequestCreateField()
        {
            if (!m_disposed)
            {
                StartCreateField();
            }
        }

        internal void SubmitActiveTask()
        {
            if (!m_disposed)
            {
                SubmitTask();
            }
        }

        internal void DiscardPendingNavigation()
        {
            if (!m_disposed)
            {
                DiscardAndContinue();
            }
        }

        internal void CancelActiveTask()
        {
            if (!m_disposed)
            {
                CancelTask();
            }
        }

        internal bool RequestWindowAction(GameDBInspectorPendingIntentKind kind,
            string message, Action continuation, string tabId = null)
        {
            if (m_disposed || continuation == null)
            {
                return false;
            }
            if (m_state.Task == null)
            {
                continuation();
                return true;
            }
            if (!m_state.Task.IsDirty)
            {
                m_state.CancelTask();
                RenderContextAfterTask();
                continuation();
                return true;
            }
            var intent = new GameDBInspectorPendingIntent(kind,
                tabId: tabId ?? m_tab?.TabId);
            if (!m_state.TrySetPendingIntent(intent))
            {
                return false;
            }
            m_pendingContinuation = continuation;
            Required<Label>(m_decision, "inspector-navigation-message").text = message;
            m_taskFooter.style.display = DisplayStyle.None;
            m_decision.style.display = DisplayStyle.Flex;
            m_ensureOpen?.Invoke(m_navigationCancel);
            return false;
        }

        internal bool RequestTableSelection(string tableName)
        {
            if (m_disposed || m_tab == null || string.Equals(tableName,
                SelectedTable()?.Name, StringComparison.Ordinal))
            {
                return true;
            }
            if (m_state.Task == null)
            {
                return true;
            }
            if (!m_state.Task.IsDirty)
            {
                m_state.CancelTask();
                return true;
            }
            var target = GameDBInspectorContext.Table(m_tab.TabId,
                m_tab.Session.DocumentId, tableName);
            if (!m_state.TrySetPendingIntent(new GameDBInspectorPendingIntent(
                GameDBInspectorPendingIntentKind.SelectTable, target)))
            {
                return false;
            }
            Required<Label>(m_decision, "inspector-navigation-message").text =
                "You have unsaved Inspector changes. Save or discard them before changing tables.";
            m_taskFooter.style.display = DisplayStyle.None;
            m_decision.style.display = DisplayStyle.Flex;
            m_ensureOpen?.Invoke(Required<Button>(m_decision,
                "inspector-navigation-cancel"));
            return false;
        }

        internal void RequestInspectField(string tableName, string fieldName)
        {
            if (m_disposed || m_tab == null || m_snapshot == null)
            {
                return;
            }
            var table = m_snapshot.Tables.FirstOrDefault(candidate =>
                candidate.Name == tableName);
            if (table?.Fields.Any(field => field.Name == fieldName) != true)
            {
                return;
            }
            if (m_state.Task != null)
            {
                if (!m_state.Task.IsDirty)
                {
                    m_state.CancelTask();
                }
                else
                {
                    var target = GameDBInspectorContext.Field(m_tab.TabId,
                        m_tab.Session.DocumentId, tableName, fieldName);
                    if (m_state.TrySetPendingIntent(new GameDBInspectorPendingIntent(
                        GameDBInspectorPendingIntentKind.SelectField, target)))
                    {
                        Required<Label>(m_decision,
                            "inspector-navigation-message").text =
                            "You have unsaved Inspector changes. Save or discard them before inspecting another field.";
                        m_taskFooter.style.display = DisplayStyle.None;
                        m_decision.style.display = DisplayStyle.Flex;
                        m_ensureOpen?.Invoke(m_navigationCancel);
                    }
                    return;
                }
            }
            m_selectedFieldName = fieldName;
            m_state.SetContext(GameDBInspectorContext.Field(m_tab.TabId,
                m_tab.Session.DocumentId, tableName, fieldName));
            m_ensureOpen?.Invoke(m_fieldRename);
            RenderContext();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            m_fields.selectionChanged -= OnFieldSelectionChanged;
            m_tableCreate.clicked -= StartCreateTable;
            m_back.clicked -= ShowTableContext;
            m_tableRename.clicked -= StartRenameTable;
            m_tableDelete.clicked -= DeleteTable;
            m_fieldCreate.clicked -= StartCreateField;
            m_fieldRename.clicked -= StartRenameField;
            m_fieldChangeType.clicked -= StartChangeFieldType;
            m_fieldDelete.clicked -= DeleteField;
            m_databaseToggle.clicked -= ToggleDatabase;
            m_databaseEdit.clicked -= StartEditDatabase;
            m_navigationCancel.clicked -= CancelPendingNavigation;
            m_navigationDiscard.clicked -= DiscardAndContinue;
            m_navigationSave.clicked -= SaveAndContinue;
            m_taskCancel.clicked -= CancelTask;
            m_taskPrimary.clicked -= SubmitTask;
            m_typeEditor.Changed -= OnTypeChanged;
            m_typeEditor.Dispose();
            m_pendingContinuation = null;
            m_state.Reset();
            m_taskForm.Clear();
            m_taskMessage.Clear();
            ClearActionMessage();
            m_nameField = null;
            m_keyTypeField = null;
            m_keyArgumentField = null;
            m_scopeField = null;
            m_localizationToggle = null;
            m_fields.makeItem = null;
            m_fields.bindItem = null;
            m_fields.itemsSource = null;
        }

        private void RenderContext()
        {
            HideScreens();
            m_taskFooter.style.display = DisplayStyle.None;
            if (!Equals(m_actionMessageContext, m_state.Context))
            {
                ClearActionMessage();
            }
            m_databaseCard.style.display = DisplayStyle.Flex;
            m_databaseSummary.text = m_snapshot == null ? string.Empty
                : $"{m_snapshot.ScopeName} · {(m_snapshot.LocalizationDatabase ? "Localization" : "Standard")}";
            m_databaseScroll.style.display = m_databaseExpanded
                ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_state.Context?.Kind == GameDBInspectorContextKind.Field)
            {
                RenderField();
            }
            else
            {
                RenderTable();
            }
            ApplyEditingMode();
        }

        private void RenderTable()
        {
            var table = SelectedTable();
            m_eyebrow.text = table == null ? "Database" : "Table";
            m_title.text = table?.Name ?? "No table selected";
            m_back.style.display = DisplayStyle.None;
            m_tableView.style.display = DisplayStyle.Flex;
            m_tableSummary.text = table == null
                ? "Create a table to begin authoring schema and data."
                : $"{FormatKeyType(table)} · {table.Rows.Count} rows · {table.Fields.Count} fields";
            m_binding = true;
            try
            {
                m_fields.itemsSource = table?.Fields;
                m_fields.Rebuild();
                m_fields.SetSelectionWithoutNotify(Array.Empty<int>());
            }
            finally
            {
                m_binding = false;
            }
            m_tableRename.style.display = table == null
                ? DisplayStyle.None : DisplayStyle.Flex;
            m_tableDelete.style.display = table == null
                ? DisplayStyle.None : DisplayStyle.Flex;
            m_fieldCreate.SetEnabled(table != null);
        }

        private void RenderField()
        {
            var field = SelectedField();
            if (field == null)
            {
                m_selectedFieldName = null;
                ShowTableContext();
                return;
            }
            m_eyebrow.text = "Field";
            m_title.text = field.Name;
            m_back.style.display = DisplayStyle.Flex;
            m_fieldView.style.display = DisplayStyle.Flex;
            var draft = DraftFrom(field);
            m_fieldType.text = GameDBFieldTypeDraftAdapter.Format(draft);
            m_fieldDetail.text = draft.Shape == GameDBFieldShape.Dictionary
                ? "Dictionary key and value types are edited together."
                : draft.Shape == GameDBFieldShape.Array
                    ? "Array element type" : "Scalar value type";
        }

        private void StartCreateTable()
        {
            if (!CanStartTask())
            {
                return;
            }
            var context = GameDBInspectorContext.Database(
                m_tab.TabId, m_tab.Session.DocumentId);
            BeginTask(GameDBInspectorTaskKind.CreateTable, context,
                new GameDBInspectorTableDraft(string.Empty, KeyType.@string, null),
                "New Table", "Database");
            BuildTableForm(true);
        }

        private void StartRenameTable()
        {
            var table = SelectedTable();
            if (!CanStartTask() || table == null)
            {
                return;
            }
            var context = GameDBInspectorContext.Table(m_tab.TabId,
                m_tab.Session.DocumentId, table.Name);
            BeginTask(GameDBInspectorTaskKind.RenameTable, context,
                new GameDBInspectorTableDraft(table.Name, table.KeyType,
                    table.KeyTypeArgument), "Rename Table", table.Name);
            BuildTableForm(false);
        }

        private void StartCreateField()
        {
            var table = SelectedTable();
            if (!CanStartTask() || table == null)
            {
                return;
            }
            var context = GameDBInspectorContext.Table(m_tab.TabId,
                m_tab.Session.DocumentId, table.Name);
            var type = new GameDBInspectorFieldTypeDraft(
                FieldType.@string, false, null);
            BeginTask(GameDBInspectorTaskKind.CreateField, context,
                new GameDBInspectorFieldDraft(string.Empty, type),
                "New Field", $"In table: {table.Name}");
            BuildFieldForm(true, type);
        }

        private void StartRenameField()
        {
            var field = SelectedField();
            if (!CanStartTask() || field == null)
            {
                return;
            }
            var context = m_state.Context;
            BeginTask(GameDBInspectorTaskKind.RenameField, context,
                new GameDBInspectorFieldNameDraft(field.Name),
                "Edit Field Name", $"In table: {context.TableName}");
            BuildNameForm(field.Name);
        }

        private void StartChangeFieldType()
        {
            var field = SelectedField();
            if (!CanStartTask() || field == null)
            {
                return;
            }
            var type = DraftFrom(field);
            BeginTask(GameDBInspectorTaskKind.ChangeFieldType, m_state.Context,
                type, "Change Field Type", $"{m_state.Context.TableName}.{field.Name}");
            BuildFieldForm(false, type);
        }

        private void StartEditDatabase()
        {
            if (!CanStartTask())
            {
                return;
            }
            var context = GameDBInspectorContext.Database(
                m_tab.TabId, m_tab.Session.DocumentId);
            BeginTask(GameDBInspectorTaskKind.EditDatabase, context,
                new GameDBInspectorDatabaseDraft(m_snapshot.ScopeName,
                    m_snapshot.LocalizationDatabase), "Edit Database", m_tab.Session.AssetPath);
            m_taskForm.Clear();
            m_typeEditorHost.style.display = DisplayStyle.None;
            m_scopeField = new TextField("Scope")
            {
                name = "inspector-task-scope-field",
                value = m_snapshot.ScopeName
            };
            m_localizationToggle = new Toggle("Localization database")
            {
                name = "inspector-task-localization-toggle",
                value = m_snapshot.LocalizationDatabase
            };
            m_scopeField.RegisterValueChangedCallback(_ => MarkTaskDirty());
            m_localizationToggle.RegisterValueChangedCallback(_ => MarkTaskDirty());
            m_taskForm.Add(m_scopeField);
            m_taskForm.Add(m_localizationToggle);
            ValidateTask();
            m_ensureOpen?.Invoke(m_scopeField);
        }

        private void BeginTask(GameDBInspectorTaskKind kind,
            GameDBInspectorContext context, IGameDBInspectorDraft draft,
            string title, string taskContext)
        {
            m_state.BeginTask(new GameDBInspectorTaskState(kind, context, draft, m_snapshot));
            ClearActionMessage();
            HideScreens();
            m_eyebrow.text = "Task";
            m_title.text = title;
            m_back.style.display = DisplayStyle.None;
            m_taskContext.text = taskContext;
            m_taskView.style.display = DisplayStyle.Flex;
            m_taskFooter.style.display = DisplayStyle.Flex;
            m_databaseCard.style.display = DisplayStyle.None;
            m_taskMessage.Clear();
            m_typeEditor.Bind(new GameDBInspectorFieldTypeDraft(
                FieldType.@string, false, null), EnumChoices(), TableChoices(),
                m_snapshot.LocalizationDatabase);
            m_taskPrimary.text = kind == GameDBInspectorTaskKind.CreateTable
                || kind == GameDBInspectorTaskKind.CreateField ? "Add" : "Save";
            ApplyEditingMode();
        }

        private void BuildTableForm(bool includeKey)
        {
            var draft = (GameDBInspectorTableDraft)m_state.Task.Draft;
            m_taskForm.Clear();
            m_typeEditorHost.style.display = DisplayStyle.None;
            m_nameField = new TextField("Name")
            {
                name = "inspector-task-name-field",
                value = draft.Name
            };
            m_nameField.RegisterValueChangedCallback(_ => MarkTaskDirty());
            m_taskForm.Add(m_nameField);
            m_keyTypeField = null;
            m_keyArgumentField = null;
            if (includeKey)
            {
                m_keyTypeField = new DropdownField("Key type",
                    new List<string> { "String", "Enum" }, 0)
                {
                    name = "inspector-task-key-type-field"
                };
                m_keyArgumentField = new DropdownField("Enum type",
                    EnumChoices().ToList(), -1)
                {
                    name = "inspector-task-key-argument-field"
                };
                m_keyTypeField.RegisterValueChangedCallback(_ =>
                {
                    MarkTaskDirty();
                    ApplyKeyArgumentVisibility();
                });
                m_keyArgumentField.RegisterValueChangedCallback(_ => MarkTaskDirty());
                m_taskForm.Add(m_keyTypeField);
                m_taskForm.Add(m_keyArgumentField);
                ApplyKeyArgumentVisibility();
            }
            ValidateTask();
            m_ensureOpen?.Invoke(m_nameField);
        }

        private void BuildNameForm(string value)
        {
            m_taskForm.Clear();
            m_typeEditorHost.style.display = DisplayStyle.None;
            m_nameField = new TextField("Name")
            {
                name = "inspector-task-name-field",
                value = value
            };
            m_nameField.RegisterValueChangedCallback(_ => MarkTaskDirty());
            m_taskForm.Add(m_nameField);
            ValidateTask();
            m_ensureOpen?.Invoke(m_nameField);
        }

        private void BuildFieldForm(bool includeName,
            GameDBInspectorFieldTypeDraft type)
        {
            m_taskForm.Clear();
            m_typeEditorHost.style.display = DisplayStyle.Flex;
            if (includeName)
            {
                var draft = (GameDBInspectorFieldDraft)m_state.Task.Draft;
                m_nameField = new TextField("Name")
                {
                    name = "inspector-task-name-field",
                    value = draft.Name
                };
                m_nameField.RegisterValueChangedCallback(_ => MarkTaskDirty());
                m_taskForm.Add(m_nameField);
            }
            else
            {
                m_nameField = null;
            }
            m_typeEditor.Bind(type, EnumChoices(), TableChoices(),
                m_snapshot.LocalizationDatabase);
            ValidateTask();
            m_ensureOpen?.Invoke(m_nameField as VisualElement
                ?? m_typeEditorHost.Q<DropdownField>("field-shape-field"));
        }

        private void SubmitTask()
        {
            if (m_state.Task == null || !ValidateTask())
            {
                return;
            }
            var task = m_state.Task;
            var current = m_tab.Session.CreateSnapshot();
            if (task.RecheckStaleness(current))
            {
                ShowTaskError("The target schema changed. Cancel and review the current schema.");
                return;
            }
            GameDBSchemaActionResult result;
            switch (task.Kind)
            {
                case GameDBInspectorTaskKind.EditDatabase:
                    result = m_actions.SetDatabaseMetadata(m_tab,
                        task.Context.DocumentId, current.Revision,
                        m_scopeField.value.Trim(), m_localizationToggle.value);
                    break;
                case GameDBInspectorTaskKind.CreateTable:
                    result = m_actions.AddTable(m_tab, task.Context.DocumentId,
                        current.Revision, m_nameField.value,
                        m_keyTypeField.value == "Enum" ? KeyType.@enum : KeyType.@string,
                        m_keyArgumentField.value);
                    break;
                case GameDBInspectorTaskKind.RenameTable:
                    result = m_actions.RenameTable(m_tab, task.Context.DocumentId,
                        current.Revision, task.Context.TableName, m_nameField.value);
                    break;
                case GameDBInspectorTaskKind.CreateField:
                    result = m_actions.AddField(m_tab, task.Context.DocumentId,
                        current.Revision, task.Context.TableName, m_nameField.value,
                        m_typeEditor.Validate().TypeSpec);
                    break;
                case GameDBInspectorTaskKind.RenameField:
                    result = m_actions.RenameField(m_tab, task.Context.DocumentId,
                        current.Revision, task.Context.TableName,
                        task.Context.FieldName, m_nameField.value);
                    break;
                case GameDBInspectorTaskKind.ChangeFieldType:
                    result = m_actions.ReplaceField(m_tab, task.Context.DocumentId,
                        current.Revision, task.Context.TableName,
                        task.Context.FieldName, m_typeEditor.Validate().TypeSpec);
                    break;
                default:
                    return;
            }
            if (!result.Success)
            {
                ShowTaskError(result.CommandResult?.Message
                    ?? "The Inspector action could not be completed.");
                return;
            }
            m_snapshot = result.Snapshot;
            var tableName = task.Kind == GameDBInspectorTaskKind.CreateTable
                || task.Kind == GameDBInspectorTaskKind.RenameTable
                ? m_nameField.value.Trim() : task.Context.TableName;
            var fieldName = task.Kind == GameDBInspectorTaskKind.CreateField
                || task.Kind == GameDBInspectorTaskKind.RenameField
                ? m_nameField.value.Trim() : task.Context.FieldName;
            m_selectedFieldName = fieldName;
            var context = !string.IsNullOrEmpty(fieldName)
                ? GameDBInspectorContext.Field(m_tab.TabId, m_tab.Session.DocumentId,
                    tableName, fieldName)
                : !string.IsNullOrEmpty(tableName)
                    ? GameDBInspectorContext.Table(m_tab.TabId,
                        m_tab.Session.DocumentId, tableName)
                    : GameDBInspectorContext.Database(m_tab.TabId,
                        m_tab.Session.DocumentId);
            m_pendingContinuation = null;
            m_state.CompleteTask(context);
            m_refreshPresentation?.Invoke();
            RenderContext();
        }

        private bool ValidateTask()
        {
            if (m_state.Task == null)
            {
                return false;
            }
            if (m_state.Task.IsStale)
            {
                ShowTaskError("The target schema changed. Cancel and review the current schema.");
                m_taskPrimary.SetEnabled(false);
                return false;
            }
            string message = null;
            if (m_nameField != null)
            {
                var name = m_nameField.value?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    message = "Name is required.";
                }
                else if (m_state.Task.Kind == GameDBInspectorTaskKind.CreateTable
                    && m_snapshot.Tables.Any(table => table.Name == name))
                {
                    message = "A table with this name already exists.";
                }
                else if (m_state.Task.Kind == GameDBInspectorTaskKind.CreateField
                    && m_snapshot.Tables.FirstOrDefault(table => table.Name
                        == m_state.Task.Context.TableName)?.Fields.Any(field =>
                            field.Name == name) == true)
                {
                    message = "A field with this name already exists.";
                }
            }
            if (message == null && (m_state.Task.Kind == GameDBInspectorTaskKind.CreateField
                || m_state.Task.Kind == GameDBInspectorTaskKind.ChangeFieldType))
            {
                var type = m_typeEditor.Validate();
                if (!type.Success)
                {
                    message = type.Message;
                }
            }
            if (message == null && m_state.Task.Kind == GameDBInspectorTaskKind.CreateTable
                && m_keyTypeField?.value == "Enum"
                && string.IsNullOrWhiteSpace(m_keyArgumentField?.value))
            {
                message = "Choose an imported enum key type.";
            }
            m_taskMessage.Clear();
            if (message != null)
            {
                m_taskMessage.Add(new HelpBox(message, HelpBoxMessageType.Error));
            }
            m_taskPrimary.SetEnabled(message == null && !m_dataOnlyEditing()
                && !m_state.Task.IsStale);
            return message == null;
        }

        private void MarkTaskDirty()
        {
            if (m_binding || m_state.Task == null)
            {
                return;
            }
            m_state.Task.MarkDirty();
            ValidateTask();
        }

        private void OnTypeChanged(GameDBInspectorFieldTypeDraft _,
            GameDBFieldTypeValidationResult __)
        {
            MarkTaskDirty();
        }

        private void CancelTask()
        {
            m_pendingContinuation = null;
            m_state.CancelTask();
            RenderContextAfterTask();
        }

        private void CancelPendingNavigation()
        {
            m_state.TakePendingIntent();
            m_pendingContinuation = null;
            m_decision.style.display = DisplayStyle.None;
            m_taskFooter.style.display = m_state.Task == null
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void DiscardAndContinue()
        {
            var intent = m_state.TakePendingIntent();
            m_state.CancelTask();
            ContinuePendingIntent(intent);
        }

        private void SaveAndContinue()
        {
            var intent = m_state.TakePendingIntent();
            var continuation = m_pendingContinuation;
            SubmitTask();
            m_pendingContinuation = continuation;
            if (m_state.Task == null)
            {
                ContinuePendingIntent(intent);
            }
            else if (intent != null)
            {
                m_state.TrySetPendingIntent(intent);
            }
        }

        private void ContinuePendingIntent(GameDBInspectorPendingIntent intent)
        {
            m_decision.style.display = DisplayStyle.None;
            var continuation = m_pendingContinuation;
            m_pendingContinuation = null;
            if (continuation != null)
            {
                RenderContextAfterTask();
                continuation();
                return;
            }
            if (intent?.Kind == GameDBInspectorPendingIntentKind.SelectField
                && intent.TargetContext != null)
            {
                RequestInspectField(intent.TargetContext.TableName,
                    intent.TargetContext.FieldName);
                return;
            }
            if (intent?.Kind != GameDBInspectorPendingIntentKind.SelectTable
                || intent.TargetContext == null || m_tab == null)
            {
                RenderContextAfterTask();
                return;
            }
            var current = m_tab.ViewState ?? new GameDBWorkspaceTabViewState();
            var viewState = new GameDBWorkspaceTabViewState(
                intent.TargetContext.TableName, null, current.SearchText,
                current.Sorts, current.Columns, current.HorizontalScroll,
                current.VerticalScroll);
            m_selectedFieldName = null;
            if (m_workspace.TrySetTabViewState(m_tab.TabId, viewState))
            {
                m_refreshPresentation?.Invoke();
            }
            else
            {
                ShowActionMessage("The selected table could not be opened.",
                    HelpBoxMessageType.Error);
                RenderContextAfterTask();
            }
        }

        private void RenderContextAfterTask()
        {
            if (m_tab == null || m_snapshot == null)
            {
                m_state.Reset();
                m_selectedFieldName = null;
                ShowEmpty();
                return;
            }
            var table = SelectedTable();
            var fieldName = m_state.Context?.FieldName;
            m_selectedFieldName = table?.Fields.Any(field => field.Name == fieldName) == true
                ? fieldName : null;
            m_state.SetContext(!string.IsNullOrEmpty(m_selectedFieldName)
                ? GameDBInspectorContext.Field(m_tab.TabId, m_tab.Session.DocumentId,
                    table.Name, m_selectedFieldName)
                : table != null ? GameDBInspectorContext.Table(m_tab.TabId,
                    m_tab.Session.DocumentId, table.Name)
                : GameDBInspectorContext.Database(m_tab.TabId,
                    m_tab.Session.DocumentId));
            RenderContext();
        }

        private void ShowTableContext()
        {
            if (m_state.Task != null)
            {
                return;
            }
            var table = SelectedTable();
            m_selectedFieldName = null;
            m_state.SetContext(table == null
                ? GameDBInspectorContext.Database(m_tab.TabId, m_tab.Session.DocumentId)
                : GameDBInspectorContext.Table(m_tab.TabId,
                    m_tab.Session.DocumentId, table.Name));
            RenderContext();
        }

        private void DeleteTable()
        {
            var table = SelectedTable();
            if (table == null || m_tab == null)
            {
                return;
            }
            ClearActionMessage();
            var current = m_tab.Session.CreateSnapshot();
            var result = m_actions.DeleteTable(m_tab, m_tab.Session.DocumentId,
                current.Revision, table.Name, current);
            if (result.Success)
            {
                m_selectedFieldName = null;
                m_refreshPresentation?.Invoke();
                return;
            }
            ShowActionFailure(result, "The table could not be deleted.");
        }

        private void DeleteField()
        {
            var table = SelectedTable();
            var field = SelectedField();
            if (table == null || field == null || m_tab == null)
            {
                return;
            }
            ClearActionMessage();
            var current = m_tab.Session.CreateSnapshot();
            var result = m_actions.DeleteField(m_tab, m_tab.Session.DocumentId,
                current.Revision, table.Name, field.Name);
            if (result.Success)
            {
                m_selectedFieldName = null;
                m_refreshPresentation?.Invoke();
                return;
            }
            ShowActionFailure(result, "The field could not be deleted.");
        }

        private void OnFieldSelectionChanged(IEnumerable<object> selection)
        {
            if (m_binding || m_state.Task != null)
            {
                return;
            }
            var field = selection.OfType<GameDBFieldSnapshot>().FirstOrDefault();
            if (field == null || m_tab == null)
            {
                return;
            }
            m_selectedFieldName = field.Name;
            var table = SelectedTable();
            m_state.SetContext(GameDBInspectorContext.Field(m_tab.TabId,
                m_tab.Session.DocumentId, table.Name, field.Name));
            RenderContext();
        }

        private void BindFieldItem(VisualElement element, int index)
        {
            var field = m_fields.itemsSource?[index] as GameDBFieldSnapshot;
            var label = (Label)element;
            label.text = field == null ? string.Empty
                : $"{field.Name}    {GameDBFieldTypeDraftAdapter.Format(DraftFrom(field))}";
            label.tooltip = field?.Name ?? string.Empty;
        }

        private void ToggleDatabase()
        {
            m_databaseExpanded = !m_databaseExpanded;
            m_databaseToggle.text = m_databaseExpanded ? "Database ⌄" : "Database ›";
            m_databaseScroll.style.display = m_databaseExpanded
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyKeyArgumentVisibility()
        {
            if (m_keyArgumentField != null)
            {
                m_keyArgumentField.style.display = m_keyTypeField?.value == "Enum"
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ValidateTask();
        }

        private void ApplyEditingMode()
        {
            var enabled = !m_dataOnlyEditing();
            m_tableCreate.SetEnabled(enabled && m_state.Task == null);
            m_tableRename.SetEnabled(enabled && SelectedTable() != null);
            m_tableDelete.SetEnabled(enabled && SelectedTable() != null);
            m_fieldCreate.SetEnabled(enabled && SelectedTable() != null);
            m_fieldRename.SetEnabled(enabled && SelectedField() != null);
            m_fieldChangeType.SetEnabled(enabled && SelectedField() != null);
            m_fieldDelete.SetEnabled(enabled && SelectedField() != null);
            m_databaseEdit.SetEnabled(enabled);
        }

        private bool CanStartTask()
        {
            return !m_disposed && m_tab != null && m_snapshot != null
                && m_state.Task == null && !m_dataOnlyEditing();
        }

        private GameDBTableSnapshot SelectedTable()
        {
            var tableName = m_tab?.ViewState?.SelectedTableId;
            return m_snapshot?.Tables.FirstOrDefault(table => table.Name == tableName)
                ?? m_snapshot?.Tables.FirstOrDefault();
        }

        private GameDBFieldSnapshot SelectedField()
        {
            return SelectedTable()?.Fields.FirstOrDefault(field =>
                field.Name == m_selectedFieldName);
        }

        private IReadOnlyList<string> EnumChoices()
        {
            try
            {
                return (m_importedEnumTypes() ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value,
                        StringComparer.Ordinal).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private IReadOnlyList<string> TableChoices()
        {
            return m_snapshot?.Tables.Select(table => table.Name)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray()
                ?? Array.Empty<string>();
        }

        private static GameDBInspectorFieldTypeDraft DraftFrom(
            GameDBFieldSnapshot field)
        {
            return new GameDBInspectorFieldTypeDraft(field.FieldType,
                field.IsArray, field.TypeArgument, field.DictionaryType);
        }

        private static string FormatKeyType(GameDBTableSnapshot table)
        {
            return table.KeyType == KeyType.@enum
                ? $"Enum key ({table.KeyTypeArgument})" : "String key";
        }

        private void ShowTaskError(string message)
        {
            m_taskMessage.Clear();
            m_taskMessage.Add(new HelpBox(message,
                HelpBoxMessageType.Error));
        }

        private void ShowActionFailure(GameDBSchemaActionResult result,
            string fallbackMessage)
        {
            var cancelled = result?.Status == GameDBSchemaActionStatus.Cancelled;
            var message = cancelled ? "Action cancelled."
                : result?.Status == GameDBSchemaActionStatus.TargetChangedAfterConfirmation
                    ? "The active database changed after confirmation. No schema change was made."
                    : result?.Status == GameDBSchemaActionStatus.TargetUnavailable
                        ? "The target database is no longer active. No schema change was made."
                        : result?.CommandResult?.Message ?? fallbackMessage;
            ShowActionMessage(message, cancelled
                ? HelpBoxMessageType.Info : HelpBoxMessageType.Error);
        }

        private void ShowActionMessage(string message, HelpBoxMessageType type)
        {
            m_actionMessageContext = m_state.Context;
            m_actionMessage.Clear();
            m_actionMessage.Add(new HelpBox(message, type));
        }

        private void ClearActionMessage()
        {
            m_actionMessageContext = null;
            m_actionMessage.Clear();
        }

        private void RenderTaskState()
        {
            if (m_dataOnlyEditing())
            {
                ShowTaskError("Schema tasks cannot be saved in Play Mode. Cancel this task to continue.");
                m_taskPrimary.SetEnabled(false);
            }
            else
            {
                ValidateTask();
            }
            ApplyEditingMode();
        }

        private void HideScreens()
        {
            m_tableView.style.display = DisplayStyle.None;
            m_fieldView.style.display = DisplayStyle.None;
            m_taskView.style.display = DisplayStyle.None;
            m_decision.style.display = DisplayStyle.None;
        }

        private void ShowEmpty()
        {
            HideScreens();
            m_eyebrow.text = "Inspector";
            m_title.text = "No database";
            m_back.style.display = DisplayStyle.None;
            m_tableView.style.display = DisplayStyle.Flex;
            m_tableSummary.text = "Open a GameDB database to inspect its schema.";
            m_binding = true;
            try
            {
                m_fields.itemsSource = null;
                m_fields.Rebuild();
            }
            finally
            {
                m_binding = false;
            }
            ClearActionMessage();
            m_databaseCard.style.display = DisplayStyle.None;
            m_taskFooter.style.display = DisplayStyle.None;
        }

        private static T Required<T>(VisualElement root, string name)
            where T : VisualElement
        {
            return root?.Q<T>(name) ?? throw new InvalidOperationException(
                $"Required GameDB Inspector element '{name}' was not found.");
        }
    }
}
