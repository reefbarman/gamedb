using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary
{
    public enum FieldType { @bool, color, dictionary, @enum, @float, @int, @string, tableRef, unityObject, vector2, vector3, vector4, @long, @double }
    public enum KeyType { @enum, @string }

    internal static class TypeUtils
    {
        public static string[] GetKeyTypeNames()
        {
            var typeNames = Enum.GetNames(typeof(KeyType));

            typeNames = typeNames.Select(s => s.First().ToString().ToUpper() + s.Substring(1)).ToArray();

            return typeNames;
        }

        public static string[] GetTypeNames()
        {
            var typeNames = Enum.GetNames(typeof(FieldType));

            typeNames = typeNames.Select(s => s.First().ToString().ToUpper() + s.Substring(1)).ToArray();
            typeNames[(int)FieldType.tableRef] = "Table Reference";
            typeNames[(int)FieldType.dictionary] = "Dictionary";

            return typeNames;
        }

        public static FieldType KeyTypeToFieldType(KeyType type)
        {
            switch (type)
            {
                case KeyType.@string:
                    return FieldType.@string;
                case KeyType.@enum:
                    return FieldType.@enum;
                default:
                    return FieldType.tableRef;
            }
        }

        public static object DeserializeValue(FieldType type, bool isArray, Type systemType, object val)
        {
            switch (type)
            {
                case FieldType.@enum:
                    if (isArray)
                    {
                        var listVal = val as List<object>;

                        var enumList = new List<object>();

                        foreach (var enumVal in listVal)
                        {
                            enumList.Add(Enum.Parse(systemType, enumVal as string));
                        }

                        val = enumList;
                    }
                    else
                    {
                        val = Enum.Parse(systemType, val as string);
                    }
                    break;
                case FieldType.unityObject:
                    if (isArray)
                    {
                        var listVal = val as List<object>;
                        var referenceList = new List<object>();

                        foreach (var referenceVal in listVal)
                        {
                            referenceList.Add(UnityObjectReferenceWire.Parse(referenceVal));
                        }

                        val = referenceList;
                    }
                    else
                    {
                        val = UnityObjectReferenceWire.Parse(val);
                    }
                    break;
                case FieldType.@long:
                    if (isArray)
                    {
                        var listVal = val as List<object>;
                        val = listVal.Select(NormalizeInt64).Cast<object>().ToList();
                    }
                    else
                    {
                        val = NormalizeInt64(val);
                    }
                    break;
                case FieldType.@double:
                    if (isArray)
                    {
                        var listVal = val as List<object>;
                        val = listVal.Select(NormalizeDouble).Cast<object>().ToList();
                    }
                    else
                    {
                        val = NormalizeDouble(val);
                    }
                    break;
                case FieldType.color:
                    if (isArray)
                    {
                        var listVal = val as List<object>;

                        var colorList = new List<object>();

                        foreach (var enumVal in listVal)
                        {
                            colorList.Add(new Color(enumVal as string));
                        }

                        val = colorList;
                    }
                    else
                    {
                        val = new Color(val as string);
                    }
                    break;
                case FieldType.vector2:
                    if (isArray)
                    {
                        var listVal = val as List<object>;

                        var colorList = new List<object>();

                        foreach (var enumVal in listVal)
                        {
                            colorList.Add(new Vector2(enumVal as string));
                        }

                        val = colorList;
                    }
                    else
                    {
                        val = new Vector2(val as string);
                    }
                    break;
                case FieldType.vector3:
                    if (isArray)
                    {
                        var listVal = val as List<object>;

                        var colorList = new List<object>();

                        foreach (var enumVal in listVal)
                        {
                            colorList.Add(new Vector3(enumVal as string));
                        }

                        val = colorList;
                    }
                    else
                    {
                        val = new Vector3(val as string);
                    }
                    break;
                case FieldType.vector4:
                    if (isArray)
                    {
                        var listVal = val as List<object>;

                        var colorList = new List<object>();

                        foreach (var enumVal in listVal)
                        {
                            colorList.Add(new Vector4(enumVal as string));
                        }

                        val = colorList;
                    }
                    else
                    {
                        val = new Vector4(val as string);
                    }
                    break;
            }

            return val;
        }

        public static object GetDefaultValue(FieldType type, bool isArray = false)
        {
            object defaultValue = null;

            switch (type)
            {
                case FieldType.@string:
                    if (isArray)
                    {
                        defaultValue = new List<string>();
                    }
                    else
                    {
                        defaultValue = string.Empty;
                    }
                    break;
                case FieldType.@int:
                    if (isArray)
                    {
                        defaultValue = new List<int>();
                    }
                    else
                    {
                        defaultValue = 0;
                    }
                    break;
                case FieldType.@float:
                    if (isArray)
                    {
                        defaultValue = new List<float>();
                    }
                    else
                    {
                        defaultValue = 0f;
                    }
                    break;
                case FieldType.@long:
                    if (isArray)
                    {
                        defaultValue = new List<long>();
                    }
                    else
                    {
                        defaultValue = 0L;
                    }
                    break;
                case FieldType.@double:
                    if (isArray)
                    {
                        defaultValue = new List<double>();
                    }
                    else
                    {
                        defaultValue = 0d;
                    }
                    break;
                case FieldType.@bool:
                    if (isArray)
                    {
                        defaultValue = new List<bool>();
                    }
                    else
                    {
                        defaultValue = false;
                    }
                    break;
                case FieldType.@enum:
                    if (isArray)
                    {
                        defaultValue = new List<object>();
                    }
                    else
                    {
                        defaultValue = 0;
                    }
                    break;
                case FieldType.tableRef:
                    if (isArray)
                    {
                        defaultValue = new List<string>();
                    }
                    else
                    {
                        defaultValue = string.Empty;
                    }
                    break;
                case FieldType.color:
                    if (isArray)
                    {
                        defaultValue = new List<Color>();
                    }
                    else
                    {
                        defaultValue = new Color(0, 0, 0);
                    }
                    break;
                case FieldType.vector2:
                    if (isArray)
                    {
                        defaultValue = new List<Vector2>();
                    }
                    else
                    {
                        defaultValue = new Vector2(0, 0);
                    }
                    break;
                case FieldType.vector3:
                    if (isArray)
                    {
                        defaultValue = new List<Vector3>();
                    }
                    else
                    {
                        defaultValue = new Vector3(0, 0, 0);
                    }
                    break;
                case FieldType.vector4:
                    if (isArray)
                    {
                        defaultValue = new List<Vector4>();
                    }
                    else
                    {
                        defaultValue = new Vector4(0, 0, 0, 0);
                    }
                    break;
                case FieldType.unityObject:
                    if (isArray)
                    {
                        defaultValue = new List<UnityObjectReference>();
                    }
                    else
                    {
                        defaultValue = UnityObjectReference.Empty;
                    }
                    break;
                case FieldType.dictionary:
                    if (isArray)
                    {
                        throw new ArgumentException();
                    }

                    defaultValue = new Dictionary<object, object>();
                    break;
            }

            return defaultValue;
        }

        public static Type GetSystemType(FieldType fieldType, Type overrideType)
        {
            Type type = null;

            switch (fieldType)
            {
                case FieldType.@string:
                    type = typeof(String);
                    break;
                case FieldType.@int:
                    type = typeof(Int32);
                    break;
                case FieldType.@float:
                    type = typeof(Single);
                    break;
                case FieldType.@long:
                    type = typeof(Int64);
                    break;
                case FieldType.@double:
                    type = typeof(Double);
                    break;
                case FieldType.@bool:
                    type = typeof(Boolean);
                    break;
                case FieldType.@enum:
                    type = overrideType;
                    break;
                case FieldType.tableRef:
                    type = typeof(String);
                    break;
                case FieldType.color:
                    type = typeof(Color);
                    break;
                case FieldType.vector2:
                    type = typeof(Vector2);
                    break;
                case FieldType.vector3:
                    type = typeof(Vector3);
                    break;
                case FieldType.vector4:
                    type = typeof(Vector4);
                    break;
                case FieldType.unityObject:
                    type = typeof(UnityObjectReference);
                    break;
                case FieldType.dictionary:
                    type = overrideType;
                    break;
            }

            return type;
        }

        private static long NormalizeInt64(object value)
        {
            if (!NumericValue.TryNormalizeInt64(value, out var normalized))
            {
                throw new FormatException("Value is not a valid Int64.");
            }

            return normalized;
        }

        private static double NormalizeDouble(object value)
        {
            if (!NumericValue.TryNormalizeDouble(value, out var normalized))
            {
                throw new FormatException("Value is not a finite Double.");
            }

            return normalized;
        }
    }
}
