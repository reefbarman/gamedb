using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary
{
    public class DictionaryType
    {
        public KeyType KeyType { get; }
        public object KeyTypeArg { get; }

        public FieldType ValueType { get; }
        public object ValueTypeArg { get; }

        public static FieldType[] GetSupportedFieldTypes()
        {
            return Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                .Where(type => type != FieldType.dictionary).ToArray();
        }

        public static string[] GetSupportedTypes()
        {
            return GetSupportedFieldTypes().Select(type =>
                type == FieldType.tableRef ? "Table Reference" : type.ToString()).ToArray();
        }

        public DictionaryType(KeyType keyType, object keyTypeArg, FieldType valueType, object valueTypeArg)
        {
            KeyType = keyType;
            KeyTypeArg = keyTypeArg;

            ValueType = valueType;
            ValueTypeArg = valueTypeArg;
        }

        public object GetDefaultValue()
        {
            //TODO may need to return dictionary of each type
            return new Dictionary<object, object>();
        }

        public Type GetSystemType()
        {
            //TODO may need to return dictionary of each type
            return typeof(Dictionary<object, object>);
        }

        public Type GetKeySystemType()
        {
            return TypeUtils.GetSystemType(TypeUtils.KeyTypeToFieldType(KeyType), KeyType == KeyType.@enum ? (Type)KeyTypeArg : null);
        }

        public Type GetValueSystemType()
        {
            return TypeUtils.GetSystemType(ValueType, ValueType == FieldType.@enum ? (Type)ValueTypeArg : null);
        }

        public bool IsValueValid(object value)
        {
            if (!(value is IDictionary<string, object> dictionary))
            {
                return false;
            }

            var keyField = new FieldBase(string.Empty, TypeUtils.KeyTypeToFieldType(KeyType), false,
                KeyType == KeyType.@enum ? KeyTypeArg : null);
            var valueField = new FieldBase(string.Empty, ValueType, false,
                ValueType == FieldType.@enum ? ValueTypeArg : null);
            foreach (var entry in dictionary)
            {
                if (!keyField.IsValueValid(entry.Key) || !valueField.IsValueValid(entry.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public object DeserializeValue(object val)
        {
            if (!(val is IDictionary<string, object> dict))
            {
                throw new FormatException("dictionary field value not a dictionary");
            }

            var deserializedDict = new Dictionary<object, object>();
            foreach (var entry in dict)
            {
                var key = TypeUtils.DeserializeValue(TypeUtils.KeyTypeToFieldType(KeyType), false,
                    KeyType == KeyType.@enum ? (Type)KeyTypeArg : null, entry.Key);
                var value = TypeUtils.DeserializeValue(ValueType, false,
                    ValueType == FieldType.@enum ? (Type)ValueTypeArg : null, entry.Value);

                deserializedDict.Add(key, value);
            }

            return deserializedDict;
        }
    }
}
