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

namespace GameDBLocalization
{
    public class LocalizationTable : TableBase
    {
        public LocalizationTable(Func<string, RowBase> rowFactory) : base(LocalizationSchema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { LocalizationSchema.FieldEN, new FieldBase(LocalizationSchema.FieldEN, FieldType.@string, false, null) },
                { LocalizationSchema.FieldIT, new FieldBase(LocalizationSchema.FieldIT, FieldType.@string, false, null) }
            };
        }

        public Localization GetByKey(string key) { return m_data[key] as Localization; }

        public bool TryGetByKey(string key, out Localization row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as Localization; return row != null; } return false; }

        public Dictionary<string, Localization> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (Localization)entry.Value); }
    }
}
