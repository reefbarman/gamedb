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

namespace GameDBTypeTest
{
    public class TypeTest1Table : TableBase
    {
        public TypeTest1Table(Func<string, RowBase> rowFactory) : base(TypeTest1Schema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { TypeTest1Schema.Fieldbool, new FieldBase(TypeTest1Schema.Fieldbool, FieldType.@bool, false, null) },
                { TypeTest1Schema.Fieldcolor, new FieldBase(TypeTest1Schema.Fieldcolor, FieldType.@color, false, null) },
                { TypeTest1Schema.Fieldenum, new FieldBase(TypeTest1Schema.Fieldenum, FieldType.@enum, false, typeof(Days)) },
                { TypeTest1Schema.Fieldfloat, new FieldBase(TypeTest1Schema.Fieldfloat, FieldType.@float, false, null) },
                { TypeTest1Schema.Fieldint, new FieldBase(TypeTest1Schema.Fieldint, FieldType.@int, false, null) },
                { TypeTest1Schema.Fieldobj, new FieldBase(TypeTest1Schema.Fieldobj, FieldType.@unityObject, false, null) },
                { TypeTest1Schema.Fieldstring, new FieldBase(TypeTest1Schema.Fieldstring, FieldType.@string, false, null) },
                { TypeTest1Schema.FieldtableRef, new FieldBase(TypeTest1Schema.FieldtableRef, FieldType.@tableRef, false, "TypeTest1") },
                { TypeTest1Schema.Fieldvec2, new FieldBase(TypeTest1Schema.Fieldvec2, FieldType.@vector2, false, null) },
                { TypeTest1Schema.Fieldvec3, new FieldBase(TypeTest1Schema.Fieldvec3, FieldType.@vector3, false, null) },
                { TypeTest1Schema.Fieldvec4, new FieldBase(TypeTest1Schema.Fieldvec4, FieldType.@vector4, false, null) }
            };
        }

        public TypeTest1 GetByKey(string key) { return m_data[key] as TypeTest1; }

        public bool TryGetByKey(string key, out TypeTest1 row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as TypeTest1; return row != null; } return false; }

        public Dictionary<string, TypeTest1> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (TypeTest1)entry.Value); }
    }
}
