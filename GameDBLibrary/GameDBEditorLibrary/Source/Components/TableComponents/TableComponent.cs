using System.Collections.Generic;
using UnityEditor;

namespace GameDBEditorLibrary
{
    internal class TableComponent : Component
    {
        private bool m_inited = false;

        private TableHeaderComponent m_headerComponent;
        private List<Component> m_rows = new List<Component>();

        private string m_updateTable = null;

        private string m_tableName = string.Empty;
        private bool m_editable = false;

        private GameDBDataSource m_source = null;

        public TableComponent(string componentName, GameDBDataSource source, string tableName, bool editable) : base(componentName)
        {
            m_source = source;
            m_tableName = tableName;
            m_editable = editable;
        }

        public override void Init()
        {
            if (!m_inited)
            {
                EventSystem.Instance.RegisterEvent(Events.RELOAD_TABLE, OnUpdateTable);
                EventSystem.Instance.RegisterEvent(Events.EDIT_TABLE, OnEditTable);

                m_headerComponent = new TableHeaderComponent($"TableHeader.{m_tableName}", m_source, m_tableName, m_editable);
                AddChild(m_headerComponent);
                m_headerComponent.Init();

                UpdateTable(m_tableName);
            }

            m_inited = true;
        }

        ~TableComponent()
        {
            EventSystem.Instance.DeregisterEvent(Events.RELOAD_TABLE, OnUpdateTable);
            EventSystem.Instance.DeregisterEvent(Events.EDIT_TABLE, OnEditTable);
        }

        public override void Render(params object[] args)
        {
            m_headerComponent.Render(m_rows.Count);

            EditorGUILayout.Separator();

            foreach (var row in m_rows) {
                row.Render(m_headerComponent);
            }

            EditorGUILayout.Separator();
        }

        public override void Update()
        {
            if (m_updateTable != null)
            {
                UpdateTable(m_updateTable);
                m_updateTable = null;
            }

            base.Update();
        }

        private void OnUpdateTable(object[] args)
        {
            m_updateTable = (args[0] as string);
        }

        private void UpdateTable(string tableName)
        {
            if (tableName == m_tableName)
            {
                m_headerComponent.UpdateColumns();
                UpdateRows();
            }
        }

        private void UpdateRows() {
            var table = GetTable();

            // Add any missing rows
            m_rows.Clear();

            foreach (var rowPair in table.Data)
            {
                string componentName = GetRowComponentName(rowPair.Key);

                Component component = null;

                if (m_children.ContainsKey(componentName))
                {
                    component = m_children[componentName];
                }
                else
                {
                    RowModel rowModel = (RowModel)rowPair.Value;
                    component = new RowComponent(componentName, m_source, table.Name, rowModel.Name, m_editable);
                    AddChild(component);
                }

                component.Init();

                m_rows.Add(m_children[componentName]);
            }

            // Remove any components not needed anymore
            // Not the most effecient but only done occasionally
            var childrenToDelete = new List<string>();

            foreach (var componentPair in m_children)
            {
                if (componentPair.Key.Contains("Row.") && !table.Data.ContainsKey(componentPair.Key.Replace("Row.", "")))
                {
                    childrenToDelete.Add(componentPair.Key);
                }
            }

            foreach (string componentName in childrenToDelete)
            {
                m_children.Remove(componentName);
            }

            // Sort rows for view based on name
            var comparer = new AlphanumComparatorFast();
            m_rows.Sort((a, b) => comparer.Compare(a.Name, b.Name));
        }

        private string GetRowComponentName(string rowName)
        {
            return $"Row.{rowName}";
        }

        private void OnEditTable(object[] args)
        {
            if ((string)args[0] == m_tableName)
            {
                m_headerComponent.OnEditTable((bool)args[1]);
            }
        }

        private TableModel GetTable()
        {
            return (TableModel)m_source.GameDB.Tables[m_tableName];
        }
    }
}
