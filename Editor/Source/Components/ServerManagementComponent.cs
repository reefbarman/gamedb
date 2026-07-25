using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class ServerManagementComponent : Component
    {
        private bool m_expanded = false;

        private string m_loadedTag = string.Empty;
        private int m_loadedRevision = -1;
        private GameDBDataSource m_dataSource = new GameDBDataSource();

        public ServerManagementComponent(string name) : base(name)
        {
            AddChild(new DeploymentPickerComponent("Picker"));
            AddChild(new TablesComponent("Tables", m_dataSource, false));
        }

        public override void Init()
        {
            EventSystem.Instance.RegisterEvent(Events.REVISION_LOADED, OnRevisionLoaded);
            EventSystem.Instance.RegisterEvent(Events.REVISION_UNLOADED, OnRevisionUnloaded);

            base.Init();
        }

        ~ServerManagementComponent()
        {
            EventSystem.Instance.DeregisterEvent(Events.REVISION_LOADED, OnRevisionLoaded);
            EventSystem.Instance.DeregisterEvent(Events.REVISION_UNLOADED, OnRevisionUnloaded);
        }

        public override void Render(params object[] args)
        {
            EditorGUILayout.Separator();

            m_expanded = UIHelpers.RenderFoldout("Deploy to Server", m_expanded, true);
            if (m_expanded)
            {
                SaveGameDBServer(UIHelpers.RenderTextField("GameDB Server Host:", Settings.Instance.GameDBServer, new UIHelpers.FieldLayout(140, 460)));
                SaveDownloadServer(UIHelpers.RenderTextField("Download Server Host:", Settings.Instance.DownloadServer, new UIHelpers.FieldLayout(140, 460)));

                EditorGUILayout.Separator();

                RenderChild("Picker");

                UIHelpers.RenderDivider();

                RenderChild("Tables");
            }
        }

        private void SaveGameDBServer(string server)
        {
            if (server != Settings.Instance.GameDBServer)
            {
                Settings.Instance.GameDBServer = server;
                Settings.Instance.Save();
            }
        }

        private void SaveDownloadServer(string server)
        {
            if (server != Settings.Instance.DownloadServer)
            {
                Settings.Instance.DownloadServer = server;
                Settings.Instance.Save();
            }
        }

        private void OnRevisionLoaded(object[] args)
        {
            var tag = args[0] as string;
            var revision = Convert.ToInt32(args[1]);
            var revisionPath = args[2] as string;
            var baseDBPath = args[3] as string;
            var schemaPath = args[4] as string;

            var tablesComponenet = GetChild<TablesComponent>("Tables");
            tablesComponenet.ClearTables();

            if (m_loadedTag != tag || m_loadedRevision != revision)
            {
                var onFinished = UIHelpers.LoadingBar("Retrieving Revision", "Retrieving Revision...");

                DownloadHelper.DownloadGameDBRevision(revision, revisionPath, baseDBPath, schemaPath, (exception, dbJson, schemaJson) =>
                {
                    onFinished();
                    if (exception == null)
                    {
                        var gameDB = new GameDB();
                        if (gameDB.Import(dbJson, schemaJson))
                        {
                            m_dataSource.UpdateSource(gameDB);
                            tablesComponenet.UpdateTables();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Revision Load Error", "Failed to load the revision check console for details", "OK");
                            Debug.LogError("failed loading revision");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Revision Download Error", "Failed to download the revision check console for details", "OK");
                        Debug.LogError(exception.Message);
                    }
                });
            }
        }

        private void OnRevisionUnloaded(object[] args)
        {
            m_children.Remove("Tables");
            AddChild(new TablesComponent("Tables", m_dataSource, false));
        }
    }
}
