using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class FloatValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.FloatField(Convert.ToSingle(value), GUILayout.Width(fieldWidth));
        }
    }
}
