using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class TablesComponent : Component
    {
        private SortedDictionary<string, TableEditorComponent> m_tables = new SortedDictionary<string, TableEditorComponent>();

        private bool m_editable = true;
        private GameDBDataSource m_source = null;
        private string m_deleteTable = null;

        public TablesComponent(string name, GameDBDataSource source, bool editable = true) : base(name)
        {
            m_editable = editable;
            m_source = source;
        }

        public override void Init()
        {
            EventSystem.Instance.RegisterEvent(Events.DELETE_TABLE, OnDeleteTable);

            base.Init();
        }

        ~TablesComponent()
        {
            EventSystem.Instance.DeregisterEvent(Events.DELETE_TABLE, OnDeleteTable);
        }

        public override void Render(params object[] args)
        {
            GUILayout.Label("Tables", EditorStyles.boldLabel);

            foreach (var tablePair in m_tables)
            {
                tablePair.Value.Render();
            }

            if (m_deleteTable != null)
            {
                m_source.GameDB.Tables.Remove(m_deleteTable);
                m_deleteTable = null;
                EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
            }
        }

        public override void Update()
        {
            foreach (var tablePair in m_tables)
            {
                tablePair.Value.Update();
            }

            base.Update();
        }

        public void ClearTables()
        {
            m_tables.Clear();
        }

        public void UpdateTables()
        {
            // Add any missing tables
            foreach (var tablePair in m_source.GameDB.Tables)
            {
                var name = $"Table.{tablePair.Key}";

                if (!m_tables.ContainsKey(name))
                {
                    var table = (TableModel) tablePair.Value;
                    var component = new TableEditorComponent(name, m_source, table.Name, m_editable);
                    component.Init();
                    m_tables.Add(component.Name, component);
                }
            }

            //Clear old tables
            m_tables = new SortedDictionary<string, TableEditorComponent>(m_tables.Where(pair => m_source.GameDB.Tables.ContainsKey(pair.Key.Split('.')[1])).ToDictionary(i => i.Key, i => i.Value));
        }

        private void OnDeleteTable(object[] args)
        {
            var tableName = args[0] as string;

            m_deleteTable = tableName;
        }
    }
}
