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

        public static string[] GetSupportedTypes()
        {
            var typeNames = Enum.GetNames(typeof(FieldType)).ToList();

            typeNames[(int)FieldType.tableRef] = "Table Reference";

            typeNames.Remove(FieldType.dictionary.ToString());

            return typeNames.ToArray();
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
            return value is System.Collections.IDictionary;
        }

        public object DeserializeValue(object val)
        {
            var deserializedDict = new Dictionary<object, object>();

            if (val is IDictionary<string, object> dict)
            {
                foreach (var entry in dict)
                {
                    var key = TypeUtils.DeserializeValue(TypeUtils.KeyTypeToFieldType(KeyType), false, KeyType == KeyType.@enum ? (Type)KeyTypeArg : null, entry.Key);
                    var value = TypeUtils.DeserializeValue(ValueType, false, ValueType == FieldType.@enum ? (Type)ValueTypeArg : null, entry.Value);

                    deserializedDict.Add(key, value);
                }
            }

            return deserializedDict;
        }
    }
}
