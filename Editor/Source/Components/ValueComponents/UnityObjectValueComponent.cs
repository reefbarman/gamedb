using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDBEditorLibrary
{
    internal class UnityObjectValueComponent : ValueComponent
    {
        private Object _cachedObject;
        private string _cachedPath;

        public override object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard)
        {
            var unityObjectPath = value as string;

            if (unityObjectPath != _cachedPath)
            {
                _cachedObject = !string.IsNullOrEmpty(unityObjectPath) ? AssetDatabase.LoadAssetAtPath(unityObjectPath, typeof(Object)) : null;
                _cachedPath = unityObjectPath;
            }

            var newObj = EditorGUILayout.ObjectField(_cachedObject, typeof(Object), false, GUILayout.Width(fieldWidth));

            var objPath = AssetDatabase.GetAssetPath(newObj);

            if (!string.IsNullOrEmpty(objPath) && !objPath.Contains("Resources"))
            {
                EditorUtility.DisplayDialog("Not found in Resources folder", "Only Unity objects found in the Resources folder can be used.", "OK");
                objPath = _cachedPath;
            }

            return objPath;
        }
    }
}
