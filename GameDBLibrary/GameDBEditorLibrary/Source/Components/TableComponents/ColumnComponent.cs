using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary {
    internal class ColumnComponent : Component {
        private HeaderComponent m_columnHeader;

        private Rect m_origRect;
        private Rect m_resizeBarRect;
        private bool m_resizing = false;

        private int m_currentFieldWidth = 120;

        private int m_currentMousePos = 0;

        public ColumnComponent(string name, string tableName) : base(name) {
            m_columnHeader = new KeyFieldComponent($"Key.{tableName}");
            AddChild(m_columnHeader);
        }

        public ColumnComponent(string name, GameDBDataSource source, string tableName, string fieldName, bool editable) : base(name) {
            m_columnHeader = new FieldComponent(GetFieldComponentName(fieldName), source, tableName, fieldName, editable);
            AddChild(m_columnHeader);
        }

        public override void Render(params object[] args) {
            var rows = (int)args[0];

            m_columnHeader.Render(m_currentFieldWidth);

            if (Event.current.type == EventType.Repaint) {
                m_origRect = m_resizeBarRect = m_columnHeader.GetRenderArea();
            }

            GUILayout.Space(10);
            m_resizeBarRect.x = m_resizing ? m_currentMousePos + 1 : m_origRect.x + m_currentFieldWidth + 4;
            m_resizeBarRect.width = 6;
            m_resizeBarRect.height = rows * 18 + 24;

            EditorGUIUtility.AddCursorRect(m_resizeBarRect, MouseCursor.ResizeHorizontal);

            HandleResize();
        }

        public int GetCurrentWidth() {
            return m_currentFieldWidth;
        }

        private string GetFieldComponentName(string fieldName) 
        {
            return $"Field.{fieldName}";
        }

        private void HandleResize()
        {
            if (Event.current.type == EventType.MouseDown && m_resizeBarRect.Contains(Event.current.mousePosition)) 
            {
                m_resizing = true;
            }

            if (m_resizing) 
            {
                m_currentMousePos = (int)Event.current.mousePosition.x;
                m_currentFieldWidth = (int)Math.Max((m_currentMousePos - m_origRect.x) - 4, 70);
            }

            if (Event.current.rawType == EventType.MouseUp)
            {
                m_resizing = false;
            }
        }
    }
}