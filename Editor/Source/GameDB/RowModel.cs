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
            MutableData[fieldName] = value;
        }

        public void RemoveField(string fieldName)
        {
            MutableData.Remove(fieldName);
        }

        public void RenameField(string oldName, string newName)
        {
            if (!MutableData.TryGetValue(oldName, out var value))
            {
                return;
            }

            MutableData.Remove(oldName);
            MutableData[newName] = value;
        }

        public RowModel CopyWithName(string name)
        {
            var copy = new RowModel(name);
            foreach (var pair in MutableData)
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
                if (!MutableData.ContainsKey(fieldPair.Key))
                {
                    continue;
                }

                if (fieldPair.Value.Type == FieldType.dictionary)
                {
                    row.Add(fieldPair.Key, DictionaryTypeUtils.SerializeValue(fieldPair.Value.GetTypeArg<DictionaryType>(), MutableData[fieldPair.Key]));
                }
                else
                {
                    row.Add(fieldPair.Key, TypeHelpers.SerializeType(fieldPair.Value.Type, fieldPair.Value.IsArray, MutableData[fieldPair.Key]));
                }
            }

            return row;
        }
    }
}
