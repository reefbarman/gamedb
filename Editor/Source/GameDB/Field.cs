using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBEditorLibrary
{
    internal class Field : FieldBase
    {
        public Field(string name) : base(name) { }

        public Field(string name, FieldType type, bool array, object typeArg = null) : base(name, type, array, typeArg) { }

        public void Rename(string name)
        {
            m_name = name;
        }

        public void SetTypeArgument(object typeArgument)
        {
            m_typeArg = typeArgument;
        }

        public void DeserializeSchema(object schemaObj)
        {
            if (!(schemaObj is IDictionary<string, object> schemaDic))
            {
                throw new FormatException("field schema object is not a dictionary");
            }

            var type = schemaDic["type"] as string;

            m_type = (FieldType)Enum.Parse(typeof(FieldType), type);

            m_array = (bool)schemaDic["isArray"];

            switch (m_type)
            {
                case FieldType.@enum:
                    var typeArgStr = schemaDic["typeArg"] as string;
                    m_typeArg = AssemblyExplorer.Instance.GetType(typeArgStr);

                    if (m_typeArg == null)
                    {
                        throw new FormatException("can't find enum type: " + typeArgStr);
                    }

                    break;
                case FieldType.tableRef:
                    m_typeArg = schemaDic["typeArg"] as string;

                    if (m_typeArg == null)
                    {
                        throw new FormatException("can't find tableRef type: " + m_typeArg);
                    }

                    break;
                case FieldType.dictionary:
                    m_typeArg = DictionaryTypeUtils.Deserialize(schemaDic["typeArg"]);

                    break;
            }
        }

        public Dictionary<string, object> SerializeSchema()
        {
            object typeArg = null;

            List<string> validValues = null;

            switch (m_type)
            {
                case FieldType.@enum:
                    validValues = Enum.GetNames(GetSystemType()).ToList();
                    typeArg = m_typeArg.ToString();
                    break;
                case FieldType.tableRef:
                    typeArg = m_typeArg.ToString();
                    break;
                case FieldType.dictionary:
                    typeArg = DictionaryTypeUtils.Serialize((DictionaryType)m_typeArg);
                    break;
            }

            var schemaDic = new Dictionary<string, object> {
                {"type", m_type.ToString()},
                {"isArray", m_array},
                {"typeArg", typeArg}
            };

            if (validValues != null)
            {
                schemaDic.Add("validValues", validValues);
            }

            return schemaDic;
        }

        public bool IsComplex()
        {
            switch (Type)
            {
                case FieldType.@string:
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                case FieldType.dictionary:
                    return true;
                default:
                    return false;
            }
        }
    }
}
