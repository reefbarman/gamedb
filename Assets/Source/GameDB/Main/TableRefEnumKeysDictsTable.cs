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
    public class TableRefEnumKeysDictsTable : TableBase
    {
        public TableRefEnumKeysDictsTable(Func<string, RowBase> rowFactory) : base(TableRefEnumKeysDictsSchema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { TableRefEnumKeysDictsSchema.FieldDictStrBool, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrBool, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@bool, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrColor, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrColor, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@color, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrEnum, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrEnum, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@enum, typeof(Rarity))) },
                { TableRefEnumKeysDictsSchema.FieldDictStrFlt, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrFlt, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@float, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrInt, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrInt, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@int, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrStr, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrStr, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@string, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrTableRef, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrTableRef, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@tableRef, EnumKeyArraysSchema.TableName)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrUObj, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrUObj, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@unityObject, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrVec2, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrVec2, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@vector2, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrVec3, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrVec3, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@vector3, null)) },
                { TableRefEnumKeysDictsSchema.FieldDictStrVec4, new FieldBase(TableRefEnumKeysDictsSchema.FieldDictStrVec4, FieldType.@dictionary, false, new DictionaryType(KeyType.@string, null, FieldType.@vector4, null)) }
            };
        }

        public TableRefEnumKeysDicts GetByKey(string key) { return m_data[key] as TableRefEnumKeysDicts; }

        public bool TryGetByKey(string key, out TableRefEnumKeysDicts row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as TableRefEnumKeysDicts; return row != null; } return false; }

        public Dictionary<string, TableRefEnumKeysDicts> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (TableRefEnumKeysDicts)entry.Value); }
    }
}
