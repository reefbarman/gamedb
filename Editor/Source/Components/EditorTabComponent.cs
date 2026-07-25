using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class EditorTabComponent : Component
    {
        private Vector2 m_gameDBScrollPos = new Vector2();
        private Vector2 m_configurationScrollPos = new Vector2();

        private EditorWindow m_editorWindow = null;

        private int m_currentTab = 0;

        public EditorTabComponent(string name, EditorWindow editorWindow) : base(name)
        {
            m_editorWindow = editorWindow;

            AddChild(new LoaderComponent("Loader"));
            AddChild(new GameDBEditorComponent("GameDBEditor"));
            AddChild(new BuildComponent("Build"));
            AddChild(new ConfigurationComponent("Configuration"));
        }

        public override void Render(params object[] args)
        {
            var tabs = new[] { "GameDB", "Configuration" };

            m_currentTab = GUILayout.Toolbar(m_currentTab, tabs, GUILayout.Width(460));

            switch (m_currentTab)
            {
                default:
                    RenderGameDBTab();
                    break;
                case 1:
                    RenderConfigurationTab();
                    break;
            }
        }

        private void RenderGameDBTab()
        {
            m_gameDBScrollPos = EditorGUILayout.BeginScrollView(m_gameDBScrollPos);
            {
                m_editorWindow.BeginWindows();

                RenderChild("Loader");

                UIHelpers.RenderDivider();

                RenderChild("GameDBEditor");

                if (!Application.isPlaying)
                {
                    UIHelpers.RenderDivider();
                    RenderChild("Build");
                }

                m_editorWindow.EndWindows();
            }
            EditorGUILayout.EndScrollView();
        }

        private void RenderConfigurationTab()
        {
            m_configurationScrollPos = EditorGUILayout.BeginScrollView(m_configurationScrollPos);
            {
                m_editorWindow.BeginWindows();

                GUILayout.Label("GameDB Editor Configuration", EditorStyles.boldLabel);

                RenderChild("Configuration");

                m_editorWindow.EndWindows();
            }
            EditorGUILayout.EndScrollView();
        }

    }
}
