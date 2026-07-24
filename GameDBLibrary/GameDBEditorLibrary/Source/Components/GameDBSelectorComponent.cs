using System.Collections.Generic;
using System.Linq;

namespace GameDBEditorLibrary
{
    internal class GameDBSelectorComponent : Component
    {
        private int m_selectedGameDBPath = 0;
        private List<string> m_gameDBPaths = new List<string>();
        private bool m_inited = false;

        public GameDBSelectorComponent(string name) : base(name)
        {
        }

        public override void Init()
        {
            if (!m_inited)
            {
                m_gameDBPaths = Settings.Instance.GameDBPaths;

                EventSystem.Instance.RegisterEvent(Events.GAMEDB_LOADED, OnGameDBLoaded);

                base.Init();
            }
        }

        ~GameDBSelectorComponent()
        {
            EventSystem.Instance.DeregisterEvent(Events.GAMEDB_LOADED, OnGameDBLoaded);
        }

        public override void Render(params object[] args)
        {
            bool inGame = false;

            if (args.Length > 0)
            {
                inGame = (bool)args[0];
            }

            if (inGame)
            {
                m_selectedGameDBPath = UIHelpers.RenderDropDown("Base GameDB:", m_selectedGameDBPath, GetRenderableGameDBPaths(), new UIHelpers.FieldLayout(120, 500));
            }
            else
            {
                m_selectedGameDBPath = UIHelpers.RenderDropDown("GameDB:", m_selectedGameDBPath, GetRenderableGameDBPaths(), new UIHelpers.FieldLayout(60, 460));
            }
        }

        public int GetSelected()
        {
            return m_selectedGameDBPath;
        }

        private string[] GetRenderableGameDBPaths()
        {
            return m_gameDBPaths.Select(path => path.Replace("/", "\u2215")).ToArray();
        }

        private void OnGameDBLoaded(object[] args)
        {
            m_gameDBPaths = Settings.Instance.GameDBPaths;
            m_selectedGameDBPath = m_gameDBPaths.IndexOf(GameDB.Instance.LoadedPath);
        }
    }
}
