using GameDBLibrary;
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDBEditorLibrary
{
    internal class UnityObjectValueComponent : ValueComponent
    {
        private Object m_cachedObject;
        private string m_cachedGuid;
        private string m_cachedResolvedPath;

        public override object RenderField(object value, int fieldWidth,
            RenderState state = RenderState.Standard)
        {
            var reference = value as UnityObjectReference
                ?? throw new ArgumentException(
                    "Unity object fields require a UnityObjectReference.", nameof(value));
            var resolvedPath = reference.IsEmpty
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(reference.Guid);

            if (!string.Equals(reference.Guid, m_cachedGuid, StringComparison.Ordinal)
                || !string.Equals(resolvedPath, m_cachedResolvedPath, StringComparison.Ordinal))
            {
                m_cachedObject = !string.IsNullOrEmpty(resolvedPath)
                    ? AssetDatabase.LoadMainAssetAtPath(resolvedPath)
                    : null;
                m_cachedGuid = reference.Guid;
                m_cachedResolvedPath = resolvedPath;
            }

            var selected = EditorGUILayout.ObjectField(
                m_cachedObject, typeof(Object), false, GUILayout.Width(fieldWidth));
            if (selected == m_cachedObject)
            {
                return reference;
            }

            if (selected == null)
            {
                return UnityObjectReference.Empty;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid)
                || !AssetDatabase.IsMainAsset(selected))
            {
                EditorUtility.DisplayDialog("Unsupported Unity object",
                    "Only main project assets can be used. Scene objects and subassets are not supported.",
                    "OK");
                return reference;
            }

            try
            {
                return new UnityObjectReference(guid, path);
            }
            catch (ArgumentException)
            {
                EditorUtility.DisplayDialog("Not found in Resources folder",
                    "The asset must be beneath exactly one Resources directory.", "OK");
                return reference;
            }
        }
    }
}
