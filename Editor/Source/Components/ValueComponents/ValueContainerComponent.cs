using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace GameDBEditorLibrary
{
    internal class ValueContainerComponent : Component
    {
        protected GameDBDataSource m_source;
        protected string m_tableName;
        protected string m_fieldName;
        protected string m_rowName;
        protected bool m_complexEditable;

        private readonly bool m_editable;

        private readonly string m_arrayFieldName;
        private bool m_popupOpened;

        private Rect m_currentPopupRect;

        private Vector2 m_scrollPos;
        private int m_arraySize;
        private List<object> m_valueArray;
        private object m_complexValue;

        private readonly ValueComponent m_valueComponent;

        public ValueContainerComponent(string name, ValueComponent valComponent, GameDBDataSource source, string tableName, string fieldName, string rowName, bool editable) : base(name)
        {
            m_source = source;
            m_valueComponent = valComponent;
            m_tableName = tableName;
            m_fieldName = fieldName;
            m_rowName = rowName;
            m_editable = editable;
            m_arrayFieldName = fieldName;
        }

        public override void Render(params object[] args)
        {
            var fieldWidth = (int)args[0];

            if (GetField().IsArray || GetField().IsComplex())
            {
                if (!m_complexEditable || GetField().IsArray) 
                {
                    EditorGUILayout.SelectableLabel(ValToString(), EditorStyles.textField, GUILayout.Width(m_editable ? fieldWidth - 20 : fieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                else 
                {
                    var fieldVal = RenderField(GetValue(), fieldWidth, m_editable ? RenderState.Inline : RenderState.InlineReadOnly);

                    if (m_editable) 
                    {
                        GetRow().SetValue(m_fieldName, fieldVal);
                    }
                }

                if (m_editable && GUILayout.Button("E", GUILayout.Width(16), GUILayout.Height(16))) 
                {
                    if (GetField().IsArray)
                    {
                        var valueArray = GetArray();
                        m_arraySize = valueArray.Count;
                        m_valueArray = valueArray;

                        m_currentPopupRect = m_valueComponent.ArrayPopupRect;
                    }
                    else 
                    {
                        m_complexValue = GetValue();

                        m_currentPopupRect = m_valueComponent.ComplexPopupRect;
                    }

                    //TODO clamp to window size
                    m_currentPopupRect.position = Event.current.mousePosition - (m_currentPopupRect.size / 2);

                    m_popupOpened = true;
                }

                if (m_popupOpened) 
                {
                    m_currentPopupRect = GUILayout.Window(1, m_currentPopupRect, RenderPopup, "Edit Value");
                }
            }
            else
            {
                var fieldVal = RenderField(GetValue(), fieldWidth);

                if (m_editable)
                {
                    GetRow().SetValue(m_fieldName, fieldVal);
                }
            }
        }

        private void RenderPopup(int windowID)
        {
            if (GetField().IsArray)
            {
                RenderArrayPopup();
            }
            else
            {
                RenderComplexPopup();
            }
        }

        private void RenderArrayPopup()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Size", GUILayout.Width(40));
                    GUI.SetNextControlName(m_arrayFieldName);
                    m_arraySize = EditorGUILayout.IntField(m_arraySize, GUILayout.Width(100));
                    m_arraySize = Math.Max(m_arraySize, 0);

                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        m_arraySize++;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUI.GetNameOfFocusedControl() != m_arrayFieldName || Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    while (m_arraySize != m_valueArray.Count)
                    {
                        if (m_arraySize > m_valueArray.Count)
                        {
                            m_valueArray.Add(GetField().GetDefaultValue(false));
                        }
                        else
                        {
                            m_valueArray.RemoveAt(m_valueArray.Count - 1);
                        }
                    }
                }

                EditorGUILayout.Separator();

                if (m_valueArray.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    {
                        var indexToRemove = new List<int>();

                        for (var i = 0; i < m_valueArray.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                m_valueArray[i] = RenderField(m_valueArray[i], 140, RenderState.PopupArray);
                                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(16)))
                                {
                                    indexToRemove.Add(i);
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        if (indexToRemove.Count > 0)
                        {
                            m_valueArray = m_valueArray.Where((t, i) => !indexToRemove.Contains(i)).ToList();
                            m_arraySize = m_valueArray.Count;
                        }
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Separator();
                }

                GUILayout.FlexibleSpace();
                if (m_editable && GUILayout.Button("Save & Close"))
                {
                    GetRow().SetValue(m_fieldName, m_valueArray);
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

        private void RenderComplexPopup()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            {
                m_complexValue = RenderField(m_complexValue, 120, RenderState.Popup);

                GUILayout.FlexibleSpace();
                if (m_editable && GUILayout.Button("Save & Close"))
                {
                    GetRow().SetValue(m_fieldName, m_complexValue);
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

        public object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return m_valueComponent.RenderField(value, fieldWidth, state);
        }

        private string ValToString()
        {
            var valString = string.Empty;

            if (GetField().IsArray)
            {
                var arrayText = "";

                var valueArray = GetArray();

                for (var i = 0; i < valueArray.Count; i++)
                {
                    if (!string.IsNullOrEmpty(arrayText))
                    {
                        arrayText += ",";
                    }

                    if (GetField().Type == FieldType.tableRef && valueArray[i] == null)
                    {
                        arrayText += FieldBase.NullRefToken;
                    }
                    else if (GetField().IsComplex())
                    {
                        arrayText += $"({valueArray[i]})";
                    }
                    else
                    {
                        arrayText += valueArray[i].ToString();
                    }
                }

                valString = arrayText;
            }
            else
            {
                valString = GetRow().GetValue(m_fieldName).ToString();
            }

            return valString;
        }

        private List<object> GetArray()
        {
            var value = GetRow().GetValue(m_fieldName);

            var valueList = value as IList;

            return valueList.Cast<object>().ToList();
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
    }
}
