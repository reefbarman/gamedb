using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class LongValueComponent : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            return EditorGUILayout.LongField(Convert.ToInt64(value), GUILayout.Width(fieldWidth));
        }
    }
}
