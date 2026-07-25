using System;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class EnumValueComponent<T> : ValueComponent
    {
        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            var names = Enum.GetNames(typeof(T));

            var selectedEnum = EditorGUILayout.Popup((int)Convert.ChangeType(value, typeof(int)), names, GUILayout.Width(fieldWidth));

            value = (T)Enum.ToObject(typeof(T), selectedEnum);

            return value;
        }
    }
}
