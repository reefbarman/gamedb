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
    public class StringKeySingleTable : TableBase
    {
        public StringKeySingleTable(Func<string, RowBase> rowFactory) : base(StringKeySingleSchema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { StringKeySingleSchema.FieldBool, new FieldBase(StringKeySingleSchema.FieldBool, FieldType.@bool, false, null) },
                { StringKeySingleSchema.FieldColor, new FieldBase(StringKeySingleSchema.FieldColor, FieldType.@color, false, null) },
                { StringKeySingleSchema.FieldEnum, new FieldBase(StringKeySingleSchema.FieldEnum, FieldType.@enum, false, typeof(Days)) },
                { StringKeySingleSchema.FieldFloat, new FieldBase(StringKeySingleSchema.FieldFloat, FieldType.@float, false, null) },
                { StringKeySingleSchema.FieldInt, new FieldBase(StringKeySingleSchema.FieldInt, FieldType.@int, false, null) },
                { StringKeySingleSchema.FieldString, new FieldBase(StringKeySingleSchema.FieldString, FieldType.@string, false, null) },
                { StringKeySingleSchema.FieldTableRef, new FieldBase(StringKeySingleSchema.FieldTableRef, FieldType.@tableRef, false, EnumKeyArraysSchema.TableName) },
                { StringKeySingleSchema.FieldUnityObject, new FieldBase(StringKeySingleSchema.FieldUnityObject, FieldType.@unityObject, false, null) },
                { StringKeySingleSchema.FieldVector2, new FieldBase(StringKeySingleSchema.FieldVector2, FieldType.@vector2, false, null) },
                { StringKeySingleSchema.FieldVector3, new FieldBase(StringKeySingleSchema.FieldVector3, FieldType.@vector3, false, null) },
                { StringKeySingleSchema.FieldVector4, new FieldBase(StringKeySingleSchema.FieldVector4, FieldType.@vector4, false, null) }
            };
        }

        public StringKeySingle GetByKey(string key) { return m_data[key] as StringKeySingle; }

        public bool TryGetByKey(string key, out StringKeySingle row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as StringKeySingle; return row != null; } return false; }

        public Dictionary<string, StringKeySingle> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (StringKeySingle)entry.Value); }
    }
}
