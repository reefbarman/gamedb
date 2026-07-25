using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class DeploymentComponent : Component
    {
        public DeploymentComponent(string name) : base(name)
        {
            AddChild(new GameDBSelectorComponent("GameDBSelector"));
            AddChild(new BuildComponent("Build"));
            AddChild(new ServerManagementComponent("Server"));
        }

        public override void Render(params object[] args)
        {
            RenderChild("GameDBSelector");
            if (GUILayout.Button("Load GameDB", GUILayout.Width(150)))
            {
                LoadGameDB();
            }

            EditorGUILayout.LabelField($"Loaded GameDB: {GameDB.Instance.LoadedPath}");

            UIHelpers.RenderDivider();

            RenderChild("Build");
            RenderChild("Server");
        }

        private void LoadGameDB()
        {
            var success = GameDB.Instance.Load(Settings.Instance.GameDBPaths[GetChild<GameDBSelectorComponent>("GameDBSelector").GetSelected()]);

            if (success)
            {
                EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
            }

            //TODO handle error?
        }
    }
}
