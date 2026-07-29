using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBAddRowRequest
    {
        internal GameDBSnapshot Snapshot { get; }
        internal GameDBTableSnapshot Table { get; }
        internal string Revision { get; }
        internal VisualElement FocusTarget { get; }

        internal GameDBAddRowRequest(GameDBSnapshot snapshot,
            GameDBTableSnapshot table, string revision, VisualElement focusTarget)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Revision = revision;
            FocusTarget = focusTarget;
        }
    }

    internal sealed class GameDBTableViewController : IDisposable
    {
        private const float MinimumColumnWidth = 48f;
        private const float MaximumColumnWidth = 600f;
        private const float HeaderChromeWidth = 28f;
        private const float CellChromeWidth = 16f;
        private const float FieldChromeWidth = 32f;
        private const float CollectionChromeWidth = 64f;
        private const string ResizeDragAreaClass =
            "unity-multi-column-header__column-resize-handle__drag-area";
        private const string HeaderTitleClass =
            "unity-multi-column-header__column__title";
        private readonly ToolbarButton m_addRow;
        private readonly ToolbarButton m_deleteRow;
        private readonly ToolbarButton m_columns;
        private readonly ToolbarSearchField m_search;
        private readonly ListView m_tableNavigation;
        private readonly MultiColumnListView m_grid;
        private readonly VisualElement m_actionMessageHost;
        private readonly VisualElement m_emptyState;
        private readonly Label m_emptyMessage;
        private readonly Button m_emptyAction;
        private readonly Action<GameDBWorkspaceTabViewState> m_viewStateChanged;
        private readonly Func<GameDBAddRowRequest, bool> m_addRowRequested;
        private readonly Func<GameDBRowCreateIntent, GameDBRowMutationResult> m_createRow;
        private readonly Func<GameDBRowRenameIntent, GameDBRowMutationResult> m_renameRow;
        private readonly Func<GameDBRowDeleteIntent, GameDBRowMutationResult> m_deleteRowIntent;
        private readonly Func<GameDBValueEditIntent, GameDBValueEditResult> m_editValue;
        private readonly Action<GameDBCollectionEditRequest> m_editCollection;
        private GameDBTableViewProjection m_projection;
        private GameDBSnapshot m_snapshot;
        private GameDBWorkspaceTabViewState m_viewState;
        private readonly List<Column> m_displayColumns = new List<Column>();
        private string m_columnSignature;
        private string m_pendingActionMessage;
        private bool m_binding;
        private bool m_disposed;

        internal GameDBTableViewController(ToolbarButton addRow,
            ToolbarButton deleteRow, ToolbarButton columns,
            ToolbarSearchField search, ListView tableNavigation,
            MultiColumnListView grid, VisualElement actionMessageHost,
            VisualElement emptyState,
            Label emptyMessage, Button emptyAction,
            Action<GameDBWorkspaceTabViewState> viewStateChanged,
            Func<GameDBAddRowRequest, bool> addRowRequested = null,
            Func<GameDBRowCreateIntent, GameDBRowMutationResult> createRow = null,
            Func<GameDBRowRenameIntent, GameDBRowMutationResult> renameRow = null,
            Func<GameDBRowDeleteIntent, GameDBRowMutationResult> deleteRowIntent = null,
            Func<GameDBValueEditIntent, GameDBValueEditResult> editValue = null,
            Action<GameDBCollectionEditRequest> editCollection = null)
        {
            m_addRow = addRow ?? throw new ArgumentNullException(nameof(addRow));
            m_deleteRow = deleteRow ?? throw new ArgumentNullException(nameof(deleteRow));
            m_columns = columns ?? throw new ArgumentNullException(nameof(columns));
            m_search = search ?? throw new ArgumentNullException(nameof(search));
            m_tableNavigation = tableNavigation
                ?? throw new ArgumentNullException(nameof(tableNavigation));
            m_grid = grid ?? throw new ArgumentNullException(nameof(grid));
            m_actionMessageHost = actionMessageHost
                ?? throw new ArgumentNullException(nameof(actionMessageHost));
            m_emptyState = emptyState ?? throw new ArgumentNullException(nameof(emptyState));
            m_emptyMessage = emptyMessage ?? throw new ArgumentNullException(nameof(emptyMessage));
            m_emptyAction = emptyAction ?? throw new ArgumentNullException(nameof(emptyAction));
            m_viewStateChanged = viewStateChanged
                ?? throw new ArgumentNullException(nameof(viewStateChanged));
            m_addRowRequested = addRowRequested;
            m_createRow = createRow;
            m_renameRow = renameRow;
            m_deleteRowIntent = deleteRowIntent;
            m_editValue = editValue;
            m_editCollection = editCollection;

            m_addRow.clicked += RequestAddRowFromToolbar;
            m_emptyAction.clicked += RequestAddRowFromEmptyState;
            m_deleteRow.clicked += DeleteSelectedRow;
            m_search.RegisterValueChangedCallback(OnSearchChanged);
            m_tableNavigation.makeItem = MakeLabel;
            m_tableNavigation.bindItem = BindTable;
            m_tableNavigation.selectionChanged += OnTableSelectionChanged;
            m_grid.selectionChanged += OnRowSelectionChanged;
            m_grid.columnSortingChanged += OnColumnSortingChanged;
            m_grid.headerContextMenuPopulateEvent += OnHeaderContextMenuPopulate;
            m_grid.RegisterCallback<ClickEvent>(OnGridClick, TrickleDown.TrickleDown);
            m_grid.columns.reorderable = false;
            m_grid.columns.resizable = true;
            m_grid.columns.resizePreview = true;
        }

        internal GameDBWorkspaceTabViewState Bind(
            GameDBWorkspaceTabViewState viewState, GameDBSnapshot snapshot)
        {
            if (m_disposed)
            {
                return viewState ?? new GameDBWorkspaceTabViewState();
            }

            var resolved = viewState ?? new GameDBWorkspaceTabViewState();
            m_binding = true;
            try
            {
                m_actionMessageHost.Clear();
                if (!string.IsNullOrWhiteSpace(m_pendingActionMessage))
                {
                    m_actionMessageHost.Add(new HelpBox(m_pendingActionMessage,
                        HelpBoxMessageType.Warning));
                    m_pendingActionMessage = null;
                }
                m_snapshot = snapshot;
                m_viewState = viewState;
                if (viewState == null || snapshot == null)
                {
                    m_search.SetValueWithoutNotify(string.Empty);
                    m_grid.sortColumnDescriptions.Clear();
                    m_projection = null;
                    m_tableNavigation.itemsSource = null;
                    m_grid.itemsSource = null;
                    m_tableNavigation.ClearSelection();
                    m_grid.ClearSelection();
                    ClearColumns();
                    m_columnSignature = null;
                    SetToolbarState(false, false);
                    ShowEmptyState("Open a database to view its tables.");
                    return resolved;
                }

                m_search.SetValueWithoutNotify(viewState.SearchText);
                m_projection = new GameDBTableViewProjection(snapshot,
                    viewState.SelectedTableId, viewState.SearchText, viewState.Sorts);
                m_tableNavigation.itemsSource = m_projection.Tables as System.Collections.IList;
                m_tableNavigation.RefreshItems();
                var tableIndex = m_projection.SelectedTable == null
                    ? -1
                    : m_projection.Tables.ToList().FindIndex(table =>
                        ReferenceEquals(table, m_projection.SelectedTable));
                m_tableNavigation.SetSelectionWithoutNotify(tableIndex < 0
                    ? Array.Empty<int>()
                    : new[] { tableIndex });

                ReconcileColumns(m_projection.SelectedTable);
                RestoreColumnLayout(viewState.Columns);
                RestoreSortDescriptions(m_projection.Sorts);
                m_grid.itemsSource = m_projection.Rows as System.Collections.IList;
                m_grid.RefreshItems();
                var rowIndex = m_projection.IndexOfRow(viewState.SelectedRowId);
                m_grid.SetSelectionWithoutNotify(rowIndex < 0
                    ? Array.Empty<int>()
                    : new[] { rowIndex });
                resolved = new GameDBWorkspaceTabViewState(
                    m_projection.SelectedTable?.Name,
                    m_projection.ContainsSourceRow(viewState.SelectedRowId)
                        ? viewState.SelectedRowId
                        : null,
                    viewState.SearchText, m_projection.Sorts, CaptureColumnLayout(),
                    viewState.HorizontalScroll, viewState.VerticalScroll);

                var hasTable = m_projection.SelectedTable != null;
                var hasSelectedRow = rowIndex >= 0;
                SetToolbarState(hasTable, hasSelectedRow);
                if (!hasTable)
                {
                    ShowEmptyState("This database has no tables.");
                }
                else if (m_projection.Rows.Count == 0)
                {
                    var canAddRow = string.IsNullOrWhiteSpace(viewState.SearchText);
                    ShowEmptyState(canAddRow
                        ? $"'{m_projection.SelectedTable.Name}' has no rows."
                        : "No rows match the current search.", canAddRow);
                }
                else
                {
                    m_emptyState.style.display = DisplayStyle.None;
                    m_grid.style.display = DisplayStyle.Flex;
                }
            }
            finally
            {
                m_binding = false;
            }
            m_viewState = resolved;
            return resolved;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            m_addRow.clicked -= RequestAddRowFromToolbar;
            m_emptyAction.clicked -= RequestAddRowFromEmptyState;
            m_deleteRow.clicked -= DeleteSelectedRow;
            m_search.UnregisterValueChangedCallback(OnSearchChanged);
            m_tableNavigation.selectionChanged -= OnTableSelectionChanged;
            m_grid.selectionChanged -= OnRowSelectionChanged;
            m_grid.columnSortingChanged -= OnColumnSortingChanged;
            m_grid.headerContextMenuPopulateEvent -= OnHeaderContextMenuPopulate;
            m_grid.UnregisterCallback<ClickEvent>(OnGridClick, TrickleDown.TrickleDown);
            m_tableNavigation.makeItem = null;
            m_tableNavigation.bindItem = null;
            m_tableNavigation.itemsSource = null;
            m_grid.itemsSource = null;
            ClearColumns();
            m_projection = null;
            m_snapshot = null;
            m_viewState = null;
        }

        private void ReconcileColumns(GameDBTableSnapshot table)
        {
            var signature = table == null
                ? string.Empty
                : table.Name + "\n" + string.Join("\n", table.Fields.Select(field =>
                    $"{field.Name}|{field.FieldType}|{field.IsArray}|{field.TypeArgument}|"
                    + $"{field.DictionaryType?.KeyType}|{field.DictionaryType?.KeyTypeArgument}|"
                    + $"{field.DictionaryType?.ValueType}|{field.DictionaryType?.ValueTypeArgument}"));
            if (signature == m_columnSignature)
            {
                return;
            }

            ClearColumns();
            m_columnSignature = signature;
            if (table == null)
            {
                return;
            }

            AddColumn(CreateKeyColumn());
            foreach (var field in table.Fields)
            {
                AddColumn(CreateValueColumn(field));
            }
        }

        private Column CreateKeyColumn()
        {
            return new Column
            {
                name = GameDBTableViewProjection.KeyFieldId,
                title = "Key",
                width = 160f,
                minWidth = MinimumColumnWidth,
                maxWidth = MaximumColumnWidth,
                sortable = true,
                makeCell = () => new GameDBRowKeyEditorCell(m_renameRow == null
                    ? null
                    : intent => PresentRowMutation(m_renameRow(intent))),
                bindCell = (element, index) =>
                {
                    var cell = (GameDBRowKeyEditorCell)element;
                    if (m_projection == null || index < 0
                        || index >= m_projection.Rows.Count)
                    {
                        cell.Unbind();
                        return;
                    }
                    cell.Bind(m_snapshot, m_projection.SelectedTable,
                        m_projection.Rows[index], m_snapshot?.Revision);
                },
                unbindCell = (element, _) =>
                    ((GameDBRowKeyEditorCell)element).Unbind()
            };
        }


        private Column CreateValueColumn(GameDBFieldSnapshot field)
        {
            return new Column
            {
                name = field.Name,
                title = field.Name,
                width = 140f,
                minWidth = MinimumColumnWidth,
                maxWidth = MaximumColumnWidth,
                sortable = true,
                makeCell = () => GameDBValueEditorFactory.Create(field, m_editValue,
                    m_editCollection),
                bindCell = (element, index) =>
                {
                    if (m_projection == null || index < 0
                        || index >= m_projection.Rows.Count)
                    {
                        GameDBValueEditorFactory.Unbind(element);
                        return;
                    }

                    GameDBValueEditorFactory.Bind(element, field, m_snapshot,
                        m_projection.SelectedTable, m_projection.Rows[index],
                        m_snapshot?.Revision);
                },
                unbindCell = (element, _) => GameDBValueEditorFactory.Unbind(element)
            };
        }

        private void AddColumn(Column column)
        {
            column.propertyChanged += OnColumnPropertyChanged;
            m_grid.columns.Add(column);
            m_displayColumns.Add(column);
        }

        private void ClearColumns()
        {
            foreach (var column in m_grid.columns)
            {
                column.propertyChanged -= OnColumnPropertyChanged;
            }
            m_grid.columns.Clear();
            m_displayColumns.Clear();
        }

        private static VisualElement MakeLabel()
        {
            var label = new Label();
            label.AddToClassList("gamedb-editor__table-cell");
            return label;
        }

        private void BindTable(VisualElement element, int index)
        {
            var label = (Label)element;
            var table = m_projection?.Tables[index];
            label.text = table?.Name ?? string.Empty;
            label.tooltip = label.text;
            label.userData = table?.Name;
        }

        private void OnTableSelectionChanged(IEnumerable<object> selection)
        {
            if (m_binding || m_disposed)
            {
                return;
            }
            var table = selection.OfType<GameDBTableSnapshot>().FirstOrDefault();
            if (table != null)
            {
                var current = m_viewState ?? new GameDBWorkspaceTabViewState();
                ApplyViewState(new GameDBWorkspaceTabViewState(table.Name, null,
                    current.SearchText, current.Sorts, current.Columns,
                    current.HorizontalScroll, current.VerticalScroll));
            }
        }

        private void OnRowSelectionChanged(IEnumerable<object> selection)
        {
            if (m_binding || m_disposed || m_projection?.SelectedTable == null)
            {
                return;
            }
            var row = selection.OfType<GameDBRowSnapshot>().FirstOrDefault();
            m_deleteRow.SetEnabled(row != null);
            var current = m_viewState ?? new GameDBWorkspaceTabViewState();
            ApplyViewState(new GameDBWorkspaceTabViewState(
                m_projection.SelectedTable.Name, row?.Key,
                current.SearchText, current.Sorts, current.Columns,
                current.HorizontalScroll, current.VerticalScroll), false);
        }

        private void OnSearchChanged(ChangeEvent<string> change)
        {
            SetSearchText(change.newValue);
        }

        internal void RequestAddRowFromToolbar()
        {
            RequestAddRow(m_addRow);
        }

        internal void RequestAddRowFromEmptyState()
        {
            RequestAddRow(m_emptyAction);
        }

        private void RequestAddRow(VisualElement focusTarget)
        {
            if (m_disposed || m_addRowRequested == null || m_snapshot == null
                || m_projection?.SelectedTable == null)
            {
                return;
            }
            if (!m_addRowRequested(new GameDBAddRowRequest(m_snapshot,
                m_projection.SelectedTable, m_snapshot.Revision, focusTarget)))
            {
                PresentRowMutation(new GameDBRowMutationResult(false,
                    "The Add Row editor could not be opened. Refresh the table and try again.",
                    m_snapshot, null, GameDBRowReferenceImpact.None));
            }
        }

        private void DeleteSelectedRow()
        {
            if (m_disposed || m_deleteRowIntent == null
                || m_projection?.SelectedTable == null
                || m_viewState?.SelectedRowId == null)
            {
                return;
            }
            var intent = new GameDBRowDeleteIntent(
                m_projection.SelectedTable.Name, m_viewState.SelectedRowId,
                m_snapshot?.Revision);
            m_grid.schedule.Execute(() =>
            {
                if (!m_disposed)
                {
                    PresentRowMutation(m_deleteRowIntent(intent));
                }
            });
        }

        internal GameDBRowMutationResult CreateRow(string rowKey)
        {
            if (m_disposed || m_createRow == null || m_projection?.SelectedTable == null)
            {
                return null;
            }
            return PresentRowMutation(m_createRow(new GameDBRowCreateIntent(
                m_projection.SelectedTable.Name, rowKey, m_snapshot?.Revision)));
        }

        internal GameDBRowMutationResult RenameRow(string currentKey, string newKey)
        {
            if (m_disposed || m_renameRow == null || m_projection?.SelectedTable == null)
            {
                return null;
            }
            return PresentRowMutation(m_renameRow(new GameDBRowRenameIntent(
                m_projection.SelectedTable.Name, currentKey, newKey,
                m_snapshot?.Revision)));
        }

        internal GameDBRowMutationResult DeleteRow(string rowKey)
        {
            if (m_disposed || m_deleteRowIntent == null
                || m_projection?.SelectedTable == null)
            {
                return null;
            }
            return PresentRowMutation(m_deleteRowIntent(new GameDBRowDeleteIntent(
                m_projection.SelectedTable.Name, rowKey, m_snapshot?.Revision)));
        }

        private GameDBRowMutationResult PresentRowMutation(
            GameDBRowMutationResult result)
        {
            m_actionMessageHost.Clear();
            m_pendingActionMessage = null;
            if (result?.Success == false)
            {
                m_actionMessageHost.Add(new HelpBox(
                    result.Message ?? "The row action failed.",
                    HelpBoxMessageType.Error));
            }
            else if (!string.IsNullOrWhiteSpace(result?.Message))
            {
                m_actionMessageHost.Add(new HelpBox(
                    result.Message, HelpBoxMessageType.Warning));
                m_pendingActionMessage = result.Message;
            }
            return result;
        }

        internal void SetSearchText(string searchText)
        {
            if (m_binding || m_disposed)
            {
                return;
            }
            var current = m_viewState ?? new GameDBWorkspaceTabViewState();
            ApplyViewState(new GameDBWorkspaceTabViewState(
                current.SelectedTableId, current.SelectedRowId, searchText,
                current.Sorts, current.Columns,
                current.HorizontalScroll, current.VerticalScroll));
        }

        private void OnColumnPropertyChanged(object sender,
            BindablePropertyChangedEventArgs args)
        {
            if (!m_binding && !m_disposed && sender is Column
                && args.propertyName.Equals(new BindingId(nameof(Column.width))))
            {
                PublishColumnLayout();
            }
        }

        private void OnGridClick(ClickEvent evt)
        {
            if (m_disposed || evt.button != 0 || evt.clickCount != 2
                || !(evt.target is VisualElement target))
            {
                return;
            }

            var resizeHandle = FindAncestorWithClass(target, ResizeDragAreaClass);
            if (resizeHandle == null)
            {
                return;
            }

            var handles = m_grid.Query<VisualElement>(className: ResizeDragAreaClass)
                .ToList();
            var handleIndex = handles.IndexOf(resizeHandle);
            var visibleColumns = m_displayColumns.Where(column => column.visible).ToArray();
            var titles = m_grid.Query<TextElement>(className: HeaderTitleClass).ToList();
            if (handleIndex < 0 || handleIndex >= visibleColumns.Length
                || handleIndex >= titles.Count)
            {
                return;
            }

            BestFitColumn(visibleColumns[handleIndex], titles[handleIndex]);
            evt.StopPropagation();
        }

        private static VisualElement FindAncestorWithClass(VisualElement element,
            string className)
        {
            while (element != null)
            {
                if (element.ClassListContains(className))
                {
                    return element;
                }
                element = element.parent;
            }
            return null;
        }

        private bool BestFitColumn(Column column, TextElement measureHost)
        {
            if (measureHost?.panel == null)
            {
                return false;
            }
            return ApplyBestFitColumn(column,
                text => measureHost.MeasureTextSize(text ?? string.Empty,
                    float.NaN, VisualElement.MeasureMode.Undefined,
                    float.NaN, VisualElement.MeasureMode.Undefined).x);
        }

        internal bool BestFitColumn(string fieldId, Func<string, float> measureText)
        {
            return ApplyBestFitColumn(m_displayColumns.FirstOrDefault(column =>
                column.name == fieldId), measureText);
        }

        private bool ApplyBestFitColumn(Column column, Func<string, float> measureText)
        {
            if (m_disposed || column == null || m_projection?.SelectedTable == null
                || !TryCalculateBestFitWidth(column, m_projection.SelectedTable,
                    measureText, out var width)
                || Math.Abs(column.width.value - width) < 0.01f)
            {
                return false;
            }
            column.width = width;
            return true;
        }

        internal static bool TryCalculateBestFitWidth(Column column,
            GameDBTableSnapshot table, Func<string, float> measureText, out float width)
        {
            width = 0f;
            if (column == null || table == null || measureText == null)
            {
                return false;
            }

            var field = column.name == GameDBTableViewProjection.KeyFieldId
                ? null
                : table.Fields.FirstOrDefault(candidate => candidate.Name == column.name);
            if (field == null && column.name != GameDBTableViewProjection.KeyFieldId)
            {
                return false;
            }

            var measured = MeasureCandidate(column.title ?? column.name, HeaderChromeWidth,
                measureText);
            if (!IsFinite(measured))
            {
                return false;
            }

            var chrome = BestFitCellChrome(field);
            foreach (var row in table.Rows)
            {
                var candidate = MeasureCandidate(BestFitDisplayText(field, row), chrome,
                    measureText);
                if (!IsFinite(candidate))
                {
                    return false;
                }
                measured = Math.Max(measured, candidate);
            }

            width = (float)Math.Ceiling(Math.Max(BestFitMinimumWidth(field),
                Math.Min(MaximumColumnWidth, measured)));
            return true;
        }

        private static float MeasureCandidate(string text, float chrome,
            Func<string, float> measureText)
        {
            return measureText(text ?? string.Empty) + chrome;
        }

        private static float BestFitCellChrome(GameDBFieldSnapshot field)
        {
            if (field == null)
            {
                return CellChromeWidth;
            }
            if (field.IsArray || field.FieldType == FieldType.dictionary)
            {
                return CollectionChromeWidth;
            }
            return field.FieldType == FieldType.@enum
                || field.FieldType == FieldType.tableRef
                || field.FieldType == FieldType.unityObject
                ? FieldChromeWidth
                : CellChromeWidth;
        }

        private static float BestFitMinimumWidth(GameDBFieldSnapshot field)
        {
            if (field == null)
            {
                return MinimumColumnWidth;
            }
            switch (field.FieldType)
            {
                case FieldType.vector2:
                    return 120f;
                case FieldType.vector3:
                    return 160f;
                case FieldType.vector4:
                    return 200f;
                case FieldType.color:
                    return 120f;
                default:
                    return MinimumColumnWidth;
            }
        }

        private static string BestFitDisplayText(GameDBFieldSnapshot field,
            GameDBRowSnapshot row)
        {
            if (field == null)
            {
                return row?.Key ?? string.Empty;
            }
            if (row == null || !row.Values.TryGetValue(field.Name, out var value))
            {
                value = null;
            }
            if (field.FieldType == FieldType.tableRef && !field.IsArray)
            {
                return value as string ?? FieldBase.NullRefToken;
            }
            if (field.FieldType == FieldType.unityObject && !field.IsArray)
            {
                return UnityObjectDisplayText(value as UnityObjectReference);
            }
            return GameDBTableViewProjection.FormatValue(value);
        }

        private static string UnityObjectDisplayText(UnityObjectReference reference)
        {
            if (reference == null || reference.IsEmpty
                || string.IsNullOrWhiteSpace(reference.Path))
            {
                return string.Empty;
            }
            return Path.GetFileNameWithoutExtension(reference.Path) ?? string.Empty;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnHeaderContextMenuPopulate(ContextualMenuPopulateEvent evt,
            Column column)
        {
            var index = m_displayColumns.IndexOf(column);
            evt.menu.AppendAction("Move Left", _ => MoveColumn(column.name, -1),
                index > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Move Right", _ => MoveColumn(column.name, 1),
                index >= 0 && index < m_displayColumns.Count - 1
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        internal bool MoveColumn(string fieldId, int offset)
        {
            if (m_binding || m_disposed || (offset != -1 && offset != 1))
            {
                return false;
            }
            var from = m_displayColumns.FindIndex(column => column.name == fieldId);
            var to = from + offset;
            if (from < 0 || to < 0 || to >= m_displayColumns.Count)
            {
                return false;
            }

            m_grid.columns.ReorderDisplay(from, to);
            var moved = m_displayColumns[from];
            m_displayColumns.RemoveAt(from);
            m_displayColumns.Insert(to, moved);
            PublishColumnLayout();
            return true;
        }

        private void OnColumnSortingChanged()
        {
            if (m_binding || m_disposed)
            {
                return;
            }
            var current = m_viewState ?? new GameDBWorkspaceTabViewState();
            var sorts = m_grid.sortedColumns.Select(sort =>
                new GameDBWorkspaceSortState(sort.column?.name ?? sort.columnName,
                    sort.direction == SortDirection.Descending)).ToArray();
            ApplyViewState(new GameDBWorkspaceTabViewState(
                current.SelectedTableId, current.SelectedRowId,
                current.SearchText, sorts, current.Columns,
                current.HorizontalScroll, current.VerticalScroll));
        }

        private void ApplyViewState(GameDBWorkspaceTabViewState viewState,
            bool rebind = true)
        {
            var resolved = rebind ? Bind(viewState, m_snapshot) : viewState;
            if (!rebind)
            {
                m_viewState = resolved;
            }
            m_viewStateChanged(resolved);
        }

        private void RestoreColumnLayout(
            IReadOnlyList<GameDBWorkspaceColumnState> columns)
        {
            var tableId = m_projection?.SelectedTable?.Name;
            var sourceOrder = m_displayColumns.Select((column, index) =>
                    new { column.name, index })
                .ToDictionary(item => item.name, item => item.index,
                    StringComparer.Ordinal);
            var states = (columns ?? Array.Empty<GameDBWorkspaceColumnState>())
                .Where(state => state != null
                    && (state.TableId == tableId || state.TableId == null)
                    && sourceOrder.ContainsKey(state.FieldId))
                .GroupBy(state => state.FieldId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(state =>
                        state.TableId == tableId)
                    .First())
                .ToDictionary(state => state.FieldId, StringComparer.Ordinal);
            foreach (var column in m_displayColumns)
            {
                if (states.TryGetValue(column.name, out var state)
                    && state.Width > 0f)
                {
                    column.width = Math.Max(MinimumColumnWidth,
                        Math.Min(MaximumColumnWidth, state.Width));
                }
            }

            var desired = states.Values
                .OrderBy(state => state.Order)
                .ThenBy(state => sourceOrder[state.FieldId])
                .Select(state => m_grid.columns[state.FieldId])
                .Concat(m_displayColumns.Where(column => !states.ContainsKey(column.name)))
                .Distinct()
                .ToArray();
            for (var target = 0; target < desired.Length; target++)
            {
                var current = m_displayColumns.IndexOf(desired[target]);
                if (current < 0 || current == target)
                {
                    continue;
                }
                m_grid.columns.ReorderDisplay(current, target);
                var moved = m_displayColumns[current];
                m_displayColumns.RemoveAt(current);
                m_displayColumns.Insert(target, moved);
            }
        }

        private GameDBWorkspaceColumnState[] CaptureColumnLayout()
        {
            var tableId = m_projection?.SelectedTable?.Name;
            var previous = (m_viewState?.Columns
                    ?? Array.Empty<GameDBWorkspaceColumnState>())
                .Where(column => column.TableId != tableId
                    && column.TableId != null);
            var current = m_displayColumns.Select((column, order) =>
                new GameDBWorkspaceColumnState(column.name,
                    Math.Max(MinimumColumnWidth,
                        Math.Min(MaximumColumnWidth, column.width.value)),
                    order, tableId));
            return previous.Concat(current).ToArray();
        }

        private void PublishColumnLayout()
        {
            var current = m_viewState ?? new GameDBWorkspaceTabViewState();
            ApplyViewState(new GameDBWorkspaceTabViewState(
                current.SelectedTableId, current.SelectedRowId,
                current.SearchText, current.Sorts, CaptureColumnLayout(),
                current.HorizontalScroll, current.VerticalScroll), false);
        }

        private void RestoreSortDescriptions(
            IReadOnlyList<GameDBWorkspaceSortState> sorts)
        {
            var current = m_grid.sortColumnDescriptions.ToArray();
            if (current.Length == sorts.Count && current.Select((sort, index) =>
                    (sort.columnName == sorts[index].FieldId)
                    && (sort.direction == SortDirection.Descending
                        == sorts[index].Descending)).All(matches => matches))
            {
                return;
            }

            m_grid.sortColumnDescriptions.Clear();
            foreach (var sort in sorts)
            {
                m_grid.sortColumnDescriptions.Add(new SortColumnDescription(
                    sort.FieldId, sort.Descending
                        ? SortDirection.Descending
                        : SortDirection.Ascending));
            }
        }

        private void SetToolbarState(bool hasTable, bool hasSelectedRow)
        {
            m_addRow.SetEnabled(hasTable);
            m_deleteRow.SetEnabled(hasSelectedRow);
            m_columns.SetEnabled(hasTable);
        }

        private void ShowEmptyState(string message, bool canAddRow = false)
        {
            m_emptyMessage.text = message;
            m_emptyAction.SetEnabled(canAddRow);
            m_emptyAction.style.display = canAddRow
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_emptyState.style.display = DisplayStyle.Flex;
            m_grid.style.display = DisplayStyle.None;
        }
    }
}
