using GameDBLibrary;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    internal class DictionaryTypeUtils
    {
        public static DictionaryType Deserialize(object typeArg)
        {
            DictionaryType type = null;

            if (typeArg is SortedDictionary<string, object> json)
            {
                var keyType = (KeyType)Enum.Parse(typeof(KeyType), json["key"] as string);
                var keyTypeArg = DeserializeTypeArg(TypeUtils.KeyTypeToFieldType(keyType), json["keyTypeArg"]);

                var valueType = (FieldType)Enum.Parse(typeof(FieldType), json["value"] as string);
                var valueTypeArg = DeserializeTypeArg(valueType, json["valueTypeArg"]);

                type = new DictionaryType(keyType, keyTypeArg, valueType, valueTypeArg);
            }

            return type;
        }

        public static object Serialize(DictionaryType type)
        {
            return new Dictionary<string, object>
            {
                { "key", type.KeyType.ToString() },
                { "keyTypeArg", type.KeyTypeArg?.ToString() },
                { "value", type.ValueType.ToString() },
                { "valueTypeArg", type.ValueTypeArg?.ToString() }
            };
        }

        public static object SerializeValue(DictionaryType type, object val)
        {
            var dict = val as Dictionary<object, object>;

            var serializedDict = new Dictionary<object, object>();

            foreach (var entry in dict)
            {
                serializedDict.Add(TypeHelpers.SerializeType(TypeUtils.KeyTypeToFieldType(type.KeyType), false, entry.Key), TypeHelpers.SerializeType(type.ValueType, false, entry.Value));
            }

            return serializedDict;
        }

        private static object DeserializeTypeArg(FieldType type, object typeArgObj)
        {
            object typeArg = null;

            switch (type)
            {
                case FieldType.@enum:
                    var typeArgStr = typeArgObj as string;
                    typeArg = AssemblyExplorer.Instance.GetType(typeArgStr);

                    if (typeArg == null)
                    {
                        throw new FormatException("can't find enum type: " + typeArgObj);
                    }

                    break;
                case FieldType.tableRef:
                    typeArg = typeArgObj as string;

                    if (typeArg == null)
                    {
                        throw new FormatException("can't find tableRef type: " + typeArgObj);
                    }

                    break;
            }

            return typeArg;
        }
    }
}
