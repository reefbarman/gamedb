using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class ConfigEnumsComponent : Component
    {
        private bool m_enumsExpanded = false;
        private int m_arraySize = -1;
        private List<string> m_enumsList = null;

        public ConfigEnumsComponent(string name) : base(name) {}

        public override void Render(params object[] args)
        {
            m_enumsExpanded = EditorGUILayout.Foldout(m_enumsExpanded, "Imported Enums");

            if (m_enumsExpanded)
            {
                UIHelpers.RenderIndented(RenderEnumsList);
            }
            else
            {
                m_arraySize = -1;
                m_enumsList = null;
            }
        }

        private void RenderEnumsList()
        {
            if (m_arraySize == -1)
            {
                m_enumsList = Settings.Instance.ImportedEnums;
                m_arraySize = m_enumsList.Count;
            }

            UIHelpers.RenderHorizontalGroup(delegate {
                GUI.SetNextControlName("EnumsSizeField");
                m_arraySize = Math.Max(UIHelpers.RenderIntField("Size:", m_arraySize, new UIHelpers.FieldLayout(50, 100)), 0);

                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(16)))
                {
                    var availableEnums = AssemblyExplorer.Instance.EnumTypes.Select(t => t.FullName).ToArray();

                    if (availableEnums.Length > 0)
                    {
                        m_arraySize++;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Import Enums", "No enums found within Game Code", "OK");
                    }
                }
            });

            EditorGUILayout.Separator();

            if (m_enumsList != null && m_enumsList.Count > 0)
            {
                UIHelpers.RenderIndented(delegate {
                    List<int> indexToRemove = new List<int>();

                    for (int i = 0; i < m_enumsList.Count; i++)
                    {
                        UIHelpers.RenderHorizontalGroup(delegate
                        {
                            var origEnumTypes = AssemblyExplorer.Instance.EnumTypes.Select(t => t.FullName).ToArray();

                            var index = Array.IndexOf(origEnumTypes, m_enumsList[i]);

                            if (index == -1)
                            {
                                index = 0;
                            }

                            var selected = EditorGUILayout.Popup(index, origEnumTypes.Select(s => s.Replace("+", ".")).ToArray(), GUILayout.Width(240));

                            if (selected < origEnumTypes.Length)
                            {
                                var origEnumType = origEnumTypes.ElementAt(selected);
                                if (m_enumsList[i] != origEnumType)
                                {
                                    m_enumsList[i] = origEnumType;
                                    SaveImportedEnums(m_enumsList);
                                }
                            }

                            if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(16)))
                            {
                                GUI.FocusControl("EnumsSizeField");
                                indexToRemove.Add(i);
                            }
                        });
                    }

                    if (indexToRemove.Count > 0)
                    {
                        List<string> valuesToKeep = new List<string>();

                        for (int i = 0; i < m_enumsList.Count; i++)
                        {
                            if (!indexToRemove.Contains(i))
                            {
                                valuesToKeep.Add(m_enumsList[i]);
                            }
                        }

                        m_enumsList = valuesToKeep;
                        m_arraySize = valuesToKeep.Count;
                        SaveImportedEnums(m_enumsList);
                    }
                });
                EditorGUILayout.Separator();
            }

            //Handle change of size
            if (GUI.GetNameOfFocusedControl() != "EnumsSizeField" || ControlHandler.Instance.GetKeyPressed(KeyCode.Return))
            {
                while (m_arraySize != m_enumsList.Count)
                {
                    if (m_arraySize > m_enumsList.Count)
                    {
                        m_enumsList.Add(null);
                    }
                    else
                    {
                        m_enumsList.RemoveAt(m_enumsList.Count - 1);
                    }
                }
            }
        }

        public void SaveImportedEnums(List<string> importedEnum)
        {
            Settings.Instance.ImportedEnums = importedEnum.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            Settings.Instance.Save();
        }
    }
}
