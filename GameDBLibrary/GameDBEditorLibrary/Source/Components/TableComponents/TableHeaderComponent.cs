using System.Collections.Generic;

namespace GameDBEditorLibrary {
    internal class TableHeaderComponent : Component {

        private ColumnComponent m_keyColumn;
        private SortedDictionary<string, Component> m_columns = new SortedDictionary<string, Component>();

        private string m_tableName = string.Empty;
        private GameDBDataSource m_source = null;

        private bool m_editEnabled = false;
        private bool m_editable = false;

        public TableHeaderComponent(string name, GameDBDataSource source, string tableName, bool editable) : base(name) {
            m_source = source;
            m_tableName = tableName;
            m_editable = editable;
        }

        public override void Init() {
            m_keyColumn = new ColumnComponent(GetColumnComponentName($"Key.{m_tableName}"), m_tableName);
            AddChild(m_keyColumn);

            base.Init();
        }

        public void UpdateColumns() {
            var table = GetTable();
            var fields = table.Fields;

            m_columns.Clear();

            foreach (var fieldPair in fields) {
                string componentName = GetColumnComponentName(fieldPair.Key);

                if (!m_children.ContainsKey(componentName)) {
                    var component = new ColumnComponent(componentName, m_source, table.Name, fieldPair.Value.Name, m_editEnabled && m_editable);
                    AddChild(component);
                    component.Init();
                }

                m_columns.Add(componentName, m_children[componentName]);
            }

            // Remove any components not needed anymore
            // Not the most effecient but only done occasionally
            List<string> childrenToDelete = new List<string>();

            foreach (var componentPair in m_children) {
                if (componentPair.Key.Contains("Column.") && !fields.ContainsKey(componentPair.Key.Replace("Column.", ""))) {
                    childrenToDelete.Add(componentPair.Key);
                }
            }

            foreach (string componentName in childrenToDelete) {
                m_children.Remove(componentName);
            }
        }

        public override void Render(params object[] args){
            UIHelpers.RenderHorizontalGroup(() => {
                m_keyColumn.Render(args[0]);

                foreach (var column in m_columns) {
                    column.Value.Render(args[0]);
                }
            });
        }

        public int GetColumnWidth(string columnName) {
            return columnName == "Key" ? m_keyColumn.GetCurrentWidth() : ((ColumnComponent)m_columns[GetColumnComponentName(columnName)]).GetCurrentWidth();
        }

        public void OnEditTable(bool editEnabled) {
            m_editEnabled = editEnabled;
        }

        private TableModel GetTable() {
            return (TableModel)m_source.GameDB.Tables[m_tableName];
        }

        private string GetColumnComponentName(string columnName) {
            return $"Column.{columnName}";
        }
    }
}
