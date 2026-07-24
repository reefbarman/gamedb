using UnityEditor;
using UnityEngine;
using Color = GameDBLibrary.Color;

namespace GameDBEditorLibrary
{
    internal class ColorValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.ColorField(((Color)value).ToUnityColor(), GUILayout.Width(fieldWidth)).ToGameDBColor();
        }
    }
}
