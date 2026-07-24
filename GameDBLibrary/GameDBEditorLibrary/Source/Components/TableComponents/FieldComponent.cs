using GameDBLibrary;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary {
    internal class FieldComponent : HeaderComponent
    {
        private string m_tableName = string.Empty;
        private string m_fieldName = string.Empty;

        private bool m_editable = false;
        private bool m_inited = false;

        private GameDBDataSource m_source = null;

        public FieldComponent(string name, GameDBDataSource source, string tableName, string fieldName, bool editable) : base(name)
        {
            m_source = source;
            m_tableName = tableName;
            m_fieldName = fieldName;
            m_editable = editable;
        }

        public override void Init()
        {
            if (!m_inited)
            {
                EventSystem.Instance.RegisterEvent(Events.EDIT_TABLE, OnEditTable);
            }

            m_inited = true;
        }

        ~FieldComponent()
        {
            EventSystem.Instance.DeregisterEvent(Events.EDIT_TABLE, OnEditTable);
        }

        protected override void RenderHeader(int width)
        {
            var inGame = Application.isPlaying;

            var field = GetField();

            var type = field.Type.ToString();

            if (field.Type == FieldType.@enum)
            {
                type = field.GetSystemType().ToString().Replace("+", ".");
            }

            var label = $"{field.Name} ({type}{(field.IsArray ? "[]" : "")})";
            var toolTip = label;
            var shortenedLabel = label;

            var labelDimensions = inGame || !m_editable ? width : width - 20;

            var guiSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);

            var textDimensions = guiSkin.label.CalcSize(new GUIContent(shortenedLabel));

            var numChars = label.Length;

            while (textDimensions.x >= (labelDimensions - 15))
            {
                numChars--;
                label = label.Substring(0, numChars);
                shortenedLabel = label + "...";
                textDimensions = guiSkin.label.CalcSize(new GUIContent(shortenedLabel));
            }

            
            EditorGUILayout.LabelField(new GUIContent(shortenedLabel, toolTip), GUILayout.Width(labelDimensions));
            m_renderArea = GUILayoutUtility.GetLastRect();

            if (!inGame && m_editable && GUILayout.Button(new GUIContent("x", "Delete field"), EditorStyles.miniButton, GUILayout.Width(16))) {
                if (EditorUtility.DisplayDialog("Remove field?", "Are you sure?", "Yes", "No")) {
                    DeleteField();
                }
            }
        }

        private void OnEditTable(object[] args)
        {
            if ((string)args[0] == m_tableName)
            {
                m_editable = (bool)args[1];
            }
        }

        public void DeleteField()
        {
            GetTable().RemoveField(GetField());
            EventSystem.Instance.TriggerEvent(Events.RELOAD_TABLE, m_tableName);
        }

        private Field GetField()
        {
            return (Field) m_source.GameDB.Tables[m_tableName].Fields[m_fieldName];
        }

        private TableModel GetTable()
        {
            return (TableModel) m_source.GameDB.Tables[m_tableName];
        }
    }
}
