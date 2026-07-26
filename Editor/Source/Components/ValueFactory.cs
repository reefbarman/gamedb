using GameDBLibrary;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal static class ValueFactory
    {
        public static ValueComponent GetValueComponent(FieldType type, Type systemType)
        {
            switch (type)
            {
                case FieldType.@string:
                    return new StringValueComponent();
                case FieldType.@int:
                    return new IntValueComponent();
                case FieldType.@bool:
                    return new BoolValueComponent();
                case FieldType.@float:
                    return new FloatValueComponent();
                case FieldType.@long:
                    return new LongValueComponent();
                case FieldType.@double:
                    return new DoubleValueComponent();
                case FieldType.@enum:
                    return (ValueComponent)Activator.CreateInstance(typeof(EnumValueComponent<>).MakeGenericType(systemType));
                case FieldType.tableRef:
                    return new TableRefValueComponent();
                case FieldType.color:
                    return new ColorValueComponent();
                case FieldType.vector2:
                    return new Vector2ValueComponent();
                case FieldType.vector3:
                    return new Vector3ValueComponent();
                case FieldType.vector4:
                    return new Vector4ValueComponent();
                case FieldType.unityObject:
                    return new UnityObjectValueComponent();
            }

            return null;
        }

        public static Component Create(string name, GameDBDataSource source, string tableName, string fieldName, string rowName, bool editable)
        {
            var field = (Field)source.GameDB.Tables[tableName].Fields[fieldName];

            if (field.Type == FieldType.dictionary)
            {
                return new DictionaryValueComponent(name, source, tableName, fieldName, rowName, editable);
            }

            var valueComponent = GetValueComponent(field.Type, field.GetSystemType());

            if (field.Type == FieldType.tableRef)
            {
                try
                {
                    (valueComponent as TableRefValueComponent).Table = source.GameDB.Tables[field.GetTypeArg<string>()];
                }
                catch (KeyNotFoundException)
                {
                    Debug.LogError($"Table Reference {name} Exists for missing table: {field.GetTypeArg<string>()}");
                }
            }

            return new ValueContainerComponent(name, valueComponent, source, tableName, fieldName, rowName, editable);
        }
    }
}
