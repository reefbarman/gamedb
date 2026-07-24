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
    public class TypeTest2Table : TableBase
    {
        public TypeTest2Table(Func<string, RowBase> rowFactory) : base(TypeTest2Schema.TableName, KeyType.@enum, typeof(Colors), rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { TypeTest2Schema.Fieldbool, new FieldBase(TypeTest2Schema.Fieldbool, FieldType.@bool, true, null) },
                { TypeTest2Schema.Fieldcolor, new FieldBase(TypeTest2Schema.Fieldcolor, FieldType.@color, true, null) },
                { TypeTest2Schema.Fieldenum, new FieldBase(TypeTest2Schema.Fieldenum, FieldType.@enum, true, typeof(Rarity)) },
                { TypeTest2Schema.Fieldfloat, new FieldBase(TypeTest2Schema.Fieldfloat, FieldType.@float, true, null) },
                { TypeTest2Schema.Fieldint, new FieldBase(TypeTest2Schema.Fieldint, FieldType.@int, true, null) },
                { TypeTest2Schema.Fieldobj, new FieldBase(TypeTest2Schema.Fieldobj, FieldType.@unityObject, true, null) },
                { TypeTest2Schema.Fieldstring, new FieldBase(TypeTest2Schema.Fieldstring, FieldType.@string, true, null) },
                { TypeTest2Schema.FieldtableRef, new FieldBase(TypeTest2Schema.FieldtableRef, FieldType.@tableRef, true, "TypeTest1") },
                { TypeTest2Schema.Fieldvec2, new FieldBase(TypeTest2Schema.Fieldvec2, FieldType.@vector2, true, null) },
                { TypeTest2Schema.Fieldvec3, new FieldBase(TypeTest2Schema.Fieldvec3, FieldType.@vector3, true, null) },
                { TypeTest2Schema.Fieldvec4, new FieldBase(TypeTest2Schema.Fieldvec4, FieldType.@vector4, true, null) }
            };
        }

        public TypeTest2 GetByKey(Colors key) { return m_data[key.ToString()] as TypeTest2; }

        public bool TryGetByKey(Colors key, out TypeTest2 row) { row = null; if (m_data.ContainsKey(key.ToString())) { row = m_data[key.ToString()] as TypeTest2; return row != null; } return false; }

        public Dictionary<Colors, TypeTest2> GetRows() { return m_data.ToDictionary(entry => (Colors)Enum.Parse(typeof(Colors), entry.Key), entry => (TypeTest2)entry.Value); }
    }
}
