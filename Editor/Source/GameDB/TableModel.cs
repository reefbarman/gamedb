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

            if (!MutableFields.ContainsKey(fieldName))
            {
                Field field = new Field(fieldName, type, array, typeArg);

                MutableFields.Add(fieldName, field);

                foreach (var rowPair in MutableData)
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
            if (!MutableFields.Remove(fieldName))
            {
                return false;
            }

            foreach (var rowPair in MutableData)
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
            if (!MutableFields.TryGetValue(oldName, out var field) || MutableFields.ContainsKey(newName))
            {
                return false;
            }

            MutableFields.Remove(oldName);
            ((Field)field).Rename(newName);
            MutableFields.Add(newName, field);

            foreach (var rowPair in MutableData)
            {
                ((RowModel)rowPair.Value).RenameField(oldName, newName);
            }

            return true;
        }

        public bool ReplaceField(string fieldName, FieldType type, bool array, object typeArg = null)
        {
            if (!MutableFields.ContainsKey(fieldName))
            {
                return false;
            }

            var field = new Field(fieldName, type, array, typeArg);
            MutableFields[fieldName] = field;

            foreach (var rowPair in MutableData)
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
                && !MutableData.ContainsKey(key))
            {
                var row = new RowModel(key);

                foreach (var fieldPair in MutableFields)
                {
                    row.SetValue(fieldPair.Value.Name, GetDefaultValue(fieldPair.Value));
                }

                MutableData.Add(key, row);
                success = true;
            }

            return success;
        }

        public bool RemoveKey(string key)
        {
            return MutableData.Remove(key);
        }

        public bool RenameKey(string oldKey, string newKey)
        {
            if (oldKey == FieldBase.NullRefToken || newKey == FieldBase.NullRefToken
                || !MutableData.TryGetValue(oldKey, out var row) || MutableData.ContainsKey(newKey))
            {
                return false;
            }

            MutableData.Remove(oldKey);
            MutableData.Add(newKey, ((RowModel)row).CopyWithName(newKey));
            return true;
        }

        public bool SetValue(string key, string fieldName, object value)
        {
            if (!MutableData.TryGetValue(key, out var row) || !MutableFields.TryGetValue(fieldName, out var field) || !field.IsValueValid(value))
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

            MutableFields = fields;

            if (!tableSchema.TryGetValue("key", out var keySchemaObj)
                || !(keySchemaObj is IDictionary<string, object> keySchema))
            {
                throw new GameDBSchemaFormatException(
                    "Table schema is missing the required 'key' object or it is not a dictionary.");
            }
            if (!keySchema.TryGetValue("type", out var keyTypeObj)
                || !(keyTypeObj is string keyTypeName)
                || !Enum.TryParse(keyTypeName, out KeyType keyType))
            {
                throw new GameDBSchemaFormatException(
                    "Table schema 'key.type' must name a supported key type.");
            }

            m_tableKey.KeyType = keyType;

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

        public Dictionary<string, object> SerializeSchema()
        {
            var tableSchema = new Dictionary<string, object>();

            foreach (var fieldPair in MutableFields)
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

            foreach (var rowPair in MutableData)
            {
                var row = (RowModel)rowPair.Value;
                tableData.Add(rowPair.Key, row.SerializeRow(MutableFields));
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
