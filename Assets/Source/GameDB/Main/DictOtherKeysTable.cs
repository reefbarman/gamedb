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
    public class DictOtherKeysTable : TableBase
    {
        public DictOtherKeysTable(Func<string, RowBase> rowFactory) : base(DictOtherKeysSchema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { DictOtherKeysSchema.FieldDictEnumStr, new FieldBase(DictOtherKeysSchema.FieldDictEnumStr, FieldType.@dictionary, false, new DictionaryType(KeyType.@enum, typeof(Colors), FieldType.@string, null)) }
            };
        }

        public DictOtherKeys GetByKey(string key) { return m_data[key] as DictOtherKeys; }

        public bool TryGetByKey(string key, out DictOtherKeys row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as DictOtherKeys; return row != null; } return false; }

        public Dictionary<string, DictOtherKeys> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (DictOtherKeys)entry.Value); }
    }
}
