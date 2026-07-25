using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class RowComponent : Component
    {
        private string m_tableName = string.Empty;
        private string m_rowName = string.Empty;
        private SortedDictionary<string, Component> m_values = new SortedDictionary<string, Component>();
        private bool m_editable = false;
        private GameDBDataSource m_source = null;

        public RowComponent(string name, GameDBDataSource source, string tableName, string rowName, bool editable) : base(name)
        {
            m_source = source;
            m_tableName = tableName;
            m_rowName = rowName;
            m_editable = editable;
        }

        public override void Init()
        {
            var rowData = GetRow().Data;

            foreach (var fieldPair in rowData) {
                var name = GetValueName(fieldPair.Key);

                if (!m_values.ContainsKey(name))
                {
                    m_values.Add(name, ValueFactory.Create(name, m_source, m_tableName, GetTable().Fields[fieldPair.Key].Name, m_rowName, m_editable));
                }
            }

            m_values = new SortedDictionary<string, Component>(m_values.Where(pair => rowData.ContainsKey(pair.Key.Split('.')[1])).ToDictionary(i => i.Key, i => i.Value));
        }

        public override void Render(params object[] args) {
            UIHelpers.RenderHorizontalGroup(() => {
                var header = args[0] as TableHeaderComponent;

                EditorGUILayout.LabelField(new GUIContent(m_rowName, m_rowName), GUILayout.Width(header.GetColumnWidth("Key")));

                GUILayout.Space(10);

                foreach (var pair in m_values) {
                    pair.Value.Render(header.GetColumnWidth(pair.Value.Name.Split('.')[1]));
                    GUILayout.Space(10);
                }

                RenderDeleteButton();
            });
        }

        private void RenderDeleteButton() {
            if (m_editable && GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(16))) {
                if (EditorUtility.DisplayDialog("Remove key?", "Are you sure?", "Yes", "No")) {
                    DeleteRow();
                }
            }
        }

        private string GetValueName(string valueName) {
            return $"Value.{valueName}";
        }

        private void DeleteRow()
        {
            if (GetTable().RemoveKey(m_rowName))
            {
                EventSystem.Instance.TriggerEvent(Events.RELOAD_TABLE, m_tableName);
            }
        }

        private TableModel GetTable()
        {
            return (TableModel)m_source.GameDB.Tables[m_tableName];
        }

        private RowModel GetRow()
        {
            return (RowModel)m_source.GameDB.Tables[m_tableName].Data[m_rowName];
        }
    }
}
