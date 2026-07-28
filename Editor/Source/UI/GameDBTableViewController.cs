using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBTableViewController : IDisposable
    {
        private const float MinimumColumnWidth = 48f;
        private const float MaximumColumnWidth = 600f;
        private readonly ToolbarSearchField m_search;
        private readonly ListView m_tableNavigation;
        private readonly MultiColumnListView m_grid;
        private readonly Label m_placeholder;
        private readonly Action<GameDBWorkspaceTabViewState> m_viewStateChanged;
        private readonly Func<GameDBValueEditIntent, GameDBValueEditResult> m_editValue;
        private readonly Action<GameDBCollectionEditRequest> m_editCollection;
        private GameDBTableViewProjection m_projection;
        private GameDBSnapshot m_snapshot;
        private GameDBWorkspaceTabViewState m_viewState;
        private readonly List<Column> m_displayColumns = new List<Column>();
        private string m_columnSignature;
        private bool m_binding;
        private bool m_disposed;

        internal GameDBTableViewController(ToolbarSearchField search,
            ListView tableNavigation, MultiColumnListView grid, Label placeholder,
            Action<GameDBWorkspaceTabViewState> viewStateChanged,
            Func<GameDBValueEditIntent, GameDBValueEditResult> editValue = null,
            Action<GameDBCollectionEditRequest> editCollection = null)
        {
            m_search = search ?? throw new ArgumentNullException(nameof(search));
            m_tableNavigation = tableNavigation
                ?? throw new ArgumentNullException(nameof(tableNavigation));
            m_grid = grid ?? throw new ArgumentNullException(nameof(grid));
            m_placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
            m_viewStateChanged = viewStateChanged
                ?? throw new ArgumentNullException(nameof(viewStateChanged));
            m_editValue = editValue;
            m_editCollection = editCollection;

            m_search.RegisterValueChangedCallback(OnSearchChanged);
            m_tableNavigation.makeItem = MakeLabel;
            m_tableNavigation.bindItem = BindTable;
            m_tableNavigation.selectionChanged += OnTableSelectionChanged;
            m_grid.selectionChanged += OnRowSelectionChanged;
            m_grid.columnSortingChanged += OnColumnSortingChanged;
            m_grid.headerContextMenuPopulateEvent += OnHeaderContextMenuPopulate;
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
                    ShowPlaceholder("Open a database to view its tables.");
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

                if (m_projection.SelectedTable == null)
                {
                    ShowPlaceholder("This database has no tables.");
                }
                else if (m_projection.Rows.Count == 0)
                {
                    ShowPlaceholder(string.IsNullOrWhiteSpace(viewState.SearchText)
                        ? $"'{m_projection.SelectedTable.Name}' has no rows."
                        : "No rows match the current search.");
                }
                else
                {
                    m_placeholder.style.display = DisplayStyle.None;
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
            m_search.UnregisterValueChangedCallback(OnSearchChanged);
            m_tableNavigation.selectionChanged -= OnTableSelectionChanged;
            m_grid.selectionChanged -= OnRowSelectionChanged;
            m_grid.columnSortingChanged -= OnColumnSortingChanged;
            m_grid.headerContextMenuPopulateEvent -= OnHeaderContextMenuPopulate;
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

            AddColumn(CreateColumn(GameDBTableViewProjection.KeyFieldId, "Key",
                row => row.Key, 160f));
            foreach (var field in table.Fields)
            {
                AddColumn(CreateValueColumn(field));
            }
        }

        private Column CreateColumn(string name, string title,
            Func<GameDBRowSnapshot, string> getText, float width)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = MinimumColumnWidth,
                maxWidth = MaximumColumnWidth,
                sortable = true,
                makeCell = MakeLabel,
                bindCell = (element, index) =>
                {
                    var label = (Label)element;
                    if (m_projection == null || index < 0
                        || index >= m_projection.Rows.Count)
                    {
                        label.text = string.Empty;
                        label.tooltip = string.Empty;
                        label.userData = null;
                        return;
                    }
                    var row = m_projection.Rows[index];
                    label.text = getText(row);
                    label.tooltip = label.text;
                    label.userData = row.Key;
                },
                unbindCell = (element, _) =>
                {
                    var label = (Label)element;
                    label.text = string.Empty;
                    label.tooltip = string.Empty;
                    label.userData = null;
                }
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

        private void ShowPlaceholder(string message)
        {
            m_placeholder.text = message;
            m_placeholder.style.display = DisplayStyle.Flex;
            m_grid.style.display = DisplayStyle.None;
        }
    }
}
