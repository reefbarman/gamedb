using GameDBLibrary;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class GameDBEditorComponent : Component
    {
        private GenerateClassesPopupComponent m_exportPopup = null;
        private GSheetsPopupComponent m_gSheetsPopup = null;

        private string m_loadedGameDB = string.Empty;
        private bool m_inited = false;

        private string m_addTableName = "";
        private bool m_exportGameDB = false;
#if !FREE_VERSION
        private bool m_gsheetsExport = false;
#endif
        private int m_selectedKeyType = (int)KeyType.@string;
        private int m_selectedEnum = 0;
        private int m_selectedTable = 0;
        private object m_typeArg = null;

        public GameDBEditorComponent(string name) : base(name)
        {
            m_exportPopup = new GenerateClassesPopupComponent("GenerateClassesPopup");
            m_gSheetsPopup = new GSheetsPopupComponent("GSheetsPopup");

            var source = new GameDBDataSource();
            source.UpdateSource(GameDB.Instance);

            AddChild(new TablesComponent("Tables", source));
            AddChild(m_exportPopup);
            AddChild(m_gSheetsPopup);
        }

        public override void Init()
        {
            if (!m_inited)
            {
                EventSystem.Instance.RegisterEvent(Events.GAMEDB_LOADED, OnGameDBLoaded);

                m_exportPopup.OnPopupClosed += ExportPopupClosed;
                m_gSheetsPopup.OnPopupClosed += GSheetsPopupClosed;
            }

            m_inited = true;

            base.Init();
        }

        ~GameDBEditorComponent()
        {
            m_exportPopup.OnPopupClosed -= ExportPopupClosed;
            m_gSheetsPopup.OnPopupClosed -= GSheetsPopupClosed;

            EventSystem.Instance.DeregisterEvent(Events.GAMEDB_LOADED, OnGameDBLoaded);
        }

        public override void Render(params object[] args)
        {
            bool inGame = Application.isPlaying;

            GUILayout.Label("GameDB", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(m_loadedGameDB))
            {
                if (!inGame)
                {
                    GameDB.Instance.ScopeName = UIHelpers.RenderTextField("GameDB Scope Name:", GameDB.Instance.ScopeName, new UIHelpers.FieldLayout(140, 400));
#if !FREE_VERSION
                    GameDB.Instance.LocalizationDB = UIHelpers.RenderToggle("Localization DB:", GameDB.Instance.LocalizationDB, new UIHelpers.FieldLayout(100, 200));
#endif
                }

                EditorGUILayout.Separator();

                if (!inGame)
                {
                    GUILayout.Label("Create Table", EditorStyles.boldLabel);

                    UIHelpers.RenderHorizontalGroup(delegate {
                        m_addTableName = UIHelpers.RenderTextField("Table Name:", m_addTableName, new UIHelpers.FieldLayout(80, 300));
                        m_selectedKeyType = UIHelpers.RenderDropDown("Key Type:", m_selectedKeyType, TypeUtils.GetKeyTypeNames(), new UIHelpers.FieldLayout(65, 200));

                        switch ((KeyType)m_selectedKeyType)
                        {
                            case KeyType.@enum:
                                var enumTypes = Settings.Instance.ImportedEnums;
                                m_selectedEnum = UIHelpers.RenderDropDown("Enum:", m_selectedEnum, enumTypes.Select(s => s.Replace("+", ".")).ToArray(), new UIHelpers.FieldLayout(50, 200));

                                if (enumTypes.Count > 0)
                                {
                                    m_typeArg = AssemblyExplorer.Instance.GetType(enumTypes[m_selectedEnum]);
                                }
                                break;
                            case KeyType.@string:
                                m_typeArg = null;
                                break;
                        }
                    });

                    if (GUILayout.Button("Create Table", GUILayout.Width(150)))
                    {
                        if (AddTable(m_addTableName, (KeyType)m_selectedKeyType, m_typeArg))
                        {
                            m_addTableName = null;
                            GUI.FocusControl("");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Table already exists!", $"A table already exists with name: {m_addTableName}", "OK");
                        }
                    }

                    EditorGUILayout.Separator();
                }

                RenderChild("Tables");

                EditorGUILayout.Separator();

                GUILayout.Label("Save & Export", EditorStyles.boldLabel);

                UIHelpers.RenderHorizontalGroup(delegate {
                    if (inGame && GUILayout.Button("Reload In-Game", GUILayout.Width(150)))
                    {
                        //TODO deal with import error
                        GameDB.Instance.ReloadRuntimeDB();
                    }

                    if (GUILayout.Button("Save GameDB", GUILayout.Width(150)))
                    {
                        GameDB.Instance.Save();

                        EditorUtility.DisplayDialog("Save GameDB", "The save was successful", "OK");
                    }

                    if (!inGame && GUILayout.Button("Generate Classes", GUILayout.Width(150)))
                    {
                        if (!string.IsNullOrEmpty(GameDB.Instance.ScopeName.Trim()))
                        {
                            m_exportGameDB = true;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Set ScopeName", "A GameDB Scope Name is required before you can export code!", "OK");
                        }
                    }

#if !FREE_VERSION
                    if (!inGame && GUILayout.Button("Google Sheets", GUILayout.Width(150)))
                    {
                        m_gsheetsExport = true;
                    }
#endif

                    if (m_exportGameDB)
                    {
                        RenderChild("GenerateClassesPopup");
                    }

#if !FREE_VERSION
                    if (m_gsheetsExport)
                    {
                        RenderChild("GSheetsPopup");
                    }
#endif
                });
            }
        }

        public bool AddTable(string tableName, KeyType type, object typeArg = null)
        {
            bool success = false;

            if (GameDB.Instance.AddTable(tableName, type, typeArg))
            {
                EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
                success = true;
            }

            return success;
        }

        private void OnGameDBLoaded(object[] args)
        {
            var tablesComponenet = GetChild<TablesComponent>("Tables");

            m_loadedGameDB = GameDB.Instance.LoadedPath;
            tablesComponenet.ClearTables();
            tablesComponenet.UpdateTables();
        }

        private void ExportPopupClosed()
        {
            m_exportGameDB = false;
        }

        private void GSheetsPopupClosed()
        {
#if !FREE_VERSION
            m_gsheetsExport = false;
#endif
        }
    }
}
