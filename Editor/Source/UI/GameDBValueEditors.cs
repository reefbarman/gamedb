using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
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
    internal sealed class GameDBValueEditIntent
    {
        internal string TableName { get; }
        internal string RowKey { get; }
        internal string FieldName { get; }
        internal object WireValue { get; }
        internal string ExpectedRevision { get; }

        internal GameDBValueEditIntent(string tableName, string rowKey,
            string fieldName, object wireValue, string expectedRevision)
        {
            TableName = tableName;
            RowKey = rowKey;
            FieldName = fieldName;
            WireValue = wireValue;
            ExpectedRevision = expectedRevision;
        }
    }

    internal sealed class GameDBValueEditResult
    {
        internal bool Success { get; }
        internal string Message { get; }
        internal GameDBSnapshot Snapshot { get; }

        internal GameDBValueEditResult(bool success, string message,
            GameDBSnapshot snapshot)
        {
            Success = success;
            Message = message;
            Snapshot = snapshot;
        }
    }

    internal sealed class GameDBRowCreateIntent
    {
        internal string TableName { get; }
        internal string RowKey { get; }
        internal string ExpectedRevision { get; }

        internal GameDBRowCreateIntent(string tableName, string rowKey,
            string expectedRevision)
        {
            TableName = tableName;
            RowKey = rowKey;
            ExpectedRevision = expectedRevision;
        }
    }

    internal sealed class GameDBRowRenameIntent
    {
        internal string TableName { get; }
        internal string CurrentKey { get; }
        internal string NewKey { get; }
        internal string ExpectedRevision { get; }
        internal string ExpectedDatabasePath { get; }

        internal GameDBRowRenameIntent(string tableName, string currentKey,
            string newKey, string expectedRevision, string expectedDatabasePath = null)
        {
            TableName = tableName;
            CurrentKey = currentKey;
            NewKey = newKey;
            ExpectedRevision = expectedRevision;
            ExpectedDatabasePath = expectedDatabasePath;
        }
    }

    internal sealed class GameDBRowDeleteIntent
    {
        internal string TableName { get; }
        internal string RowKey { get; }
        internal string ExpectedRevision { get; }

        internal GameDBRowDeleteIntent(string tableName, string rowKey,
            string expectedRevision)
        {
            TableName = tableName;
            RowKey = rowKey;
            ExpectedRevision = expectedRevision;
        }
    }

    internal sealed class GameDBRowMutationResult
    {
        internal bool Success { get; }
        internal string Message { get; }
        internal GameDBSnapshot Snapshot { get; }
        internal string CanonicalRowKey { get; }
        internal GameDBRowReferenceImpact ReferenceImpact { get; }

        internal GameDBRowMutationResult(bool success, string message,
            GameDBSnapshot snapshot, string canonicalRowKey,
            GameDBRowReferenceImpact referenceImpact)
        {
            Success = success;
            Message = message;
            Snapshot = snapshot;
            CanonicalRowKey = canonicalRowKey;
            ReferenceImpact = referenceImpact ?? GameDBRowReferenceImpact.None;
        }
    }

    internal static class GameDBValueEditorFactory
    {
        internal static VisualElement Create(GameDBFieldSnapshot field,
            Func<GameDBValueEditIntent, GameDBValueEditResult> edit,
            Action<GameDBCollectionEditRequest> editCollection = null)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (field.IsArray || field.FieldType == FieldType.dictionary)
            {
                return editCollection == null
                    ? new GameDBReadOnlyValueCell()
                    : new GameDBCollectionValueCell(field, editCollection);
            }
            return edit == null
                ? new GameDBReadOnlyValueCell()
                : new GameDBValueEditorCell(field, edit);
        }

        internal static void Bind(VisualElement element, GameDBFieldSnapshot field,
            GameDBSnapshot snapshot, GameDBTableSnapshot table,
            GameDBRowSnapshot row, string revision)
        {
            if (element is GameDBValueEditorCell editor)
            {
                editor.Bind(snapshot, table, row, revision);
                return;
            }
            if (element is GameDBCollectionValueCell collection)
            {
                collection.Bind(snapshot, table, row, revision);
                return;
            }

            var label = (GameDBReadOnlyValueCell)element;
            row.Values.TryGetValue(field.Name, out var value);
            label.Bind(row.Key, GameDBTableViewProjection.FormatValue(value));
        }

        internal static void Unbind(VisualElement element)
        {
            if (element is GameDBValueEditorCell editor)
            {
                editor.Unbind();
            }
            else if (element is GameDBCollectionValueCell collection)
            {
                collection.Unbind();
            }
            else
            {
                ((GameDBReadOnlyValueCell)element).Unbind();
            }
        }
    }

    internal sealed class GameDBCollectionValueCell : VisualElement
    {
        private readonly GameDBFieldSnapshot m_field;
        private readonly Action<GameDBCollectionEditRequest> m_edit;
        private readonly Label m_summary;
        private readonly Button m_button;
        private GameDBCollectionEditRequest m_request;

        internal GameDBCollectionValueCell(GameDBFieldSnapshot field,
            Action<GameDBCollectionEditRequest> edit)
        {
            m_field = field;
            m_edit = edit;
            AddToClassList("gamedb-editor__table-cell");
            AddToClassList("gamedb-editor__collection-cell");
            m_summary = new Label();
            m_summary.AddToClassList("gamedb-editor__collection-cell-summary");
            m_button = new Button(Open) { text = "Edit", tooltip = "Edit collection" };
            m_button.AddToClassList("gamedb-editor__collection-cell-action");
            Add(m_summary);
            Add(m_button);
        }

        internal void Bind(GameDBSnapshot snapshot, GameDBTableSnapshot table,
            GameDBRowSnapshot row, string revision)
        {
            row.Values.TryGetValue(m_field.Name, out var value);
            var count = value is ICollection collection
                ? collection.Count
                : value is IEnumerable enumerable && !(value is string)
                    ? enumerable.Cast<object>().Count() : 0;
            var unit = m_field.FieldType == FieldType.dictionary
                ? (count == 1 ? "entry" : "entries")
                : (count == 1 ? "item" : "items");
            m_summary.text = $"{count} {unit}";
            tooltip = GameDBTableViewProjection.FormatValue(value);
            userData = row.Key;
            m_request = new GameDBCollectionEditRequest(snapshot, table, row,
                m_field, revision, m_button);
            m_button.SetEnabled(true);
        }

        internal void Unbind()
        {
            m_request = null;
            m_summary.text = string.Empty;
            tooltip = string.Empty;
            userData = null;
            m_button.SetEnabled(false);
        }

        internal void Open()
        {
            if (m_request != null)
            {
                m_edit(m_request);
            }
        }
    }

    internal sealed class GameDBRowKeyEditorCell : VisualElement
    {
        private const string InvalidClass = "gamedb-editor__value-editor--invalid";
        private readonly Func<GameDBRowRenameIntent, GameDBRowMutationResult> m_rename;
        private readonly Label m_label;
        private VisualElement m_control;
        private GameDBSnapshot m_snapshot;
        private GameDBTableSnapshot m_table;
        private GameDBRowSnapshot m_row;
        private string m_revision;
        private bool m_editing;
        private bool m_suppressFocusCommit;
        private bool m_restoreFocusAfterCommit;
        private bool m_commitPending;
        private int m_bindingGeneration;

        internal VisualElement Control => m_control;
        internal bool IsEditing => m_editing;

        internal GameDBRowKeyEditorCell(
            Func<GameDBRowRenameIntent, GameDBRowMutationResult> rename)
        {
            m_rename = rename;
            AddToClassList("gamedb-editor__table-cell");
            AddToClassList("gamedb-editor__key-editor");
            focusable = true;
            m_label = new Label();
            m_label.AddToClassList("gamedb-editor__key-editor-label");
            Add(m_label);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        internal void Bind(GameDBSnapshot snapshot, GameDBTableSnapshot table,
            GameDBRowSnapshot row, string revision)
        {
            var preserveDraft = m_editing && m_table?.Name == table.Name
                && m_row?.Key == row.Key;
            if (!preserveDraft)
            {
                m_bindingGeneration++;
                CancelEdit(false);
            }
            m_snapshot = snapshot;
            m_table = table;
            m_row = row;
            m_revision = revision;
            userData = row.Key;
            m_label.text = row.Key;
            if (preserveDraft)
            {
                ValidateCurrentDraft();
            }
            else
            {
                tooltip = row.Key;
                RemoveFromClassList(InvalidClass);
            }
        }

        internal void Unbind()
        {
            m_bindingGeneration++;
            CancelEdit(false);
            m_snapshot = null;
            m_table = null;
            m_row = null;
            m_revision = null;
            userData = null;
            tooltip = string.Empty;
            m_label.text = string.Empty;
            RemoveFromClassList(InvalidClass);
        }

        internal bool BeginEdit()
        {
            if (m_editing || m_rename == null || m_table == null || m_row == null)
            {
                return false;
            }
            m_editing = true;
            RemoveFromClassList(InvalidClass);
            tooltip = string.Empty;
            m_label.style.display = DisplayStyle.None;
            if (m_table.KeyType == KeyType.@enum)
            {
                var names = string.IsNullOrWhiteSpace(m_table.KeyTypeArgument)
                    ? new List<string>()
                    : GameDBScalarDraftAdapter.EnumNames(new GameDBScalarDraftDescriptor(
                        FieldType.@enum, m_table.KeyTypeArgument, m_snapshot)).ToList();
                var used = new HashSet<string>(m_table.Rows
                    .Where(row => !ReferenceEquals(row, m_row)).Select(row => row.Key),
                    StringComparer.Ordinal);
                var choices = names.Where(name => !used.Contains(name)).ToList();
                if (!choices.Contains(m_row.Key))
                {
                    choices.Insert(0, m_row.Key);
                }
                var popup = new PopupField<string>(choices, m_row.Key);
                popup.RegisterValueChangedCallback(evt => RequestCommit(evt.newValue, true));
                popup.RegisterCallback<KeyDownEvent>(OnEnumKeyDown,
                    TrickleDown.TrickleDown);
                popup.RegisterCallback<FocusOutEvent>(OnEnumFocusOut);
                m_control = popup;
            }
            else
            {
                var field = new TextField();
                field.SetValueWithoutNotify(m_row.Key);
                field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                field.RegisterCallback<FocusOutEvent>(OnFocusOut);
                m_control = field;
            }
            m_control.AddToClassList("gamedb-editor__key-editor-control");
            Add(m_control);
            m_control.schedule.Execute(() =>
            {
                if (m_editing)
                {
                    m_control.Focus();
                    if (m_control is TextField text)
                    {
                        text.SelectAll();
                    }
                }
            });
            return true;
        }

        internal void Commit(string newKey)
        {
            if (!m_editing || m_table == null || m_row == null)
            {
                return;
            }
            var currentKey = m_row.Key;
            var normalizedKey = newKey?.Trim();
            if (string.Equals(currentKey, normalizedKey, StringComparison.Ordinal))
            {
                EndEdit(m_restoreFocusAfterCommit);
                return;
            }
            if (!ValidateDraft(newKey, out var validationMessage))
            {
                Reject(validationMessage);
                return;
            }
            var generation = m_bindingGeneration;
            var result = m_rename(new GameDBRowRenameIntent(
                m_table.Name, currentKey, newKey, m_revision,
                m_snapshot?.DatabasePath));
            if (generation != m_bindingGeneration || !m_editing)
            {
                return;
            }
            if (result?.Success == true)
            {
                var canonicalKey = result.CanonicalRowKey ?? newKey?.Trim();
                if (!ApplyCanonicalResult(result, canonicalKey))
                {
                    Reject("The renamed row could not be resolved from canonical data.");
                    return;
                }
                EndEdit(m_restoreFocusAfterCommit);
                return;
            }
            if (result?.Snapshot != null
                && !ApplyCanonicalResult(result, result.CanonicalRowKey ?? currentKey))
            {
                Unbind();
                return;
            }
            Reject(result?.Message ?? "The row key could not be renamed.");
        }

        internal void CancelEdit(bool restoreFocus = true)
        {
            if (!m_editing)
            {
                return;
            }
            m_suppressFocusCommit = true;
            EndEdit(false);
            m_suppressFocusCommit = false;
            if (restoreFocus && panel != null)
            {
                Focus();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && evt.clickCount == 2 && BeginEdit())
            {
                evt.StopImmediatePropagation();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CancelEdit();
                evt.StopImmediatePropagation();
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                RequestCommit(((TextField)m_control).value, true);
                evt.StopImmediatePropagation();
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (!m_suppressFocusCommit && m_editing && m_control is TextField field)
            {
                RequestCommit(field.value, false);
            }
        }

        private void OnEnumKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CancelEdit();
                evt.StopImmediatePropagation();
            }
        }

        private void OnEnumFocusOut(FocusOutEvent evt)
        {
            schedule.Execute(() =>
            {
                if (!m_suppressFocusCommit && m_editing && !m_commitPending)
                {
                    CancelEdit(false);
                }
            });
        }

        private void RequestCommit(string newKey, bool restoreFocus)
        {
            var row = m_row;
            var revision = m_revision;
            m_commitPending = true;
            schedule.Execute(() =>
            {
                m_commitPending = false;
                if (m_editing && ReferenceEquals(m_row, row)
                    && string.Equals(m_revision, revision,
                        StringComparison.OrdinalIgnoreCase))
                {
                    m_restoreFocusAfterCommit = restoreFocus;
                    Commit(newKey);
                }
            });
        }

        private bool ApplyCanonicalResult(GameDBRowMutationResult result,
            string canonicalKey)
        {
            var snapshot = result.Snapshot;
            var table = snapshot?.Tables.FirstOrDefault(candidate =>
                candidate.Name == m_table.Name);
            var row = table?.Rows.FirstOrDefault(candidate =>
                candidate.Key == canonicalKey);
            if (table == null || row == null)
            {
                return false;
            }
            m_snapshot = snapshot;
            m_table = table;
            m_row = row;
            m_revision = snapshot.Revision;
            m_label.text = canonicalKey;
            userData = canonicalKey;
            tooltip = canonicalKey;
            return true;
        }

        private void Reject(string message)
        {
            tooltip = message ?? string.Empty;
            AddToClassList(InvalidClass);
            m_control?.Focus();
        }

        private void ValidateCurrentDraft()
        {
            var draft = m_control is TextField text ? text.value
                : m_control is PopupField<string> popup ? popup.value : m_row.Key;
            if (ValidateDraft(draft, out var message))
            {
                tooltip = string.Empty;
                RemoveFromClassList(InvalidClass);
            }
            else
            {
                tooltip = message;
                AddToClassList(InvalidClass);
            }
        }

        private bool ValidateDraft(string newKey, out string message)
        {
            var key = newKey?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                message = "Enter a row key.";
                return false;
            }
            if (key == FieldBase.NullRefToken)
            {
                message = $"{FieldBase.NullRefToken} is reserved for null table references.";
                return false;
            }
            if (m_table.Rows.Any(row => row.Key == key && row.Key != m_row.Key))
            {
                message = $"A row with key '{key}' already exists.";
                return false;
            }
            message = null;
            return true;
        }

        private void EndEdit(bool restoreFocus)
        {
            m_editing = false;
            m_commitPending = false;
            m_suppressFocusCommit = true;
            if (m_control != null)
            {
                m_control.RemoveFromHierarchy();
                m_control = null;
            }
            m_suppressFocusCommit = false;
            m_label.style.display = DisplayStyle.Flex;
            RemoveFromClassList(InvalidClass);
            if (restoreFocus && panel != null)
            {
                Focus();
            }
        }
    }

    internal sealed class GameDBReadOnlyValueCell : Label
    {
        internal GameDBReadOnlyValueCell()
        {
            AddToClassList("gamedb-editor__table-cell");
        }

        internal void Bind(string rowKey, string value)
        {
            text = value ?? string.Empty;
            tooltip = text;
            userData = rowKey;
        }

        internal void Unbind()
        {
            text = string.Empty;
            tooltip = string.Empty;
            userData = null;
        }
    }

    internal sealed class GameDBValueEditorCell : VisualElement
    {
        private const string InvalidClass = "gamedb-editor__value-editor--invalid";
        private readonly GameDBFieldSnapshot m_field;
        private readonly Func<GameDBValueEditIntent, GameDBValueEditResult> m_edit;
        private readonly VisualElement m_control;
        private GameDBSnapshot m_snapshot;
        private GameDBTableSnapshot m_table;
        private GameDBRowSnapshot m_row;
        private string m_revision;
        private object m_canonicalValue;
        private bool m_binding;

        internal string FieldName => m_field.Name;
        internal VisualElement Control => m_control;

        internal GameDBValueEditorCell(GameDBFieldSnapshot field,
            Func<GameDBValueEditIntent, GameDBValueEditResult> edit)
        {
            m_field = field;
            m_edit = edit;
            AddToClassList("gamedb-editor__table-cell");
            AddToClassList("gamedb-editor__value-editor");
            m_control = CreateControl(field);
            m_control.AddToClassList("gamedb-editor__value-editor-control");
            Add(m_control);
            RegisterCallbacks();
        }

        internal void Bind(GameDBSnapshot snapshot, GameDBTableSnapshot table,
            GameDBRowSnapshot row, string revision)
        {
            m_binding = true;
            try
            {
                m_snapshot = snapshot;
                m_table = table;
                m_row = row;
                m_revision = revision;
                row.Values.TryGetValue(m_field.Name, out m_canonicalValue);
                userData = row.Key;
                tooltip = string.Empty;
                RemoveFromClassList(InvalidClass);
                SetControlValue(m_canonicalValue);
            }
            finally
            {
                m_binding = false;
            }
        }

        internal void Unbind()
        {
            m_binding = true;
            try
            {
                m_snapshot = null;
                m_table = null;
                m_row = null;
                m_revision = null;
                m_canonicalValue = null;
                userData = null;
                tooltip = string.Empty;
                RemoveFromClassList(InvalidClass);
                ClearControlValue();
            }
            finally
            {
                m_binding = false;
            }
        }

        private VisualElement CreateControl(GameDBFieldSnapshot field)
        {
            switch (field.FieldType)
            {
                case FieldType.@string:
                    return new TextField { isDelayed = true };
                case FieldType.@int:
                    return new IntegerField { isDelayed = true };
                case FieldType.@long:
                    return new LongField { isDelayed = true };
                case FieldType.@float:
                    return new FloatField { isDelayed = true };
                case FieldType.@double:
                    return new DoubleField { isDelayed = true };
                case FieldType.@bool:
                    return new Toggle();
                case FieldType.@enum:
                case FieldType.tableRef:
                    return new PopupField<string>(new List<string> { string.Empty }, 0);
                case FieldType.color:
                    return new ColorField();
                case FieldType.vector2:
                    return new Vector2Field();
                case FieldType.vector3:
                    return new Vector3Field();
                case FieldType.vector4:
                    return new Vector4Field();
                case FieldType.unityObject:
                    return new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
                default:
                    return new Label();
            }
        }

        private void RegisterCallbacks()
        {
            switch (m_control)
            {
                case TextField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    break;
                case IntegerField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    break;
                case LongField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    break;
                case FloatField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    break;
                case DoubleField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    break;
                case Toggle field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case PopupField<string> field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case ColorField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case Vector2Field field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case Vector3Field field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case Vector4Field field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
                case ObjectField field:
                    field.RegisterValueChangedCallback(change => ApplyControlValue(change.newValue));
                    break;
            }
        }

        private void OnKeyDown(KeyDownEvent change)
        {
            if (change.keyCode != KeyCode.Escape || m_binding || m_row == null)
            {
                return;
            }

            CancelDraft();
            change.StopImmediatePropagation();
        }

        internal void CancelDraft()
        {
            if (m_binding || m_row == null)
            {
                return;
            }

            m_binding = true;
            try
            {
                SetControlValue(m_canonicalValue);
                tooltip = string.Empty;
                RemoveFromClassList(InvalidClass);
            }
            finally
            {
                m_binding = false;
            }
        }

        internal void ApplyControlValue(object value)
        {
            switch (m_field.FieldType)
            {
                case FieldType.tableRef:
                    Commit((string)value == FieldBase.NullRefToken ? null : value);
                    break;
                case FieldType.color:
                    Commit(((UnityEngine.Color)value).ToGameDBColor().ToString());
                    break;
                case FieldType.vector2:
                    Commit(((UnityEngine.Vector2)value).ToGameDBVector().ToString());
                    break;
                case FieldType.vector3:
                    Commit(((UnityEngine.Vector3)value).ToGameDBVector().ToString());
                    break;
                case FieldType.vector4:
                    Commit(((UnityEngine.Vector4)value).ToGameDBVector().ToString());
                    break;
                case FieldType.unityObject:
                    CommitObject((UnityEngine.Object)value);
                    break;
                default:
                    Commit(value);
                    break;
            }
        }

        private void CommitObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                Commit(TypeHelpers.SerializeType(FieldType.unityObject, false,
                    UnityObjectReference.Empty));
                return;
            }

            var path = AssetDatabase.GetAssetPath(value);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid)
                || AssetDatabase.IsValidFolder(path) || !AssetDatabase.IsMainAsset(value))
            {
                Reject("Only main project assets beneath Assets can be used.");
                return;
            }

            try
            {
                Commit(TypeHelpers.SerializeType(FieldType.unityObject, false,
                    new UnityObjectReference(guid, path)));
            }
            catch (Exception exception)
            {
                Reject(exception.Message);
            }
        }

        private void Commit(object wireValue)
        {
            if (m_binding || m_table == null || m_row == null)
            {
                return;
            }

            var result = m_edit(new GameDBValueEditIntent(m_table.Name, m_row.Key,
                m_field.Name, wireValue, m_revision));
            if (result?.Snapshot != null)
            {
                ApplyCanonicalSnapshot(result.Snapshot);
            }
            if (result == null || !result.Success)
            {
                Reject(result?.Message ?? "The value could not be applied.");
            }
        }

        private void ApplyCanonicalSnapshot(GameDBSnapshot snapshot)
        {
            var table = snapshot.Tables.FirstOrDefault(candidate =>
                candidate.Name == m_table?.Name);
            var row = table?.Rows.FirstOrDefault(candidate =>
                candidate.Key == m_row?.Key);
            if (table == null || row == null)
            {
                return;
            }

            m_snapshot = snapshot;
            m_table = table;
            m_row = row;
            m_revision = snapshot.Revision;
            row.Values.TryGetValue(m_field.Name, out m_canonicalValue);
            m_binding = true;
            try
            {
                SetControlValue(m_canonicalValue);
                tooltip = string.Empty;
                RemoveFromClassList(InvalidClass);
            }
            finally
            {
                m_binding = false;
            }
        }

        private void Reject(string message)
        {
            m_binding = true;
            try
            {
                SetControlValue(m_canonicalValue);
                tooltip = message ?? string.Empty;
                EnableInClassList(InvalidClass, true);
            }
            finally
            {
                m_binding = false;
            }
        }

        private void SetControlValue(object value)
        {
            switch (m_control)
            {
                case TextField field:
                    field.SetValueWithoutNotify(value as string ?? string.Empty);
                    break;
                case IntegerField field:
                    field.SetValueWithoutNotify(Convert.ToInt32(value));
                    break;
                case LongField field:
                    field.SetValueWithoutNotify(Convert.ToInt64(value));
                    break;
                case FloatField field:
                    field.SetValueWithoutNotify(Convert.ToSingle(value));
                    break;
                case DoubleField field:
                    field.SetValueWithoutNotify(Convert.ToDouble(value));
                    break;
                case Toggle field:
                    field.SetValueWithoutNotify(Convert.ToBoolean(value));
                    break;
                case PopupField<string> field:
                    BindPopup(field, value);
                    break;
                case ColorField field:
                    field.SetValueWithoutNotify(((Color)value).ToUnityColor());
                    break;
                case Vector2Field field:
                    field.SetValueWithoutNotify(((Vector2)value).ToUnityVector());
                    break;
                case Vector3Field field:
                    field.SetValueWithoutNotify(((Vector3)value).ToUnityVector());
                    break;
                case Vector4Field field:
                    field.SetValueWithoutNotify(((Vector4)value).ToUnityVector());
                    break;
                case ObjectField field:
                    field.SetValueWithoutNotify(ResolveObject(value as UnityObjectReference));
                    EnableInClassList(InvalidClass,
                        value is UnityObjectReference reference && !reference.IsEmpty
                        && field.value == null);
                    break;
            }
        }

        private void BindPopup(PopupField<string> popup, object value)
        {
            var selected = m_field.FieldType == FieldType.tableRef
                ? value as string ?? FieldBase.NullRefToken
                : value?.ToString() ?? string.Empty;
            var choices = m_field.FieldType == FieldType.tableRef
                ? TableReferenceChoices()
                : EnumChoices(value);
            if (!choices.Contains(selected))
            {
                choices.Add(selected);
                EnableInClassList(InvalidClass, true);
                tooltip = $"'{selected}' is not a valid {m_field.FieldType} value.";
            }
            popup.choices = choices;
            popup.SetValueWithoutNotify(selected);
        }

        private List<string> TableReferenceChoices()
        {
            var choices = new List<string> { FieldBase.NullRefToken };
            var target = m_snapshot?.Tables.FirstOrDefault(table =>
                table.Name == m_field.TypeArgument);
            if (target != null)
            {
                choices.AddRange(target.Rows.Select(row => row.Key));
            }
            return choices;
        }


        private List<string> EnumChoices(object value)
        {
            var enumType = value?.GetType();
            if (enumType == null || !enumType.IsEnum)
            {
                enumType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => assembly.GetType(m_field.TypeArgument, false))
                    .FirstOrDefault(type => type != null && type.IsEnum);
            }
            return enumType == null
                ? new List<string>()
                : Enum.GetNames(enumType).ToList();
        }

        private static UnityEngine.Object ResolveObject(UnityObjectReference reference)
        {
            return reference == null || reference.IsEmpty
                ? null
                : AssetDatabase.LoadMainAssetAtPath(reference.Path);
        }

        private void ClearControlValue()
        {
            switch (m_control)
            {
                case TextField field: field.SetValueWithoutNotify(string.Empty); break;
                case IntegerField field: field.SetValueWithoutNotify(0); break;
                case LongField field: field.SetValueWithoutNotify(0L); break;
                case FloatField field: field.SetValueWithoutNotify(0f); break;
                case DoubleField field: field.SetValueWithoutNotify(0d); break;
                case Toggle field: field.SetValueWithoutNotify(false); break;
                case PopupField<string> field:
                    field.choices = new List<string> { string.Empty };
                    field.SetValueWithoutNotify(string.Empty);
                    break;
                case ColorField field: field.SetValueWithoutNotify(UnityEngine.Color.black); break;
                case Vector2Field field: field.SetValueWithoutNotify(UnityEngine.Vector2.zero); break;
                case Vector3Field field: field.SetValueWithoutNotify(UnityEngine.Vector3.zero); break;
                case Vector4Field field: field.SetValueWithoutNotify(UnityEngine.Vector4.zero); break;
                case ObjectField field: field.SetValueWithoutNotify(null); break;
            }
        }
    }
}
