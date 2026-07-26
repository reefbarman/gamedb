using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    public class FieldBase
    {
        public const string NullRefToken = "~not-set~";

        protected string m_name = string.Empty;
        protected FieldType m_type = FieldType.@string;
        protected bool m_array = false;
        protected object m_typeArg = null;

        public string Name => m_name;
        public FieldType Type => m_type;
        public bool IsArray => m_array;

        public FieldBase(string name)
        {
            m_name = name;
        }

        public FieldBase(string name, FieldType type, bool array, object typeArg = null)
        {
            m_name = name;
            m_type = type;
            m_array = array;
            m_typeArg = typeArg;
        }

        public object GetDefaultValue(bool getArrayType = true)
        {
            return TypeUtils.GetDefaultValue(m_type, m_array && getArrayType);
        }

        public Type GetSystemType()
        {
            switch (m_type)
            {
                case FieldType.dictionary:
                    return GetTypeArg<DictionaryType>().GetSystemType();
                default:
                    return TypeUtils.GetSystemType(m_type, m_type == FieldType.@enum ? GetTypeArg<Type>() : null);
            }
        }

        public T GetTypeArg<T>()
        {
            return (T)m_typeArg;
        }

        public bool IsValueValid(object value)
        {
            if (Type == FieldType.dictionary)
            {
                return GetTypeArg<DictionaryType>().IsValueValid(value);
            }

            if (!IsArray)
            {
                return IsScalarValueValid(value);
            }

            if (!(value is List<object> valueList))
            {
                return false;
            }

            foreach (var item in valueList)
            {
                if (!IsScalarValueValid(item))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsScalarValueValid(object value)
        {
            if (value == null)
            {
                return Type == FieldType.tableRef;
            }

            if (Type == FieldType.unityObject)
            {
                return UnityObjectReferenceWire.TryParse(value, out _);
            }

            var expectedType = GetSystemType();
            if (expectedType.IsEnum || Type == FieldType.color || Type == FieldType.vector2
                || Type == FieldType.vector3 || Type == FieldType.vector4)
            {
                expectedType = typeof(string);
            }

            if (Type == FieldType.@int)
            {
                return NumericValue.TryNormalizeInt32(value, out _);
            }

            if (Type == FieldType.@float)
            {
                return NumericValue.TryNormalizeSingle(value, out _);
            }

            if (Type == FieldType.@long)
            {
                return NumericValue.TryNormalizeInt64(value, out _);
            }

            if (Type == FieldType.@double)
            {
                return NumericValue.TryNormalizeDouble(value, out _);
            }

            return value.GetType() == expectedType;
        }
    }
}
