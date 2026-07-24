using System;
using System.Collections.Generic;
using System.Linq;
using GameDBLibrary;

/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/

namespace GameDBMain
{
    public class EnumKeyArraysTable : TableBase
    {
        public EnumKeyArraysTable(Func<string, RowBase> rowFactory) : base(EnumKeyArraysSchema.TableName, KeyType.@enum, typeof(Days), rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { EnumKeyArraysSchema.FieldBoolArray, new FieldBase(EnumKeyArraysSchema.FieldBoolArray, FieldType.@bool, true, null) },
                { EnumKeyArraysSchema.FieldColorArray, new FieldBase(EnumKeyArraysSchema.FieldColorArray, FieldType.@color, true, null) },
                { EnumKeyArraysSchema.FieldEnumArray, new FieldBase(EnumKeyArraysSchema.FieldEnumArray, FieldType.@enum, true, typeof(Colors)) },
                { EnumKeyArraysSchema.FieldFloatArray, new FieldBase(EnumKeyArraysSchema.FieldFloatArray, FieldType.@float, true, null) },
                { EnumKeyArraysSchema.FieldIntArray, new FieldBase(EnumKeyArraysSchema.FieldIntArray, FieldType.@int, true, null) },
                { EnumKeyArraysSchema.FieldStringArray, new FieldBase(EnumKeyArraysSchema.FieldStringArray, FieldType.@string, true, null) },
                { EnumKeyArraysSchema.FieldTableRefArray, new FieldBase(EnumKeyArraysSchema.FieldTableRefArray, FieldType.@tableRef, true, StringKeySingleSchema.TableName) },
                { EnumKeyArraysSchema.FieldUnityObjectArray, new FieldBase(EnumKeyArraysSchema.FieldUnityObjectArray, FieldType.@unityObject, true, null) },
                { EnumKeyArraysSchema.FieldVector2Array, new FieldBase(EnumKeyArraysSchema.FieldVector2Array, FieldType.@vector2, true, null) },
                { EnumKeyArraysSchema.FieldVector3Array, new FieldBase(EnumKeyArraysSchema.FieldVector3Array, FieldType.@vector3, true, null) },
                { EnumKeyArraysSchema.FieldVector4Array, new FieldBase(EnumKeyArraysSchema.FieldVector4Array, FieldType.@vector4, true, null) }
            };
        }

        public EnumKeyArrays GetByKey(Days key) { return m_data[key.ToString()] as EnumKeyArrays; }

        public bool TryGetByKey(Days key, out EnumKeyArrays row) { row = null; if (m_data.ContainsKey(key.ToString())) { row = m_data[key.ToString()] as EnumKeyArrays; return row != null; } return false; }

        public Dictionary<Days, EnumKeyArrays> GetRows() { return m_data.ToDictionary(entry => (Days)Enum.Parse(typeof(Days), entry.Key), entry => (EnumKeyArrays)entry.Value); }
    }
}
