using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class DoubleValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.DoubleField(Convert.ToDouble(value), GUILayout.Width(fieldWidth));
        }
    }
}
