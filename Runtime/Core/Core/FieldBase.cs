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
            if (value == null)
            {
                return Type == FieldType.tableRef;
            }

            var expectedType = GetSystemType();

            if (Type == FieldType.dictionary)
            {
                return GetTypeArg<DictionaryType>().IsValueValid(value);
            }

            if (expectedType.IsEnum || Type == FieldType.color || Type == FieldType.vector2 || Type == FieldType.vector3 || Type == FieldType.vector4)
            {
                expectedType = typeof(string);
            }

            if (IsArray)
            {
                if (value.GetType() != typeof(List<object>)) return false;

                var valueList = value as List<object>;

                if (valueList.Count == 0) return true;

                value = valueList[0];
            }

            if (Type == FieldType.@int)
            {
                if (!IsNumber(value))
                {
                    return false;
                }

                try
                {
                    var number = Convert.ToInt64(value);
                    return number >= int.MinValue && number <= int.MaxValue && Convert.ToDecimal(value) == number;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (Type == FieldType.@float)
            {
                return IsNumber(value);
            }

            return value.GetType() == expectedType;
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }
    }
}
