using UnityEditor;
using UnityEngine;
using Vector4 = GameDBLibrary.Vector4;

namespace GameDBEditorLibrary
{
    internal class Vector4ValueComponent : ValueComponent
    {
        public override Rect ComplexPopupRect { get; } = new Rect(100, 100, 330, 120);
        public override Rect ArrayPopupRect { get; } = new Rect(100, 100, 340, 250);

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {

            var origMode = EditorGUIUtility.wideMode;

            var label = "Vector4";

            if (state == RenderState.Dictionary)
            {
                label = "";
                EditorGUIUtility.wideMode = true;
            }

            value = EditorGUILayout.Vector4Field(label, ((Vector4)value).ToUnityVector(), GUILayout.Width(GetFieldWidth(fieldWidth, state))).ToGameDBVector();

            EditorGUIUtility.wideMode = origMode;

            return value;
        }

        private int GetFieldWidth(int orig, RenderState state)
        {
            switch (state)
            {
                case RenderState.Popup:
                case RenderState.PopupArray:
                    return 300;
                default:
                    return orig;
            }
        }
    }
}
