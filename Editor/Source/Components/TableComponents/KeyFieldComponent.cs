using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary {
    internal class KeyFieldComponent : HeaderComponent {
        public KeyFieldComponent(string name) : base(name) {}

        protected override void RenderHeader(int width) {
            EditorGUILayout.LabelField("Key", GUILayout.Width(width));
            m_renderArea = GUILayoutUtility.GetLastRect();
        }
    }
}
