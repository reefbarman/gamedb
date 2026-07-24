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

namespace GameDBTestAddRow
{
    public class TestTable : TableBase
    {
        public TestTable(Func<string, RowBase> rowFactory) : base(TestSchema.TableName, KeyType.@string, null, rowFactory) {
            m_fields = new Dictionary<string, FieldBase>() {
                { TestSchema.FieldTest, new FieldBase(TestSchema.FieldTest, FieldType.@string, false, null) }
            };
        }

        public Test GetByKey(string key) { return m_data[key] as Test; }

        public bool TryGetByKey(string key, out Test row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as Test; return row != null; } return false; }

        public Dictionary<string, Test> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (Test)entry.Value); }
    }
}
