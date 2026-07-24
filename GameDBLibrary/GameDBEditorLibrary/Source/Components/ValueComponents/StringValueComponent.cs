using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class StringValueComponent : ValueComponent
    {
        public override bool ComplexEditable { get; } = true;
        public override Rect ArrayPopupRect { get; } = new Rect(100, 100, 680, 480);
        public override Rect ComplexPopupRect { get; } = new Rect(100, 100, 680, 480);

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            switch (state) 
            {
                case RenderState.Popup:
                    return EditorGUILayout.TextArea(value as string, GUILayout.Width(660), GUILayout.Height(400));
                case RenderState.PopupArray:
                    return EditorGUILayout.TextArea(value as string, GUILayout.Width(620), GUILayout.Height(100));
                default:
                    return EditorGUILayout.TextField(value as string, GUILayout.Width(state == RenderState.Inline ? fieldWidth - 20 : fieldWidth));
            }
        }
    }
}
