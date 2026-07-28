using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Color = GameDBLibrary.Color;
using Vector2 = GameDBLibrary.Vector2;
using Vector3 = GameDBLibrary.Vector3;
using Vector4 = GameDBLibrary.Vector4;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBCollectionEditRequest
    {
        internal GameDBSnapshot Snapshot { get; }
        internal GameDBTableSnapshot Table { get; }
        internal GameDBRowSnapshot Row { get; }
        internal GameDBFieldSnapshot Field { get; }
        internal string Revision { get; }
        internal VisualElement FocusTarget { get; }

        internal GameDBCollectionEditRequest(GameDBSnapshot snapshot,
            GameDBTableSnapshot table, GameDBRowSnapshot row,
            GameDBFieldSnapshot field, string revision, VisualElement focusTarget)
        {
            Snapshot = snapshot;
            Table = table;
            Row = row;
            Field = field;
            Revision = revision;
            FocusTarget = focusTarget;
        }
    }

    internal sealed class GameDBCollectionEditorController : IDisposable
    {
        private sealed class Draft
        {
            internal int Id { get; }
            internal object Key { get; set; }
            internal object Value { get; set; }
            internal string Error { get; set; }

            internal Draft(int id, object key, object value)
            {
                Id = id;
                Key = key;
                Value = value;
            }
        }

        private sealed class DraftRow : VisualElement
        {
            private readonly Label m_index;
            private readonly VisualElement m_key;
            private readonly VisualElement m_value;
            private readonly Button m_remove;
            private readonly Action<Draft> m_changed;
            private readonly Action<Draft> m_removed;
            private Draft m_draft;
            private bool m_binding;

            internal DraftRow(GameDBScalarDraftDescriptor keyDescriptor,
                GameDBScalarDraftDescriptor valueDescriptor,
                Action<Draft> changed, Action<Draft> removed)
            {
                m_changed = changed;
                m_removed = removed;
                AddToClassList("gamedb-editor__collection-row");
                m_index = new Label();
                m_index.AddToClassList("gamedb-editor__collection-row-index");
                Add(m_index);
                if (keyDescriptor != null)
                {
                    m_key = GameDBScalarDraftAdapter.CreateControl(keyDescriptor, OnKeyChanged);
                    m_key.AddToClassList("gamedb-editor__collection-row-key");
                    Add(m_key);
                }
                m_value = GameDBScalarDraftAdapter.CreateControl(valueDescriptor, OnValueChanged);
                m_value.AddToClassList("gamedb-editor__collection-row-value");
                Add(m_value);
                m_remove = new Button(() =>
                {
                    if (m_draft != null)
                    {
                        m_removed(m_draft);
                    }
                })
                { text = "×", tooltip = "Remove" };
                m_remove.AddToClassList("gamedb-editor__collection-row-remove");
                Add(m_remove);
            }

            internal void Bind(Draft draft, int index,
                GameDBScalarDraftDescriptor keyDescriptor,
                GameDBScalarDraftDescriptor valueDescriptor)
            {
                m_binding = true;
                try
                {
                    m_draft = draft;
                    userData = draft.Id;
                    m_index.text = index.ToString();
                    if (m_key != null)
                    {
                        GameDBScalarDraftAdapter.SetStoredValue(m_key,
                            keyDescriptor, draft.Key);
                    }
                    GameDBScalarDraftAdapter.SetStoredValue(m_value,
                        valueDescriptor, draft.Value);
                    EnableInClassList("gamedb-editor__collection-row--invalid",
                        !string.IsNullOrEmpty(draft.Error));
                    tooltip = draft.Error ?? string.Empty;
                }
                finally
                {
                    m_binding = false;
                }
            }

            internal void Unbind()
            {
                m_draft = null;
                userData = null;
                m_index.text = string.Empty;
                tooltip = string.Empty;
                RemoveFromClassList("gamedb-editor__collection-row--invalid");
            }

            internal void FocusEditor()
            {
                (m_key ?? m_value).Focus();
            }

            private void OnKeyChanged(object value)
            {
                if (!m_binding && m_draft != null)
                {
                    m_draft.Key = value;
                    m_changed(m_draft);
                }
            }

            private void OnValueChanged(object value)
            {
                if (!m_binding && m_draft != null)
                {
                    m_draft.Value = value;
                    m_changed(m_draft);
                }
            }
        }

        private readonly GameDBEditorWorkspace m_workspace;
        private readonly GameDBEditorCommandService m_commands = new GameDBEditorCommandService();
        private readonly Action m_refresh;
        private readonly Func<bool> m_dataOnlyEditing;
        private readonly VisualElement m_host;
        private readonly VisualElement m_settingsPanel;
        private readonly VisualElement m_panel;
        private readonly Label m_title;
        private readonly Label m_context;
        private readonly VisualElement m_errorHost;
        private readonly ListView m_list;
        private readonly Button m_add;
        private readonly Button m_reload;
        private readonly Button m_apply;
        private readonly Button m_cancel;
        private readonly List<Draft> m_drafts = new List<Draft>();
        private GameDBCollectionEditRequest m_request;
        private GameDBAssetSession m_session;
        private string m_documentId;
        private GameDBScalarDraftDescriptor m_keyDescriptor;
        private GameDBScalarDraftDescriptor m_valueDescriptor;
        private VisualElement m_focusTarget;
        private int m_nextId;
        private bool m_stale;
        private bool m_unrecoverable;
        private bool m_disposed;

        internal bool IsOpen => m_request != null;
        internal bool IsStale => m_stale;
        internal IReadOnlyList<object> DraftKeys => m_drafts.Select(draft => draft.Key).ToArray();
        internal IReadOnlyList<object> DraftValues => m_drafts.Select(draft => draft.Value).ToArray();
        internal IReadOnlyList<string> DraftErrors => m_drafts.Select(draft => draft.Error).ToArray();

        internal GameDBCollectionEditorController(VisualElement root,
            GameDBEditorWorkspace workspace, Action refreshPresentation = null,
            Func<bool> dataOnlyEditing = null)
        {
            m_workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            m_refresh = refreshPresentation;
            m_dataOnlyEditing = dataOnlyEditing ?? (() => false);
            m_host = Required<VisualElement>(root, "modal-host");
            m_settingsPanel = Required<VisualElement>(root, "settings-panel");
            m_panel = Required<VisualElement>(root, "collection-editor-panel");
            m_title = Required<Label>(root, "collection-editor-title");
            m_context = Required<Label>(root, "collection-editor-context");
            m_errorHost = Required<VisualElement>(root, "collection-editor-error-host");
            m_list = Required<ListView>(root, "collection-editor-list");
            m_add = Required<Button>(root, "collection-add-button");
            m_reload = Required<Button>(root, "collection-reload-button");
            m_apply = Required<Button>(root, "collection-apply-button");
            m_cancel = Required<Button>(root, "collection-cancel-button");
            m_add.clicked += Add;
            m_reload.clicked += ReloadCurrent;
            m_apply.clicked += ApplyFromButton;
            m_cancel.clicked += Cancel;
            m_host.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Close(false);
        }

        internal bool Open(GameDBCollectionEditRequest request)
        {
            if (m_disposed || request?.Snapshot == null || request.Table == null
                || request.Row == null || request.Field == null
                || (!request.Field.IsArray && request.Field.FieldType != FieldType.dictionary))
            {
                return false;
            }
            var tab = m_workspace.ActiveTab;
            if (tab == null)
            {
                return false;
            }
            m_session = tab.Session;
            m_documentId = m_session.DocumentId;
            m_request = request;
            m_focusTarget = request.FocusTarget;
            m_stale = false;
            m_unrecoverable = false;
            BuildDescriptors(request);
            LoadDrafts(request.Row.Values.TryGetValue(request.Field.Name, out var value)
                ? value : null);
            ConfigureList();
            m_settingsPanel.style.display = DisplayStyle.None;
            m_panel.style.display = DisplayStyle.Flex;
            m_host.style.display = DisplayStyle.Flex;
            m_host.pickingMode = PickingMode.Position;
            m_title.text = request.Field.FieldType == FieldType.dictionary
                ? "Edit Dictionary" : "Edit Array";
            m_context.text = $"{request.Table.Name}[{request.Row.Key}].{request.Field.Name}";
            ValidateDrafts();
            m_panel.schedule.Execute(m_add.Focus).ExecuteLater(1);
            return true;
        }

        internal void Add()
        {
            if (!IsOpen || m_stale || m_unrecoverable)
            {
                return;
            }
            object key = null;
            if (m_keyDescriptor != null)
            {
                key = m_keyDescriptor.Type == FieldType.@enum
                    ? FirstUnusedEnumKey() : string.Empty;
                if (key == null)
                {
                    ShowError(GameDBScalarDraftAdapter.EnumNames(m_keyDescriptor).Count == 0
                        ? "The dictionary enum key type could not be resolved."
                        : "Every enum key is already present in the dictionary.");
                    return;
                }
            }
            m_drafts.Add(new Draft(m_nextId++, key,
                GameDBScalarDraftAdapter.DefaultStoredValue(m_valueDescriptor)));
            RefreshRows();
            ValidateDrafts();
        }

        internal void SetDraftKey(int index, object value)
        {
            if (!IsOpen || m_disposed)
            {
                throw new InvalidOperationException("No collection editor is open.");
            }
            if (m_keyDescriptor == null)
            {
                throw new InvalidOperationException("The open collection is not a dictionary.");
            }
            if (index < 0 || index >= m_drafts.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            m_drafts[index].Key = value;
            ValidateDrafts();
        }

        internal void SetDraftValue(int index, object value)
        {
            if (!IsOpen || m_disposed)
            {
                throw new InvalidOperationException("No collection editor is open.");
            }
            if (index < 0 || index >= m_drafts.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            m_drafts[index].Value = value;
            ValidateDrafts();
        }

        internal GameDBEditorCommandResult Apply()
        {
            if (!IsOpen || m_stale || m_unrecoverable || !ValidateDrafts())
            {
                return null;
            }
            var active = m_workspace.ActiveTab;
            if (active == null || !ReferenceEquals(active.Session, m_session)
                || m_session.IsDisposed || m_session.DocumentId != m_documentId)
            {
                m_unrecoverable = true;
                ShowError("The active GameDB document changed. Cancel and reopen the collection editor.");
                m_apply.SetEnabled(false);
                return null;
            }

            object wireValue;
            try
            {
                wireValue = BuildWireValue();
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
                return null;
            }
            var result = m_commands.Execute(m_session,
                new SetValueCommand(m_request.Table.Name, m_request.Row.Key,
                    m_request.Field.Name, wireValue), m_request.Revision,
                allowedOperations: m_dataOnlyEditing()
                    ? GameDBEditorCommandService.DataOnlyOperations : null);
            if (result.Success)
            {
                Close(true);
                m_refresh?.Invoke();
            }
            else
            {
                m_stale = result.FailureKind == GameDBTransactionFailureKind.RevisionConflict;
                m_reload.style.display = m_stale ? DisplayStyle.Flex : DisplayStyle.None;
                m_apply.SetEnabled(!m_stale);
                ShowError(result.Message);
            }
            return result;
        }

        internal void ReloadCurrent()
        {
            if (!IsOpen || m_session == null || m_session.IsDisposed)
            {
                return;
            }
            var snapshot = m_session.CreateSnapshot();
            var table = snapshot.Tables.FirstOrDefault(candidate =>
                candidate.Name == m_request.Table.Name);
            var field = table?.Fields.FirstOrDefault(candidate =>
                candidate.Name == m_request.Field.Name);
            var row = table?.Rows.FirstOrDefault(candidate =>
                candidate.Key == m_request.Row.Key);
            if (field == null || row == null || !SameFieldShape(field, m_request.Field))
            {
                m_unrecoverable = true;
                ShowError("The collection target no longer exists or its type changed.");
                m_apply.SetEnabled(false);
                return;
            }
            m_request = new GameDBCollectionEditRequest(snapshot, table, row, field,
                snapshot.Revision, m_focusTarget);
            BuildDescriptors(m_request);
            LoadDrafts(row.Values.TryGetValue(field.Name, out var value) ? value : null);
            ConfigureList();
            m_stale = false;
            m_unrecoverable = false;
            ValidateDrafts();
        }

        internal void Cancel() => Close(true);

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            m_add.clicked -= Add;
            m_reload.clicked -= ReloadCurrent;
            m_apply.clicked -= ApplyFromButton;
            m_cancel.clicked -= Cancel;
            m_host.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Close(false);
        }

        private void ApplyFromButton()
        {
            Apply();
        }

        private void BuildDescriptors(GameDBCollectionEditRequest request)
        {
            var field = request.Field;
            if (field.FieldType == FieldType.dictionary)
            {
                var dictionary = field.DictionaryType
                    ?? throw new InvalidOperationException("Dictionary type metadata is missing.");
                m_keyDescriptor = new GameDBScalarDraftDescriptor(
                    dictionary.KeyType == KeyType.@enum ? FieldType.@enum : FieldType.@string,
                    dictionary.KeyTypeArgument, request.Snapshot);
                m_valueDescriptor = new GameDBScalarDraftDescriptor(dictionary.ValueType,
                    dictionary.ValueTypeArgument, request.Snapshot);
            }
            else
            {
                m_keyDescriptor = null;
                m_valueDescriptor = new GameDBScalarDraftDescriptor(field.FieldType,
                    field.TypeArgument, request.Snapshot);
            }
        }

        private void LoadDrafts(object value)
        {
            m_drafts.Clear();
            m_nextId = 0;
            if (m_keyDescriptor != null && value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    m_drafts.Add(new Draft(m_nextId++, entry.Key, entry.Value));
                }
            }
            else if (m_keyDescriptor == null && value is IEnumerable values
                && !(value is string))
            {
                foreach (var item in values)
                {
                    m_drafts.Add(new Draft(m_nextId++, null, item));
                }
            }
        }

        private void ConfigureList()
        {
            m_list.focusable = false;
            m_list.makeItem = () => new DraftRow(m_keyDescriptor, m_valueDescriptor,
                _ => ValidateDrafts(), Remove);
            m_list.bindItem = (element, index) => ((DraftRow)element).Bind(
                m_drafts[index], index, m_keyDescriptor, m_valueDescriptor);
            m_list.unbindItem = (element, _) => ((DraftRow)element).Unbind();
            m_list.itemsSource = m_drafts;
            RefreshRows();
        }

        private void Remove(Draft draft)
        {
            if (!IsOpen || m_disposed || m_stale || m_unrecoverable)
            {
                return;
            }
            m_drafts.Remove(draft);
            RefreshRows();
            ValidateDrafts();
        }

        private void RefreshRows()
        {
            m_list.RefreshItems();
            m_context.text = m_request == null ? string.Empty
                : $"{m_request.Table.Name}[{m_request.Row.Key}].{m_request.Field.Name} • {m_drafts.Count} "
                    + (m_keyDescriptor == null ? "items" : "entries");
        }

        private bool ValidateDrafts()
        {
            ClearError();
            foreach (var draft in m_drafts)
            {
                draft.Error = null;
            }
            var wireKeys = new Dictionary<string, List<Draft>>(StringComparer.Ordinal);
            foreach (var draft in m_drafts)
            {
                try
                {
                    if (m_keyDescriptor != null)
                    {
                        var key = (string)GameDBScalarDraftAdapter.ToWireValue(
                            m_keyDescriptor, draft.Key);
                        if (!wireKeys.TryGetValue(key, out var duplicates))
                        {
                            duplicates = new List<Draft>();
                            wireKeys.Add(key, duplicates);
                        }
                        duplicates.Add(draft);
                    }
                    GameDBScalarDraftAdapter.ToWireValue(m_valueDescriptor, draft.Value);
                }
                catch (Exception exception)
                {
                    draft.Error = exception.Message;
                }
            }
            foreach (var duplicates in wireKeys.Values.Where(values => values.Count > 1))
            {
                foreach (var draft in duplicates)
                {
                    draft.Error = "Dictionary keys must be unique.";
                }
            }
            var error = m_drafts.FirstOrDefault(draft => !string.IsNullOrEmpty(draft.Error))?.Error;
            if (error != null)
            {
                ShowError(error);
            }
            m_apply.SetEnabled(!m_stale && !m_unrecoverable && error == null);
            m_reload.style.display = m_stale ? DisplayStyle.Flex : DisplayStyle.None;
            m_list.RefreshItems();
            return error == null;
        }

        private object BuildWireValue()
        {
            if (m_keyDescriptor == null)
            {
                return m_drafts.Select(draft =>
                    GameDBScalarDraftAdapter.ToWireValue(m_valueDescriptor, draft.Value)).ToList();
            }
            var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var draft in m_drafts)
            {
                dictionary.Add((string)GameDBScalarDraftAdapter.ToWireValue(
                    m_keyDescriptor, draft.Key),
                    GameDBScalarDraftAdapter.ToWireValue(m_valueDescriptor, draft.Value));
            }
            return dictionary;
        }

        private object FirstUnusedEnumKey()
        {
            var used = new HashSet<string>(m_drafts.Select(draft => draft.Key?.ToString()),
                StringComparer.Ordinal);
            return GameDBScalarDraftAdapter.EnumNames(m_keyDescriptor)
                .FirstOrDefault(name => !used.Contains(name));
        }


        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsOpen && evt.keyCode == KeyCode.Escape)
            {
                Cancel();
                evt.StopImmediatePropagation();
            }
        }

        private void Close(bool restoreFocus)
        {
            m_request = null;
            m_session = null;
            m_documentId = null;
            m_keyDescriptor = null;
            m_valueDescriptor = null;
            m_drafts.Clear();
            m_stale = false;
            m_unrecoverable = false;
            m_list.itemsSource = null;
            m_list.makeItem = null;
            m_list.bindItem = null;
            m_list.unbindItem = null;
            m_panel.style.display = DisplayStyle.None;
            if (m_settingsPanel.style.display.value == DisplayStyle.None)
            {
                m_host.style.display = DisplayStyle.None;
                m_host.pickingMode = PickingMode.Ignore;
            }
            ClearError();
            if (restoreFocus && m_focusTarget?.panel != null)
            {
                m_focusTarget.Focus();
            }
            m_focusTarget = null;
        }

        private void ShowError(string message)
        {
            m_errorHost.Clear();
            m_errorHost.Add(new HelpBox(message ?? "The collection could not be applied.",
                HelpBoxMessageType.Error));
        }

        private void ClearError() => m_errorHost.Clear();

        private static bool SameFieldShape(GameDBFieldSnapshot first,
            GameDBFieldSnapshot second)
        {
            return first.FieldType == second.FieldType && first.IsArray == second.IsArray
                && first.TypeArgument == second.TypeArgument
                && Equals(first.DictionaryType?.KeyType, second.DictionaryType?.KeyType)
                && first.DictionaryType?.KeyTypeArgument == second.DictionaryType?.KeyTypeArgument
                && Equals(first.DictionaryType?.ValueType, second.DictionaryType?.ValueType)
                && first.DictionaryType?.ValueTypeArgument == second.DictionaryType?.ValueTypeArgument;
        }

        private static T Required<T>(VisualElement root, string name)
            where T : VisualElement
        {
            return root?.Q<T>(name) ?? throw new InvalidOperationException(
                $"Required GameDB collection control '{name}' was not found.");
        }
    }

    internal sealed class GameDBScalarDraftDescriptor
    {
        internal FieldType Type { get; }
        internal string TypeArgument { get; }
        internal GameDBSnapshot Snapshot { get; }
        internal IReadOnlyList<string> EnumNames { get; }

        internal GameDBScalarDraftDescriptor(FieldType type, string typeArgument,
            GameDBSnapshot snapshot)
        {
            if (type == FieldType.dictionary)
            {
                throw new ArgumentException("Nested dictionaries are not supported.");
            }
            Type = type;
            TypeArgument = typeArgument;
            Snapshot = snapshot;
            if (type == FieldType.@enum)
            {
                var enumType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => assembly.GetType(typeArgument, false))
                    .FirstOrDefault(candidate => candidate?.IsEnum == true);
                EnumNames = enumType == null
                    ? Array.Empty<string>() : Enum.GetNames(enumType);
            }
            else
            {
                EnumNames = Array.Empty<string>();
            }
        }
    }

    internal static class GameDBScalarDraftAdapter
    {
        internal static VisualElement CreateControl(GameDBScalarDraftDescriptor descriptor,
            Action<object> changed)
        {
            VisualElement control;
            switch (descriptor.Type)
            {
                case FieldType.@string: control = new TextField { isDelayed = true }; break;
                case FieldType.@int: control = new IntegerField { isDelayed = true }; break;
                case FieldType.@long: control = new LongField { isDelayed = true }; break;
                case FieldType.@float: control = new FloatField { isDelayed = true }; break;
                case FieldType.@double: control = new DoubleField { isDelayed = true }; break;
                case FieldType.@bool: control = new Toggle(); break;
                case FieldType.@enum:
                    control = new PopupField<string>(EnumNames(descriptor).ToList(), 0); break;
                case FieldType.tableRef:
                    control = new PopupField<string>(TableReferenceChoices(descriptor), 0); break;
                case FieldType.color: control = new ColorField(); break;
                case FieldType.vector2: control = new Vector2Field(); break;
                case FieldType.vector3: control = new Vector3Field(); break;
                case FieldType.vector4: control = new Vector4Field(); break;
                case FieldType.unityObject:
                    control = new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(descriptor));
            }
            Register(control, changed);
            return control;
        }

        internal static void SetStoredValue(VisualElement control,
            GameDBScalarDraftDescriptor descriptor, object value)
        {
            switch (control)
            {
                case TextField field: field.SetValueWithoutNotify(value as string ?? string.Empty); break;
                case IntegerField field: field.SetValueWithoutNotify(Convert.ToInt32(value)); break;
                case LongField field: field.SetValueWithoutNotify(Convert.ToInt64(value)); break;
                case FloatField field: field.SetValueWithoutNotify(Convert.ToSingle(value)); break;
                case DoubleField field: field.SetValueWithoutNotify(Convert.ToDouble(value)); break;
                case Toggle field: field.SetValueWithoutNotify(Convert.ToBoolean(value)); break;
                case PopupField<string> field:
                    var selected = descriptor.Type == FieldType.tableRef
                        ? value as string ?? FieldBase.NullRefToken
                        : value?.ToString() ?? EnumNames(descriptor).FirstOrDefault() ?? string.Empty;
                    if (!field.choices.Contains(selected))
                    {
                        field.choices = field.choices.Concat(new[] { selected }).ToList();
                    }
                    field.SetValueWithoutNotify(selected);
                    break;
                case ColorField field:
                    field.SetValueWithoutNotify(value is UnityEngine.Color unityColor
                        ? unityColor : value is Color color
                            ? color.ToUnityColor() : UnityEngine.Color.clear);
                    break;
                case Vector2Field field:
                    field.SetValueWithoutNotify(value is UnityEngine.Vector2 unityVector2
                        ? unityVector2 : value is Vector2 vector2
                            ? vector2.ToUnityVector() : UnityEngine.Vector2.zero);
                    break;
                case Vector3Field field:
                    field.SetValueWithoutNotify(value is UnityEngine.Vector3 unityVector3
                        ? unityVector3 : value is Vector3 vector3
                            ? vector3.ToUnityVector() : UnityEngine.Vector3.zero);
                    break;
                case Vector4Field field:
                    field.SetValueWithoutNotify(value is UnityEngine.Vector4 unityVector4
                        ? unityVector4 : value is Vector4 vector4
                            ? vector4.ToUnityVector() : UnityEngine.Vector4.zero);
                    break;
                case ObjectField field:
                    if (value is UnityEngine.Object asset)
                    {
                        field.SetValueWithoutNotify(asset);
                        break;
                    }
                    var reference = value as UnityObjectReference;
                    field.SetValueWithoutNotify(reference == null || reference.IsEmpty
                        ? null : AssetDatabase.LoadMainAssetAtPath(reference.Path));
                    break;
            }
        }

        internal static object DefaultStoredValue(GameDBScalarDraftDescriptor descriptor)
        {
            switch (descriptor.Type)
            {
                case FieldType.@string: return string.Empty;
                case FieldType.@int: return 0;
                case FieldType.@long: return 0L;
                case FieldType.@float: return 0f;
                case FieldType.@double: return 0d;
                case FieldType.@bool: return false;
                case FieldType.@enum: return EnumNames(descriptor).FirstOrDefault();
                case FieldType.tableRef: return null;
                case FieldType.color: return new Color(0, 0, 0, 255);
                case FieldType.vector2: return new Vector2(0f, 0f);
                case FieldType.vector3: return new Vector3(0f, 0f, 0f);
                case FieldType.vector4: return new Vector4(0f, 0f, 0f, 0f);
                case FieldType.unityObject: return UnityObjectReference.Empty;
                default: return null;
            }
        }

        internal static object ToWireValue(GameDBScalarDraftDescriptor descriptor,
            object value)
        {
            switch (descriptor.Type)
            {
                case FieldType.@enum:
                    var enumName = value?.ToString();
                    if (!EnumNames(descriptor).Contains(enumName))
                    {
                        throw new InvalidOperationException($"'{enumName}' is not a valid enum value.");
                    }
                    return enumName;
                case FieldType.tableRef:
                    var rowKey = value as string;
                    return rowKey == FieldBase.NullRefToken || string.IsNullOrEmpty(rowKey)
                        ? null : rowKey;
                case FieldType.color:
                    return value is UnityEngine.Color unityColor
                        ? unityColor.ToGameDBColor().ToString() : ((Color)value).ToString();
                case FieldType.vector2:
                    return value is UnityEngine.Vector2 unityVector2
                        ? unityVector2.ToGameDBVector().ToString() : ((Vector2)value).ToString();
                case FieldType.vector3:
                    return value is UnityEngine.Vector3 unityVector3
                        ? unityVector3.ToGameDBVector().ToString() : ((Vector3)value).ToString();
                case FieldType.vector4:
                    return value is UnityEngine.Vector4 unityVector4
                        ? unityVector4.ToGameDBVector().ToString() : ((Vector4)value).ToString();
                case FieldType.unityObject:
                    if (value is UnityObjectReference reference)
                    {
                        return TypeHelpers.SerializeType(FieldType.unityObject, false, reference);
                    }
                    var asset = value as UnityEngine.Object;
                    if (asset == null)
                    {
                        return TypeHelpers.SerializeType(FieldType.unityObject, false,
                            UnityObjectReference.Empty);
                    }
                    var path = AssetDatabase.GetAssetPath(asset);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid)
                        || AssetDatabase.IsValidFolder(path) || !AssetDatabase.IsMainAsset(asset))
                    {
                        throw new InvalidOperationException(
                            "Only main project assets beneath Assets can be used.");
                    }
                    return TypeHelpers.SerializeType(FieldType.unityObject, false,
                        new UnityObjectReference(guid, path));
                default:
                    return value;
            }
        }

        internal static IReadOnlyList<string> EnumNames(
            GameDBScalarDraftDescriptor descriptor)
        {
            return descriptor.EnumNames;
        }

        private static List<string> TableReferenceChoices(
            GameDBScalarDraftDescriptor descriptor)
        {
            var choices = new List<string> { FieldBase.NullRefToken };
            var table = descriptor.Snapshot?.Tables.FirstOrDefault(candidate =>
                candidate.Name == descriptor.TypeArgument);
            if (table != null)
            {
                choices.AddRange(table.Rows.Select(row => row.Key));
            }
            return choices;
        }

        private static void Register(VisualElement control, Action<object> changed)
        {
            switch (control)
            {
                case TextField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case IntegerField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case LongField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case FloatField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case DoubleField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case Toggle field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case PopupField<string> field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case ColorField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case Vector2Field field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case Vector3Field field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case Vector4Field field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
                case ObjectField field: field.RegisterValueChangedCallback(evt => changed(evt.newValue)); break;
            }
        }
    }
}
