using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class LoaderComponent : Component
    {
        private List<string> m_gameDBPaths = new List<string>();

        private int m_selectedGameDBPath = 0;
        private int m_selectedInGameDB = 0;

        public LoaderComponent(string name) : base(name)
        {
            AddChild(new GameDBSelectorComponent("GameDBSelector"));
        }

        public override void Init()
        {
            m_gameDBPaths = Settings.Instance.GameDBPaths;

            base.Init();
        }

        public override void Render(params object[] args)
        {
            bool inGame = Application.isPlaying;

            GUILayout.Label("Load or Create GameDB", EditorStyles.boldLabel);

            if (inGame)
            {
                m_selectedInGameDB = UIHelpers.RenderDropDown("Runtime GameDB", m_selectedInGameDB, GetInGameDBs(), new UIHelpers.FieldLayout(120, 500));
            }

            RenderChild("GameDBSelector", inGame);
            m_selectedGameDBPath = GetChild<GameDBSelectorComponent>("GameDBSelector").GetSelected();

            UIHelpers.RenderHorizontalGroup(delegate
            {
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Create GameDB", GUILayout.Width(150)))
                    {
                        m_selectedGameDBPath = CreateGameDB(EditorUtility.SaveFilePanel("Create GameDB Editor", "Assets", "gameDB", "json"));
                    }

                    if (GUILayout.Button("Add Existing GameDB", GUILayout.Width(150)))
                    {
                        int selectedGameDBPath = AddGameDB(EditorUtility.OpenFilePanel("Create GameDB Editor", "Assets", "json"));
                        if (selectedGameDBPath == -1)
                        {
                            //TODO WARN USER
                        }
                        else
                        {
                            m_selectedGameDBPath = selectedGameDBPath;
                        }
                    }
                }

                GUI.SetNextControlName("LoadGameDB");
                if (GUILayout.Button("Load GameDB", GUILayout.Width(150)))
                {
                    GUI.FocusControl("LoadGameDB");
                    if (!LoadGameDB(m_selectedGameDBPath, m_selectedInGameDB))
                    {
                        //TODO WARN USER
                        Debug.LogWarning("Failed to load gameDB");
                    }
                }
            });

            EditorGUILayout.LabelField($"Loaded GameDB: {GameDB.Instance.LoadedPath}");
        }

        private string[] GetInGameDBs()
        {
            return GameDB.RuntimeDBs.Select(gameDB => gameDB.Name).ToArray();
        }

        private int AddGameDB(string gameDBPath)
        {
            int addedIndex = AddGameDBPath(gameDBPath);
            if (addedIndex < 0)
            {
                return -1;
            }

            if (GameDB.Instance.Load(m_gameDBPaths[addedIndex]))
            {
                EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
            }
            else
            {
                addedIndex = -1;
            }

            return addedIndex;
        }

        private bool LoadGameDB(int selectedGameDBIndex, int selectedInGameDBIndex)
        {
            bool success = false;

            if (Application.isPlaying)
            {
                success = GameDB.Instance.LoadRuntimeDB(selectedInGameDBIndex, m_gameDBPaths[selectedGameDBIndex]);
            }
            else
            {
                success = GameDB.Instance.Load(m_gameDBPaths[selectedGameDBIndex]);
            }

            if (success)
            {
                EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
            }

            return success;
        }

        private int CreateGameDB(string gameDBPath)
        {
            int addedIndex = AddGameDBPath(gameDBPath);
            if (addedIndex < 0)
            {
                return -1;
            }

            GameDB.Instance.Create(m_gameDBPaths[addedIndex]);
            EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);

            return addedIndex;
        }

        private int AddGameDBPath(string gameDBPath)
        {
            gameDBPath = Utils.GetRelativeDataPath(gameDBPath);
            if (string.IsNullOrEmpty(gameDBPath))
            {
                return -1;
            }

            if (m_gameDBPaths.IndexOf(gameDBPath) == -1)
            {
                m_gameDBPaths.Add(gameDBPath);
            }

            Settings.Instance.GameDBPaths = m_gameDBPaths;
            Settings.Instance.Save();

            int addedIndex = m_gameDBPaths.Count - 1;

            return addedIndex;
        }
    }
}
