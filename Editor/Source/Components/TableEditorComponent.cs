using GameDBLibrary;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class TableEditorComponent : Component
    {
        private readonly string m_tableName;

        private bool m_expaned;

        private string m_stringKey = string.Empty;
        private int m_enumKey;
        private int m_tableKey;

        private string m_addFieldName = string.Empty;
        private int m_selectedType;
        private int m_selectedEnum;
        private int m_selectedTable;

        private int m_selectedDictionaryKeyType;
        private int m_selectedDictionaryKeyEnum;
        private int m_selectedDictionaryKeyTable;
        private int m_selectedDictionaryFieldType;
        private int m_selectedDictionaryFieldEnum;
        private int m_selectedDictionaryFieldTable;

        private bool m_array;
        private string m_newTableName = string.Empty;
        private bool m_editTable;

        private readonly bool m_editable = true;

        private readonly GameDBDataSource m_source;

        public TableEditorComponent(string componentName, GameDBDataSource source, string tableName, bool editable) : base(componentName)
        {
            m_source = source;

            m_tableName = tableName;
            m_editable = editable;

            AddChild(new TableComponent("Table", m_source, m_tableName, editable));
        }

        public override void Render(params object[] args)
        {
            m_expaned = EditorGUILayout.Foldout(m_expaned, m_tableName);

            if (m_expaned)
            {
                UIHelpers.RenderBox(delegate {
                    RenderKeyCreator();

                    EditorGUILayout.Separator();

                    RenderChild("Table");

                    if (!Application.isPlaying && m_editTable && m_editable)
                    {
                        UIHelpers.RenderBox(delegate {
                            EditorGUILayout.LabelField("Modify Schema:", GUILayout.Width(120));
                            EditorGUILayout.Separator();

                            UIHelpers.RenderHorizontalGroup(delegate {
                                m_addFieldName = UIHelpers.RenderTextField("Field Name:", m_addFieldName, new UIHelpers.FieldLayout(80, 240));

                                object typeArg = null;

                                if (GameDB.Instance.LocalizationDB) {
                                    m_selectedType = (int)FieldType.@string;
                                }
                                else {
                                    m_selectedType = UIHelpers.RenderDropDown("Type:", m_selectedType, TypeUtils.GetTypeNames(), new UIHelpers.FieldLayout(40, 200));

                                    var renderArrayToggle = true;

                                    switch ((FieldType) m_selectedType) {
                                        case FieldType.@enum:
                                            typeArg = RenderEnum(ref m_selectedEnum);
                                            break;
                                        case FieldType.tableRef:
                                            typeArg = RenderTableRef(ref m_selectedTable);
                                            break;
                                        case FieldType.dictionary:
                                            renderArrayToggle = false;

                                            typeArg = RenderDictionary();
                                            break;
                                    }

                                    if (renderArrayToggle)
                                    {
                                        m_array = UIHelpers.RenderToggle("Array:", m_array, new UIHelpers.FieldLayout(50, 70));
                                    }
                                }

                                if (GUILayout.Button("Create Field", GUILayout.Width(100)))
                                {
                                    if (m_addFieldName.Length > 0 && !Char.IsNumber(m_addFieldName[0]))
                                    {
                                        if (AddField(m_addFieldName, (FieldType) m_selectedType, m_array, typeArg))
                                        {
                                            m_addFieldName = null;
                                            m_array = false;
                                            GUI.FocusControl("");
                                        }
                                        else
                                        {
                                            EditorUtility.DisplayDialog("Field already exists!", $"A field already exists with name: {m_addFieldName}", "OK");
                                        }
                                    }
                                    else
                                    {
                                        EditorUtility.DisplayDialog("Invalid field name!", "Field name can't be empty or start with a number", "OK");
                                    }
                                }
                            });
                        }, GUI.backgroundColor, 100);

                        UIHelpers.RenderHorizontalGroup(delegate {
                            if (GUILayout.Button("Remove Table", GUILayout.Width(100)))
                            {
                                string errorString = null;

                                foreach (var table in m_source.GameDB.Tables)
                                {
                                    foreach (var field in table.Value.Fields)
                                    {
                                        if (field.Value.Type == FieldType.tableRef)
                                        {
                                            if (field.Value.GetTypeArg<string>() == m_tableName)
                                            {
                                                errorString = $"Can't delete table as Table Reference Field {field.Key} in table {table.Key} still exists! Remove this first!";
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(errorString))
                                {
                                    EditorUtility.DisplayDialog("Error Removing Table", errorString, "OK");
                                }
                                else if (EditorUtility.DisplayDialog("Remove table?", "Are you sure?", "Yes", "No"))
                                {
                                    EventSystem.Instance.TriggerEvent(Events.DELETE_TABLE, m_tableName);
                                }
                            }

                            m_newTableName = UIHelpers.RenderTextField("New Table Name:", m_newTableName, new UIHelpers.FieldLayout(110, 280));

                            if (GUILayout.Button("Rename Table", GUILayout.Width(100)))
                            {
                                if (EditorUtility.DisplayDialog("Rename table?", "Are you sure?", "Yes", "No"))
                                {
                                    if (RenameTable(m_newTableName))
                                    {
                                        m_newTableName = null;
                                        GUI.FocusControl("");
                                        EventSystem.Instance.TriggerEvent(Events.RELOAD_TABLE, m_tableName);
                                    }
                                    else
                                    {
                                        EditorUtility.DisplayDialog("Table already exists!", $"A table already exists with name: {m_newTableName}", "OK");
                                    }
                                }
                            }
                        });
                    }
                    else  if (!Application.isPlaying && m_editable && GUILayout.Button("Edit Table", GUILayout.Width(100)))
                    {
                        m_editTable = true;
                        EventSystem.Instance.TriggerEvent(Events.EDIT_TABLE, m_tableName, m_editTable);
                    }
                }, new UnityEngine.Color(0.8f, 0.8f, 0.8f));
            }
            else if (m_editTable)
            {
                m_editTable = false;
                EventSystem.Instance.TriggerEvent(Events.EDIT_TABLE, m_tableName, m_editTable);
            }
        }

        private object RenderEnum(ref int selectedEnum)
        {
            object typeArg = null;

            var enumTypes = Settings.Instance.ImportedEnums.ToArray();
            selectedEnum = UIHelpers.RenderDropDown("Enum:", selectedEnum, enumTypes.Select(s => s.Replace("+", ".")).ToArray(), new UIHelpers.FieldLayout(40, 200));

            if (enumTypes.Length > 0)
            {
                typeArg = AssemblyExplorer.Instance.GetType(enumTypes[selectedEnum]);
            }

            return typeArg;
        }

        private object RenderTableRef(ref int selectedTable)
        {
            var tableNames = m_source.GameDB.Tables.Keys.ToArray();
            selectedTable = UIHelpers.RenderDropDown("Table:", selectedTable, tableNames, new UIHelpers.FieldLayout(50, 200));
            return tableNames[selectedTable];
        }

        private object RenderDictionary()
        {
            object typeArg = null;

            UIHelpers.RenderHorizontalGroup(() =>
            {
                m_selectedDictionaryKeyType = UIHelpers.RenderDropDown("Key Type:", m_selectedDictionaryKeyType, TypeUtils.GetKeyTypeNames(), new UIHelpers.FieldLayout(65, 200));

                object keyTypeArg = null;
                object valueTypeArg = null;

                switch ((KeyType)m_selectedDictionaryKeyType)
                {
                    case KeyType.@enum:
                        keyTypeArg = RenderEnum(ref m_selectedDictionaryKeyEnum);
                        break;
                }

                m_selectedDictionaryFieldType = UIHelpers.RenderDropDown("Field Type:", m_selectedDictionaryFieldType, DictionaryType.GetSupportedTypes(), new UIHelpers.FieldLayout(70, 200));

                switch ((FieldType)m_selectedDictionaryFieldType)
                {
                    case FieldType.@enum:
                        valueTypeArg = RenderEnum(ref m_selectedDictionaryFieldEnum);
                        break;
                    case FieldType.tableRef:
                        valueTypeArg = RenderTableRef(ref m_selectedDictionaryFieldTable);
                        break;
                }

                typeArg = new DictionaryType((KeyType)m_selectedDictionaryKeyType, keyTypeArg,(FieldType)m_selectedDictionaryFieldType, valueTypeArg);
            });

            return typeArg;
        }

        private void RenderKeyCreator()
        {
            if (!m_editable)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal("Box");
            {
                var tableKey = GetTable().TableKeyType;

                switch (tableKey.KeyType)
                {
                    case KeyType.@string:
                        m_stringKey = UIHelpers.RenderTextField("Key:", m_stringKey, new UIHelpers.FieldLayout(40, 240));
                        break;
                    case KeyType.@enum:

                        var names = Enum.GetNames((Type) tableKey.TypeArg);
                        m_enumKey = UIHelpers.RenderDropDown("Key:", m_enumKey, names, new UIHelpers.FieldLayout(40, 240));

                        m_stringKey = null;

                        if (names.Length > 0)
                        {
                            m_stringKey = names[m_enumKey];
                        }
                        break;
                }

                if (GUILayout.Button("Create Key", GUILayout.Width(100)))
                {
                    if (!string.IsNullOrEmpty(m_stringKey))
                    {
                        if (AddKey(m_stringKey))
                        {
                            m_stringKey = null;
                            GUI.FocusControl("");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Key already exists!", $"A key already exists with name: {m_stringKey}", "OK");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Empty Key!", "Please enter a valid key", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool AddKey(string key)
        {
            bool success = false;

            if (GetTable().AddKey(key))
            {
                EventSystem.Instance.TriggerEvent(Events.RELOAD_TABLE, m_tableName);
                success = true;
            }

            return success;
        }

        private bool AddField(string fieldName, FieldType type, bool array, object typeArg = null)
        {
            var success = false;

            if (GetTable().AddField(fieldName, type, array, typeArg))
            {
                EventSystem.Instance.TriggerEvent(Events.RELOAD_TABLE, m_tableName);
                success = true;
            }

            return success;
        }

        private bool RenameTable(string tableName)
        {
            if (m_source.GameDB.Tables.ContainsKey(tableName))
            {
                return false;
            }

            var table = GetTable();

            m_source.GameDB.Tables.Remove(m_tableName);
            table.Rename(tableName);
            m_source.GameDB.Tables.Add(tableName, table);

            return true;
        }

        private TableModel GetTable()
        {
            return (TableModel)m_source.GameDB.Tables[m_tableName];
        }
    }
}
