using UnityEngine;

namespace GameDBEditorLibrary {
    internal class HeaderComponent : Component {
        protected Rect m_renderArea;

        public HeaderComponent(string name) : base(name) {
            
        }

        public override void Render(params object[] args) {
            RenderHeader((int)args[0]);
        }

        public Rect GetRenderArea() {
            return m_renderArea;
        }

        protected virtual void RenderHeader(int width) {
            
        }
    }
}
