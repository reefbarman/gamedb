using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace GameDBEditorLibrary
{
    internal class DictionaryValueComponent : Component
    {
        private struct DictEntry
        {
            public object Key;
            public object Value;
        }

        protected GameDBDataSource m_source;
        protected string m_tableName;
        protected string m_fieldName;
        protected string m_rowName;
        private readonly bool m_editable;

        private Rect m_currentPopupRect = new Rect(100, 100, 440, 200);
        private bool m_popupOpened;

        private Vector2 m_scrollPos;
        private int m_dictSize;
        private readonly string m_dictFieldName;
        private List<DictEntry> m_valueList;

        private readonly ValueComponent m_keyComponent;
        private readonly ValueComponent m_valueComponent;

        private readonly int m_keySize = 150;
        private readonly int m_fieldSize = 150;

        public DictionaryValueComponent(string name, GameDBDataSource source, string tableName, string fieldName,
            string rowName, bool editable)
            : base(name)
        {
            m_dictFieldName = name;

            m_source = source;
            m_tableName = tableName;
            m_fieldName = fieldName;
            m_rowName = rowName;
            m_editable = editable;

            var field = GetField();

            var dictType = field.GetTypeArg<DictionaryType>();

            m_keyComponent = ValueFactory.GetValueComponent(TypeUtils.KeyTypeToFieldType(dictType.KeyType), dictType.GetKeySystemType());

            m_valueComponent = ValueFactory.GetValueComponent(dictType.ValueType, dictType.GetValueSystemType());

            switch (dictType.ValueType)
            {
                case FieldType.tableRef:
                    (m_valueComponent as TableRefValueComponent).Table = source.GameDB.Tables[dictType.ValueTypeArg as string];
                    break;
                case FieldType.vector2:
                    m_fieldSize += 50;
                    m_currentPopupRect.width += 50;
                    break;
                case FieldType.vector3:
                    m_fieldSize += 100;
                    m_currentPopupRect.width += 100;
                    break;
                case FieldType.vector4:
                    m_fieldSize += 150;
                    m_currentPopupRect.width += 150;
                    break;
            }
        }

        public override void Render(params object[] args)
        {
            var fieldWidth = (int)args[0];

            EditorGUILayout.SelectableLabel(ValToString(), EditorStyles.textField, GUILayout.Width(m_editable ? fieldWidth - 20 : fieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (m_editable && GUILayout.Button("E", GUILayout.Width(16), GUILayout.Height(16)))
            {
                m_valueList = new List<DictEntry>();

                foreach (var entry in GetValue() as Dictionary<object, object>)
                {
                    m_valueList.Add(new DictEntry
                    {
                        Key = entry.Key,
                        Value = entry.Value
                    });
                }

                m_dictSize = m_valueList.Count;

                //TODO clamp to window size
                m_currentPopupRect.position = Event.current.mousePosition - (m_currentPopupRect.size / 2);

                m_popupOpened = true;
            }

            if (m_popupOpened)
            {
                m_currentPopupRect = GUILayout.Window(1, m_currentPopupRect, RenderPopup, "Edit Value");
            }
        }

        private void RenderPopup(int windowID)
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            {
                UIHelpers.RenderHorizontalGroup(() =>
                {
                    EditorGUILayout.LabelField("Size:", GUILayout.Width(40));
                    GUI.SetNextControlName(m_dictFieldName);
                    m_dictSize = EditorGUILayout.IntField(m_dictSize, GUILayout.Width(100));
                    m_dictSize = Math.Max(m_dictSize, 0);

                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        m_dictSize++;
                    }
                });

                if (GUI.GetNameOfFocusedControl() != m_dictFieldName || Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    while (m_dictSize != m_valueList.Count)
                    {
                        if (m_dictSize > m_valueList.Count)
                        {
                            //TODO get default values for key/value
                            m_valueList.Add(GetDefaultValue());
                        }
                        else
                        {
                            m_valueList.RemoveAt(m_valueList.Count - 1);
                        }
                    }
                }

                EditorGUILayout.Separator();

                if (m_valueList.Count > 0)
                {
                    var indexToRemove = new List<int>();

                    for (var i = 0; i < m_valueList.Count; i++)
                    {
                        var value = m_valueList[i];

                        UIHelpers.RenderHorizontalGroup(() =>
                        {
                            value.Key = RenderKeyField(value.Key);
                            value.Value = RenderValueField(value.Value);

                            if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(16)))
                            {
                                indexToRemove.Add(i);
                            }
                        });

                        m_valueList[i] = value;
                    }

                    if (indexToRemove.Count > 0)
                    {
                        m_valueList = m_valueList.Where((t, i) => !indexToRemove.Contains(i)).ToList();
                        m_dictSize = m_valueList.Count;
                    }

                    EditorGUILayout.Separator();
                }

                GUILayout.FlexibleSpace();
                if (m_editable && GUILayout.Button("Save & Close"))
                {
                    var dict = new Dictionary<object, object>();

                    foreach (var dictEntry in m_valueList)
                    {
                        if (dictEntry.Key != null && dictEntry.Key != FieldBase.NullRefToken && !dict.ContainsKey(dictEntry.Key))
                        {
                            dict[dictEntry.Key] = dictEntry.Value;
                        }
                    }

                    GetRow().SetValue(m_fieldName, dict);
                    m_popupOpened = false;
                }

                if (GUILayout.Button("Close"))
                {
                    m_popupOpened = false;
                }
            }
            EditorGUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private object RenderKeyField(object keyValue)
        {
            EditorGUILayout.LabelField("Key:", GUILayout.Width(35));
            return m_keyComponent.RenderField(keyValue, m_keySize, RenderState.Dictionary);
        }

        private object RenderValueField(object value)
        {
            EditorGUILayout.LabelField("Value:", GUILayout.Width(40));
            return m_valueComponent.RenderField(value, m_fieldSize, RenderState.Dictionary);
        }

        private DictEntry GetDefaultValue()
        {
            var dictType = GetField().GetTypeArg<DictionaryType>();

            return new DictEntry
            {
                Key = TypeUtils.GetDefaultValue(TypeUtils.KeyTypeToFieldType(dictType.KeyType)),
                Value = TypeUtils.GetDefaultValue(dictType.ValueType)

            };
        }

        private object GetValue()
        {
            return GetRow().GetValue(m_fieldName);
        }

        private Field GetField()
        {
            Field field = null;

            try
            {
                field = (Field)m_source.GameDB.Tables[m_tableName].Fields[m_fieldName];
            }
            catch { }

            return field;
        }

        private RowModel GetRow()
        {
            return (RowModel)m_source.GameDB.Tables[m_tableName].Data[m_rowName];
        }

        private string ValToString()
        {
            return GameDBLibrary.MiniJSON.Json.Serialize(DictionaryTypeUtils.SerializeValue(GetField().GetTypeArg<DictionaryType>(), GetValue()));
        }
    }
}
