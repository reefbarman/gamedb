using GameDBLibrary;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class TableRefValueComponent : ValueComponent
    {
        public TableBase Table { get; set; }

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            var keyList = Table.Data.Keys.ToList();
            keyList.Insert(0, FieldBase.NullRefToken);
            var keys = keyList.ToArray();

            var index = string.IsNullOrEmpty(value as string) ? 0 : Array.IndexOf(keys, value as string);

            var selectedKey = EditorGUILayout.Popup(index, keys, GUILayout.Width(fieldWidth));

            return keys[selectedKey];
        }
    }
}
