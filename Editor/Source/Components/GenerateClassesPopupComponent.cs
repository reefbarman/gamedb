using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class GenerateClassesPopupComponent : Component
    {
        public Action OnPopupClosed = null;

        private Rect m_popupRect = new Rect(100, 100, 500, 80);
        private bool m_exportForUnity = true;

        private bool m_inited = false;

        public GenerateClassesPopupComponent(string name) : base(name) { }

        public override void Render(params object[] args)
        {
            if (!m_inited)
            {
                m_popupRect.position = Event.current.mousePosition - (m_popupRect.size / 2);
                m_inited = true;
            }

            m_popupRect = GUILayout.Window(2, m_popupRect, RenderPopup, "Generate");
        }

        private void RenderPopup(int windowID)
        {
            if (GUI.Button(new Rect(480, 0, 18, 16), "x"))
            {
                ClosePopup();
            }

            UIHelpers.RenderHorizontalGroup(delegate
            {
                UIHelpers.RenderTextField("Source Location:", Settings.Instance.ExportPath, new UIHelpers.FieldLayout(100, 400));

                if (GUILayout.Button("Change"))
                {
                    var path = Application.dataPath;

                    if (!string.IsNullOrEmpty(Settings.Instance.ExportPath))
                    {
                        path = Path.Combine(path, Settings.Instance.ExportPath);
                    }

                    SaveExportLocation(EditorUtility.OpenFolderPanel("Select source directory", path, ""));
                }
            });

            m_exportForUnity = UIHelpers.RenderToggle("Generate for Unity:", m_exportForUnity, new UIHelpers.FieldLayout(120, 140));

            if (GUILayout.Button("Generate", GUILayout.Width(140)))
            {
                ClosePopup();
                if (ExportGameDB(m_exportForUnity))
                {
                    EditorUtility.DisplayDialog("Generate", "The class generation was successful", "OK");
                }
            }

            GUI.DragWindow();
        }

        public bool ExportGameDB(bool exportForUnity)
        {
            if (!GameDB.Instance.Save())
            {
                EditorUtility.DisplayDialog("Generate", "GameDB could not be saved, so no classes were generated.", "OK");
                return false;
            }

            try
            {
                new CSharpExporter().Export(Settings.Instance.ExportPath, GameDB.Instance, exportForUnity);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Generate", exception.Message, "OK");
                return false;
            }
        }

        public string GetExportLocation()
        {
            return Settings.Instance.ExportPath;
        }

        public void SetExportLocation(string path)
        {
            SaveExportLocation(path);
        }

        public void SaveExportLocation(string path)
        {
            path = Utils.GetRelativeDataPath(path);
            if (path == null)
            {
                return;
            }

            if (Settings.Instance.ExportPath != path)
            {
                Settings.Instance.ExportPath = path;
                Settings.Instance.Save();
            }
        }

        private void ClosePopup()
        {
            OnPopupClosed?.Invoke();
            m_inited = false;
        }
    }
}
