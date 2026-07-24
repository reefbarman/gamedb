using UnityEditor;
using UnityEngine;
using Vector2 = GameDBLibrary.Vector2;

namespace GameDBEditorLibrary
{
    internal class Vector2ValueComponent : ValueComponent
    {
        public override Rect ComplexPopupRect { get; } = new Rect(100, 100, 230, 120);
        public override Rect ArrayPopupRect { get; } = new Rect(100, 100, 240, 250);

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            var origMode = EditorGUIUtility.wideMode;

            var label = "Vector2";

            if (state == RenderState.Dictionary)
            {
                label = "";
                EditorGUIUtility.wideMode = true;
            }

            value = EditorGUILayout.Vector2Field(label, ((Vector2)value).ToUnityVector(), GUILayout.Width(GetFieldWidth(fieldWidth, state))).ToGameDBVector();

            EditorGUIUtility.wideMode = origMode;

            return value;
        }

        private int GetFieldWidth(int orig, RenderState state)
        {
            switch (state)
            {
                case RenderState.Popup:
                case RenderState.PopupArray:
                    return 180;
                default:
                    return orig;
            }
        }
    }
}
