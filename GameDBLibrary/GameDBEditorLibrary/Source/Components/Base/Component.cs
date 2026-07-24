using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    internal abstract class Component
    {
        protected Dictionary<string, Component> m_children = new Dictionary<string, Component>();

        private string m_name = string.Empty;

        public string Name
        {
            get { return m_name; }
        }

        public Dictionary<string, Component> Children
        {
            get { return m_children; }
        }

        protected Component(string name)
        {
            m_name = name;
        }

        public virtual void Init()
        {
            foreach(var child in m_children)
            {
                child.Value.Init();
            }
        }

        public void AddChild(Component child)
        {
            m_children.Add(child.Name, child);
        }

        public T GetChild<T>(string name) where T : Component
        {
            Component child = null;

            if (m_children.ContainsKey(name))
            {
                child = m_children[name];
            }

            return (T)child;
        }

        public void RenderChild(string name, params object[] args)
        {
            GetChild<Component>(name)?.Render(args);
        }

        public abstract void Render(params object[] args);

        public virtual void Update()
        {
            foreach(var child in m_children)
            {
                child.Value.Update();
            }
        }
    }
}
