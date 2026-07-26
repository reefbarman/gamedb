using GameDBLibrary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Color = GameDBLibrary.Color;
using Vector2 = GameDBLibrary.Vector2;
using Vector3 = GameDBLibrary.Vector3;
using Vector4 = GameDBLibrary.Vector4;

//TODO consider if this needs to be public
namespace GameDBEditorLibrary
{
    internal static class TypeHelpers
    {
        /// <summary>
        /// Converts a GameDB Color to a Unity Color
        /// </summary>
        /// <param name="color">The color to convert</param>
        /// <returns>The equivelant Unity Color</returns>
        public static UnityEngine.Color ToUnityColor(this Color color)
        {
            return new Color32(color.r, color.g, color.b, color.a);
        }

        /// <summary>
        /// Converts a Unity Color to a GameDB Color
        /// </summary>
        /// <param name="color">The color to convert</param>
        /// <returns>The equivelant GameDB Color</returns>
        public static Color ToGameDBColor(this UnityEngine.Color color)
        {
            Color32 color32 = color;
            return new Color(color32.r, color32.g, color32.b, color32.a);
        }

        public static UnityEngine.Vector2 ToUnityVector(this Vector2 vec)
        {
            return new UnityEngine.Vector2(vec.x, vec.y);
        }

        public static Vector2 ToGameDBVector(this UnityEngine.Vector2 vec)
        {
            return new Vector2(vec.x, vec.y);
        }

        public static UnityEngine.Vector3 ToUnityVector(this Vector3 vec)
        {
            return new UnityEngine.Vector3(vec.x, vec.y, vec.z);
        }

        public static Vector3 ToGameDBVector(this UnityEngine.Vector3 vec)
        {
            return new Vector3(vec.x, vec.y, vec.z);
        }

        public static UnityEngine.Vector4 ToUnityVector(this Vector4 vec)
        {
            return new UnityEngine.Vector4(vec.x, vec.y, vec.z, vec.w);
        }

        public static Vector4 ToGameDBVector(this UnityEngine.Vector4 vec)
        {
            return new Vector4(vec.x, vec.y, vec.z, vec.w);
        }

        public static object SerializeType(FieldType type, bool isArray, object value)
        {
            switch (type)
            {
                case FieldType.@enum:
                    if (isArray)
                    {
                        var serializedList = new List<object>();

                        var valList = (IList)value;

                        foreach (var val in valList)
                        {
                            serializedList.Add(val.ToString());
                        }

                        return serializedList;
                    }
                    else
                    {
                        return value.ToString();
                    }
                case FieldType.unityObject:
                    if (isArray)
                    {
                        var serializedList = new List<object>();
                        foreach (var reference in (IList)value)
                        {
                            serializedList.Add(UnityObjectReferenceWire.Serialize(
                                (UnityObjectReference)reference));
                        }

                        return serializedList;
                    }

                    return UnityObjectReferenceWire.Serialize((UnityObjectReference)value);
                case FieldType.tableRef:
                    if (isArray)
                    {
                        var serializedList = new List<object>();

                        var valList = (IList)value;

                        foreach (var val in valList)
                        {
                            serializedList.Add((string)val == FieldBase.NullRefToken ? null : val);
                        }

                        return serializedList;
                    }
                    else
                    {
                        return (string)value == FieldBase.NullRefToken ? null : value;
                    }
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                case FieldType.color:
                    if (isArray)
                    {
                        var serializedList = new List<object>();

                        var valList = (IList)value;

                        foreach (var val in valList)
                        {
                            serializedList.Add(val.ToString());
                        }

                        return serializedList;
                    }
                    else
                    {
                        return value.ToString();
                    }
                default:
                    return value;
            }
        }
    }
}
