using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Dictionary<string, object> m_data = new Dictionary<string, object>();
        private RuntimeGameDBSnapshot m_publication;

        public string Name => m_name;
        internal Dictionary<string, object> Data => m_data;
        internal Dictionary<string, object> MutableData => m_data;

        protected internal RowBase(string name)
        {
            m_name = name;
        }

        /// <summary>
        /// Gets the value for a particular field.
        /// </summary>
        /// <param name="field">The name of the field to retrieve.</param>
        /// <returns>The internally store value.</returns>
        protected internal object GetValue(string field)
        {
            return m_data[field];
        }

        protected bool HasValue(string field)
        {
            return m_data.ContainsKey(field);
        }

        protected T GetPublicationMetadata<T>() where T : class
        {
            if (m_publication == null)
            {
                throw new InvalidOperationException(
                    $"Row '{Name}' is not bound to a GameDB publication.");
            }

            return m_publication.Metadata as T;
        }

        internal void BindPublication(RuntimeGameDBSnapshot publication)
        {
            if (publication == null)
            {
                throw new ArgumentNullException(nameof(publication));
            }

            if (m_publication != null)
            {
                throw new InvalidOperationException(
                    $"Row '{Name}' is already bound to a GameDB publication.");
            }

            var frozen = new Dictionary<string, object>();
            foreach (var field in m_data)
            {
                frozen.Add(field.Key, FreezeValue(field.Value));
            }

            m_data = frozen;
            m_publication = publication;
        }

        private static object FreezeValue(object value)
        {
            if (value is IList<object> list)
            {
                var frozen = list.Select(FreezeValue).ToList();
                return new ReadOnlyCollection<object>(frozen);
            }

            if (value is IDictionary<object, object> dictionary)
            {
                var frozen = new Dictionary<object, object>();
                foreach (var item in dictionary)
                {
                    frozen.Add(item.Key, FreezeValue(item.Value));
                }

                return new ReadOnlyDictionary<object, object>(frozen);
            }

            if (value is Color color)
            {
                return new Color(color.r, color.g, color.b, color.a);
            }

            if (value is Vector2 vector2)
            {
                return new Vector2(vector2.x, vector2.y);
            }

            if (value is Vector3 vector3)
            {
                return new Vector3(vector3.x, vector3.y, vector3.z);
            }

            if (value is Vector4 vector4)
            {
                return new Vector4(vector4.x, vector4.y, vector4.z, vector4.w);
            }

            return value;
        }

        internal T ResolveReference<T>(string tableName, string key)
        {
            if (m_publication == null)
            {
                throw new InvalidOperationException(
                    $"Row '{Name}' is not bound to a GameDB publication.");
            }

            return (T)(object)m_publication.ResolveRow(tableName, key);
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

                rowData.Add(fieldPair.Key, DetachValue(row.Data[fieldPair.Key]));
            }

            m_data = rowData;
        }

        private static object DetachValue(object value)
        {
            if (value is IEnumerable<object> list && !(value is string))
            {
                return list.Select(DetachValue).ToList();
            }

            if (value is IEnumerable<KeyValuePair<object, object>> dictionary)
            {
                var result = new Dictionary<object, object>();
                foreach (var item in dictionary)
                {
                    result.Add(item.Key, DetachValue(item.Value));
                }
                return result;
            }

            if (value is Color color)
            {
                return new Color(color.r, color.g, color.b, color.a);
            }

            if (value is Vector2 vector2)
            {
                return new Vector2(vector2.x, vector2.y);
            }

            if (value is Vector3 vector3)
            {
                return new Vector3(vector3.x, vector3.y, vector3.z);
            }

            if (value is Vector4 vector4)
            {
                return new Vector4(vector4.x, vector4.y, vector4.z, vector4.w);
            }

            return value;
        }
    }
}
