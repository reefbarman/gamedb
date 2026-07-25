using GameDBLibrary;
using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    internal class RowModel : RowBase
    {
        public RowModel(string name) : base(name)
        {
        }

        public void SetValue(string fieldName, object value)
        {
            m_data[fieldName] = value;
        }

        public void RemoveField(string fieldName)
        {
            m_data.Remove(fieldName);
        }

        public void RenameField(string oldName, string newName)
        {
            if (!m_data.TryGetValue(oldName, out var value))
            {
                return;
            }

            m_data.Remove(oldName);
            m_data[newName] = value;
        }

        public RowModel CopyWithName(string name)
        {
            var copy = new RowModel(name);
            foreach (var pair in m_data)
            {
                copy.SetValue(pair.Key, pair.Value);
            }

            return copy;
        }

        public Dictionary<string, object> SerializeRow(Dictionary<string, FieldBase> fields)
        {
            var row = new Dictionary<string, object>();

            foreach (var fieldPair in fields)
            {
                if (!m_data.ContainsKey(fieldPair.Key))
                {
                    continue;
                }

                if (fieldPair.Value.Type == FieldType.dictionary)
                {
                    row.Add(fieldPair.Key, DictionaryTypeUtils.SerializeValue(fieldPair.Value.GetTypeArg<DictionaryType>(), m_data[fieldPair.Key]));
                }
                else
                {
                    row.Add(fieldPair.Key, TypeHelpers.SerializeType(fieldPair.Value.Type, fieldPair.Value.IsArray, m_data[fieldPair.Key]));
                }
            }

            return row;
        }
    }
}
