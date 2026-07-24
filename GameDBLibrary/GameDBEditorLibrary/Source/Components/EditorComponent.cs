using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class EditorComponent : Component
    {
        public EditorComponent(string name, EditorWindow editorWindow) : base(name)
        {
            AddChild(new EditorTabComponent("EditorTab", editorWindow));
        }

        public override void Render(params object[] args)
        {
            GUILayout.Label("GameDB Editor", EditorStyles.boldLabel);
            RenderChild("EditorTab");
        }
    }
}
