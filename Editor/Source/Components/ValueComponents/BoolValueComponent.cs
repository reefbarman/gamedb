using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class BoolValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.Toggle(Convert.ToBoolean(value), GUILayout.Width(fieldWidth));
        }
    }
}
