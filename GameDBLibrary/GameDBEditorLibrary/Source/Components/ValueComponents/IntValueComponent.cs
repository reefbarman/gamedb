using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class IntValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.IntField(Convert.ToInt32(value), GUILayout.Width(fieldWidth));
        }
    }
}
