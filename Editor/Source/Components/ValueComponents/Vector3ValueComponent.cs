using UnityEditor;
using UnityEngine;
using Vector3 = GameDBLibrary.Vector3;

namespace GameDBEditorLibrary
{
    internal class Vector3ValueComponent : ValueComponent
    {
        public override Rect ComplexPopupRect { get; } = new Rect(100, 100, 280, 120);
        public override Rect ArrayPopupRect { get; } = new Rect(100, 100, 290, 250);

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            var origMode = EditorGUIUtility.wideMode;

            var label = "Vector3";

            if (state == RenderState.Dictionary)
            {
                label = "";
                EditorGUIUtility.wideMode = true;
            }

            value = EditorGUILayout.Vector3Field(label, ((Vector3)value).ToUnityVector(), GUILayout.Width(GetFieldWidth(fieldWidth, state))).ToGameDBVector();

            EditorGUIUtility.wideMode = origMode;

            return value;
        }

        private int GetFieldWidth(int orig, RenderState state)
        {
            switch (state)
            {
                case RenderState.Popup:
                case RenderState.PopupArray:
                    return 250;
                default:
                    return orig;
            }
        }
    }
}
