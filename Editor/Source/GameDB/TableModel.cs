using GameDBLibrary;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    internal class TableModel : TableBase
    {
        public TableModel(string name)
            : base(name, KeyType.@string, null, (string rowName) => new RowModel(rowName))
        {
        }

        public TableModel(string name, KeyType type, object typeArg)
            : base(name, type, typeArg, (string rowName) => new RowModel(rowName))
        {
        }

        public bool AddField(string fieldName, FieldType type, bool array, object typeArg = null)
        {
            bool success = false;

            if (!m_fields.ContainsKey(fieldName))
            {
                Field field = new Field(fieldName, type, array, typeArg);

                m_fields.Add(fieldName, field);

                foreach (var rowPair in m_data)
                {
                    var row = (RowModel)rowPair.Value;
                    row.SetValue(field.Name, GetDefaultValue(field));
                }

                success = true;
            }

            return success;
        }

        public bool RemoveField(string fieldName)
        {
            if (!m_fields.Remove(fieldName))
            {
                return false;
            }

            foreach (var rowPair in m_data)
            {
                ((RowModel)rowPair.Value).RemoveField(fieldName);
            }

            return true;
        }

        public void RemoveField(Field field)
        {
            RemoveField(field.Name);
        }

        public bool RenameField(string oldName, string newName)
        {
            if (!m_fields.TryGetValue(oldName, out var field) || m_fields.ContainsKey(newName))
            {
                return false;
            }

            m_fields.Remove(oldName);
            ((Field)field).Rename(newName);
            m_fields.Add(newName, field);

            foreach (var rowPair in m_data)
            {
                ((RowModel)rowPair.Value).RenameField(oldName, newName);
            }

            return true;
        }

        public bool ReplaceField(string fieldName, FieldType type, bool array, object typeArg = null)
        {
            if (!m_fields.ContainsKey(fieldName))
            {
                return false;
            }

            var field = new Field(fieldName, type, array, typeArg);
            m_fields[fieldName] = field;

            foreach (var rowPair in m_data)
            {
                ((RowModel)rowPair.Value).SetValue(fieldName, GetDefaultValue(field));
            }

            return true;
        }

        public bool AddKey(string key)
        {
            bool success = string.IsNullOrEmpty(key);

            if (!string.IsNullOrEmpty(key)
                && key != FieldBase.NullRefToken
                && !m_data.ContainsKey(key))
            {
                var row = new RowModel(key);

                foreach (var fieldPair in m_fields)
                {
                    row.SetValue(fieldPair.Value.Name, GetDefaultValue(fieldPair.Value));
                }

                m_data.Add(key, row);
                success = true;
            }

            return success;
        }

        public bool RemoveKey(string key)
        {
            return m_data.Remove(key);
        }

        public bool RenameKey(string oldKey, string newKey)
        {
            if (oldKey == FieldBase.NullRefToken || newKey == FieldBase.NullRefToken
                || !m_data.TryGetValue(oldKey, out var row) || m_data.ContainsKey(newKey))
            {
                return false;
            }

            m_data.Remove(oldKey);
            m_data.Add(newKey, ((RowModel)row).CopyWithName(newKey));
            return true;
        }

        public bool SetValue(string key, string fieldName, object value)
        {
            if (!m_data.TryGetValue(key, out var row) || !m_fields.TryGetValue(fieldName, out var field) || !field.IsValueValid(value))
            {
                return false;
            }

            var rowModel = (RowModel)row;
            rowModel.SetValue(fieldName, rowModel.DeserializeValue(value, field));
            return true;
        }

        public void Rename(string name)
        {
            m_name = name;
        }

        public void DeserializeSchema(object tableSchemaObj)
        {
            var tableSchema = tableSchemaObj as IDictionary<string, object>;

            if (tableSchema == null)
            {
                throw new FormatException("top level table object not a dictionary");
            }

            var fieldDics = tableSchema["fields"] as IDictionary<string, object>;

            if (fieldDics == null)
            {
                throw new FormatException("fields object is not a dictionary");
            }

            var fields = new Dictionary<string, FieldBase>();

            foreach (var fieldPair in fieldDics)
            {
                Field field = new Field(fieldPair.Key);
                field.DeserializeSchema(fieldPair.Value);
                fields.Add(fieldPair.Key, field);
            }

            m_fields = fields;

            //Backwards compatible
            if (tableSchema.ContainsKey("key"))
            {
                if (!(tableSchema["key"] is IDictionary<string, object> keySchema))
                {
                    throw new FormatException("key object is not a dictionary");
                }

                m_tableKey.KeyType = (KeyType)Convert.ChangeType(Enum.Parse(typeof(KeyType), keySchema["type"] as string), typeof(KeyType));

                switch (m_tableKey.KeyType)
                {
                    case KeyType.@enum:
                        m_tableKey.TypeArg = AssemblyExplorer.Instance.GetType(keySchema["typeArg"] as string);
                        break;
                    case KeyType.@string:
                        m_tableKey.TypeArg = null;
                        break;
                }
            }
        }

        public Dictionary<string, object> SerializeSchema()
        {
            var tableSchema = new Dictionary<string, object>();

            foreach (var fieldPair in m_fields)
            {
                var field = (Field)fieldPair.Value;
                tableSchema.Add(fieldPair.Key, field.SerializeSchema());
            }

            return new Dictionary<string, object> {
                { "fields", tableSchema },
                { "key", new Dictionary<string, object>() {
                    { "type", m_tableKey.KeyType.ToString() },
                    { "typeArg", m_tableKey.TypeArg != null ? m_tableKey.TypeArg.ToString() : null }
                } }
            };
        }

        public Dictionary<string, Dictionary<string, object>> SerializeData()
        {
            var tableData = new Dictionary<string, Dictionary<string, object>>();

            foreach (var rowPair in m_data)
            {
                var row = (RowModel)rowPair.Value;
                tableData.Add(rowPair.Key, row.SerializeRow(m_fields));
            }

            return tableData;
        }

        public object GetDefaultValue(FieldBase field)
        {
            object defaultValue = null;

            defaultValue = field.GetDefaultValue(field.IsArray);

            return defaultValue;
        }
    }
}
