using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary
{
    /// <summary>
    /// This is the base class for all Rows in a GameDB's tables.
    /// It provides accessors to the values store within.
    /// </summary>
    public class RowBase
    {
        private string m_name = string.Empty;
        protected Dictionary<string, object> m_data = new Dictionary<string, object>();

        public string Name => m_name;
        internal Dictionary<string, object> Data => m_data;

        public RowBase(string name)
        {
            m_name = name;
        }

        /// <summary>
        /// Gets the value for a particular field.
        /// </summary>
        /// <param name="field">The name of the field to retrieve.</param>
        /// <returns>The internally store value.</returns>
        public object GetValue(string field)
        {
            return m_data[field];
        }

        internal void DeserializeRow(Dictionary<string, FieldBase> fields, object rowObj,
            string[] columnImportList = null, bool allowMissingSelectedFields = false)
        {
            var row = new Dictionary<string, object>();

            if (!(rowObj is IDictionary<string, object> rowDic))
            {
                throw new FormatException("row level object not a dictionary");
            }

            foreach (var fieldPair in fields)
            {
                if (columnImportList == null || columnImportList.Contains(fieldPair.Key))
                {
                    if (!rowDic.ContainsKey(fieldPair.Key))
                    {
                        if (allowMissingSelectedFields)
                        {
                            continue;
                        }

                        throw new FormatException("row missing field: " + fieldPair.Key);
                    }

                    if (!fieldPair.Value.IsValueValid(rowDic[fieldPair.Key]))
                    {
                        throw new FormatException(fieldPair.Key + " field not of expected type " + fieldPair.Value.Type);
                    }

                    row.Add(fieldPair.Key, DeserializeValue(rowDic[fieldPair.Key], fieldPair.Value));
                }
            }

            m_data = row;
        }

        internal object DeserializeValue(object val, FieldBase field)
        {
            if (field.Type == FieldType.dictionary)
            {
                return field.GetTypeArg<DictionaryType>().DeserializeValue(val);
            }

            return TypeUtils.DeserializeValue(field.Type, field.IsArray, field.GetSystemType(), val);
        }

        internal void Import(Dictionary<string, FieldBase> fields, RowBase row)
        {
            var rowData = new Dictionary<string, object>();

            foreach (var fieldPair in fields)
            {
                if (!row.Data.ContainsKey(fieldPair.Key))
                {
                    throw new FormatException("row missing field: " + fieldPair.Key);
                }

                rowData.Add(fieldPair.Key, row.Data[fieldPair.Key]);
            }

            m_data = rowData;
        }
    }
}
