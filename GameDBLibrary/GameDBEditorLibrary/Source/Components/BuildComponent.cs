using GameDBLibrary;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class BuildComponent : Component
    {
        private bool m_expanded = false;

        public BuildComponent(string name) : base(name) {}

        public override void Render(params object[] args)
        {
            EditorGUILayout.Separator();

            m_expanded = UIHelpers.RenderFoldout("Build Binary GameDB", m_expanded, true);

            if (m_expanded)
            {
                UIHelpers.RenderHorizontalGroup(delegate {
                    UIHelpers.RenderTextField("Build Location:", Settings.Instance.BuildPath, new UIHelpers.FieldLayout(90, 300));
                    if (GUILayout.Button("Change", GUILayout.Width(155)))
                    {
                        var path = Application.dataPath;
                        if (!string.IsNullOrEmpty(Settings.Instance.BuildPath))
                        {
                            path = Path.Combine(path, Settings.Instance.BuildPath);
                        }

                        SaveBuildLocation(EditorUtility.OpenFolderPanel("Select build directory", path, ""));
                    }
                });

                if (GUILayout.Button("Build", GUILayout.Width(150)))
                {
                    BuildGameDB();
                }
            }
        }

        private void SaveBuildLocation(string buildLocation)
        {
            var path = Utils.GetRelativeDataPath(buildLocation);

            if (Settings.Instance.BuildPath != path)
            {
                Settings.Instance.BuildPath = path;
                Settings.Instance.Save();
            }
        }

        private void BuildGameDB()
        {
            if (!string.IsNullOrEmpty(GameDB.Instance.LoadedPath))
            {
                if (GameDB.Instance.Save())
                {
                    if (GameDB.Instance.GetRawDataJSON(out var rawJson))
                    {
                        var buildPath = Settings.Instance.BuildPath;

                        var extension = buildPath.Contains("Resources") ? ".bytes" : ".gamedb";

                        var path = Path.Combine(Application.dataPath, Path.Combine(buildPath, Path.GetFileNameWithoutExtension(GameDB.Instance.LoadedPath) + extension));

                        var success = false;

                        try
                        {
                            var binary = BinaryGameDB.Serialize(rawJson, Config.Instance.EncryptionKey, Config.Instance.EncryptionSalt);

                            if (binary != null)
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path));
                                File.WriteAllBytes(path, binary);
                                success = true;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }

                        if (success)
                        {
                            AssetDatabase.Refresh();
                            EditorUtility.DisplayDialog("Success", "GameDB built successfully", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Failed", "There was an error building the GameDB. Check the logs for details", "OK");
                        }
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Load GameDB", "You need to load a gameDB before you can build", "OK");
            }
        }
    }
}
