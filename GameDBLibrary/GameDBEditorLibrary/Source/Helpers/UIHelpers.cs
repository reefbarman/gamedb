using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal static class UIHelpers
    {
        public struct FieldLayout
        {
            public int LabelWidth;
            public int Width;

            public FieldLayout(int labelWidth, int width)
            {
                LabelWidth = labelWidth;
                Width = width;
            }
        }

        public static void RenderHorizontalGroup(Action onRender)
        {
            EditorGUILayout.BeginHorizontal();
            {
                onRender();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void RenderIndented(Action onRender)
        {
            EditorGUI.indentLevel++;
            {
                onRender();
            }
            EditorGUI.indentLevel--;
        }

        public static void RenderBox(Action onRender, Color backgroundColor, int minWidth = -1)
        {
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;

            if (minWidth >= 0)
            {
                EditorGUILayout.BeginVertical("Box", GUILayout.MinWidth(minWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical("Box");
            }

            GUI.backgroundColor = defaultColor;

            onRender();

            EditorGUILayout.EndVertical();
        }

        public static void RenderDivider()
        {
            EditorGUILayout.Separator();
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        }

        public static string RenderTextField(string label, string inputText, FieldLayout layout)
        {
            EditorGUIUtility.labelWidth = layout.LabelWidth;
            var outString = EditorGUILayout.TextField(label, inputText, GUILayout.Width(layout.Width));
            EditorGUIUtility.labelWidth = 0; //reset

            return outString;
        }

        public static bool RenderToggle(string label, bool enabled, FieldLayout layout)
        {
            EditorGUIUtility.labelWidth = layout.LabelWidth;
            enabled = EditorGUILayout.Toggle(label, enabled, GUILayout.Width(layout.Width));
            EditorGUIUtility.labelWidth = 0; //reset

            return enabled;
        }

        public static int RenderIntField(string label, int value, FieldLayout layout)
        {
            EditorGUIUtility.labelWidth = layout.LabelWidth;
            value = EditorGUILayout.IntField(label, value, GUILayout.Width(layout.Width));
            EditorGUIUtility.labelWidth = 0; //Reset

            return value;
        }

        public static int RenderDropDown(string label, int selected, string[] options, FieldLayout layout)
        {
            EditorGUIUtility.labelWidth = layout.LabelWidth;
            selected = EditorGUILayout.Popup(label, selected, options, GUILayout.Width(layout.Width));
            EditorGUIUtility.labelWidth = 0; //reset

            return selected;
        }

        public static bool RenderFoldout(string label, bool expanded, bool bold)
        {
            GUIStyle style = EditorStyles.foldout;
            FontStyle previousStyle = style.fontStyle;

            if (bold)
            {
                style.fontStyle = FontStyle.Bold;
            }

            expanded = EditorGUILayout.Foldout(expanded, label, style);
            style.fontStyle = previousStyle;

            return expanded;
        }

        public static Action LoadingBar(string title, string message)
        {
            var progress = 0f;
            var running = true;

            void OnUpdate()
            {
                if (running)
                {
                    EditorUtility.DisplayProgressBar(title, message, progress);
                    progress += 0.005f;
                    progress %= 1;
                }
                else
                {
                    Updater.Instance.OnUpdate -= OnUpdate;
                    EditorUtility.ClearProgressBar();
                }
            }

            Updater.Instance.OnUpdate += OnUpdate;

            return () => running = false;
        }
    }
}
