using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class CSharpExporter {
        private struct Accessor
        {
            public string Name;
            public string NameSuffix;
            public string Type;
            public string ReturnType;
            public string FieldName;
            public string Getter;
            public bool IgnoreGetter;
            public bool IsArray;
            public string OptionArg;
        }

        internal void Export(string exportPath, GameDB gameDB, bool unity = true) {
            exportPath = Path.Combine(Application.dataPath, exportPath);

            var exportDirectory = $"{exportPath}/{gameDB.ScopeName}";

            if (!Directory.Exists(exportDirectory)) {
                Directory.CreateDirectory(exportDirectory);
            }

            var dataTemplate = Encoding.UTF8.GetString(Properties.Resources.data);
            var schemaTemplate = Encoding.UTF8.GetString(Properties.Resources.schema);
            var tableTemplate = Encoding.UTF8.GetString(Properties.Resources.table);
            var tableDefTemplate = Encoding.UTF8.GetString(Properties.Resources.tableDef);
            var tableAccessorTemplate = Encoding.UTF8.GetString(Properties.Resources.tableAccessor);
            var gameDBUnityTemplate = Encoding.UTF8.GetString(Properties.Resources.gameDBUnity);

            var gameDBTemplate = Encoding.UTF8.GetString(Properties.Resources.gameDB);
            var loadTemplate = string.Empty;

#if FREE_VERSION
            loadTemplate = Encoding.UTF8.GetString(Properties.Resources.unityLoad);
#else
            if (gameDB.LocalizationDB) {
                loadTemplate = Encoding.UTF8.GetString(Properties.Resources.unityBinaryLocalizationLoad);
                gameDBTemplate = Encoding.UTF8.GetString(Properties.Resources.gameDBLocalization);
            }
            else {
                loadTemplate = Encoding.UTF8.GetString(Properties.Resources.unityBinaryLoad);
            }
#endif

            var tableAccessors = string.Empty;
            var tableDefs = new List<string>();

            foreach (var tablePair in gameDB.Tables)
            {
                var table = tablePair.Value;

                var fieldKeys = string.Empty;
                var fieldDefinitions = new List<string>();

                string fieldAccessors;

                if (gameDB.LocalizationDB)
                {
                    fieldAccessors = GenerateLocalizationFieldAccessors(table, fieldDefinitions, ref fieldKeys);
                }
                else
                {
                    fieldAccessors = GenerateFieldAccesors(gameDB, table, unity, fieldDefinitions, ref fieldKeys);
                }

                var rowKeys = string.Empty;

                foreach (var rowPair in table.Data) {
                    var keyName = Regex.Replace(rowPair.Key, @"\s+", "");

                    switch (table.TableKeyType.KeyType) {
                        case KeyType.@enum:
                            var typeName = table.TableKeyType.TypeArg.ToString().Replace("+", ".");
                            rowKeys += $"        public static {typeName} Key{keyName} = {typeName}.{rowPair.Key};\n";
                            break;
                        default:
                            rowKeys += $"        public static string Key{keyName} = \"{rowPair.Key}\";\n";
                            break;
                    }
                }

                tableAccessors += string.Format(tableAccessorTemplate, table.Name);
                tableDefs.Add(string.Format(tableDefTemplate, table.Name));

                var dataClass = string.Format(dataTemplate, "", gameDB.ScopeName,
                                              table.Name, fieldAccessors);
                var schema = string.Format(schemaTemplate, gameDB.ScopeName, table.Name, fieldKeys, rowKeys);

                var keyPrefix = "@";
                var keyType = $"KeyType.{keyPrefix}{table.TableKeyType.KeyType}";

                var keyTypeArg = "null";
                var keySystemType = "string";
                var keyAccessor = "key";
                var keyStringToType = "entry.Key";

                switch (table.TableKeyType.KeyType) {
                    case KeyType.@enum:
                        var type = table.TableKeyType.TypeArg.ToString().Replace("+", ".");
                        keyTypeArg = $"typeof({type})";
                        keySystemType = type;
                        keyAccessor = "key.ToString()";
                        keyStringToType = $"({type})Enum.Parse(typeof({type}), entry.Key)";
                        break;
                }

                var tableClass = string.Format(tableTemplate, gameDB.ScopeName, table.Name, keyType, keyTypeArg, string.Join(",\n", fieldDefinitions.ToArray()), keySystemType, keyAccessor, keyStringToType);

                File.WriteAllText(Path.Combine(exportDirectory, $"{table.Name}.cs"), dataClass);
                File.WriteAllText(Path.Combine(exportDirectory, $"{table.Name}Schema.cs"), schema);
                File.WriteAllText(Path.Combine(exportDirectory, $"{table.Name}Table.cs"), tableClass);
            }

            var extraGameDBCode = string.Empty;

            if (unity)
            {
                var webRequestRegistration = "";

#if !FREE_VERSION
                webRequestRegistration = @"WebRequestHelper.Request = new GameDBLibraryUnity.WebRequest();
            WebRequestHelper.FormFactory = new GameDBLibraryUnity.FormFactory();";
#endif
                extraGameDBCode = string.Format(gameDBUnityTemplate, webRequestRegistration, loadTemplate);
            }

            var gameDBClass = string.Format(gameDBTemplate, gameDB.ScopeName, tableAccessors, string.Join("\n", tableDefs.ToArray()), extraGameDBCode);

            File.WriteAllText(Path.Combine(exportDirectory, "GameDB.cs"), gameDBClass);
        }

        private string GenerateLocalizationFieldAccessors(TableBase table, List<string> fieldDefinitions, ref string fieldKeys) {
            var baseFieldTemplate = Encoding.UTF8.GetString(Properties.Resources.localizationDataField);
            var baseTypeConvertTemplate = Encoding.UTF8.GetString(Properties.Resources.baseTypeConvert);
            var tableFieldTemplate = Encoding.UTF8.GetString(Properties.Resources.tableField);

            var fields = new SortedDictionary<string, FieldBase>(table.Fields);

            foreach (var fieldPair in fields) {
                fieldKeys += string.Format("        public static string Field{0} = \"{0}\";\n", fieldPair.Key);
                fieldDefinitions.Add(string.Format(tableFieldTemplate, table.Name, fieldPair.Key, fieldPair.Value.Type, fieldPair.Value.IsArray.ToString().ToLower(), "null"));
            }

            var dataFieldAccessor = string.Format(baseTypeConvertTemplate, typeof(string).Name, $"GetValue(m_gameDB.LocalizationLanguage)");
            return string.Format(baseFieldTemplate, "Translated", FieldType.@string.ToString(), dataFieldAccessor, string.Empty);
        }

        private string GenerateFieldAccesors(GameDB gameDB, TableBase table, bool unity, List<string> fieldDefinitions, ref string fieldKeys)
        {
            var fieldAccessors = string.Empty;

            var baseFieldTemplate = Encoding.UTF8.GetString(Properties.Resources.dataField);
            var baseFieldAccessorTemplate = Encoding.UTF8.GetString(Properties.Resources.baseFieldAccessor);
            var arrayFieldAccessorTemplate = Encoding.UTF8.GetString(Properties.Resources.arrayFieldAccessor);
            var tableFieldTemplate = Encoding.UTF8.GetString(Properties.Resources.tableField);

            var fields = new SortedDictionary<string, FieldBase>(table.Fields);

            var accessors = new List<Accessor>();

            foreach (var fieldPair in fields)
            {
                var isArray = fieldPair.Value.IsArray;
                var typeArg = "null";

                switch (fieldPair.Value.Type)
                {
                    case FieldType.@string:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeof(StringAccessor).Name, ReturnType = "string", FieldName = fieldPair.Key, IsArray = isArray});
                        break;
                    case FieldType.@int:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeof(IntAccessor).Name, ReturnType = "int", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@float:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeof(FloatAccessor).Name, ReturnType = "float", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@bool:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeof(BoolAccessor).Name, ReturnType = "bool", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@enum:
                        var enumName = fieldPair.Value.GetSystemType().ToString().Replace("+", ".");
                        typeArg = $"typeof({enumName})";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = $"EnumAccessor<{enumName}>", ReturnType = enumName, FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.tableRef:
                        var tableName = fieldPair.Value.GetTypeArg<string>();
                        var tableKeyType = gameDB.Tables[tableName].TableKeyType;
                        typeArg = $"{tableName}Schema.TableName";

                        var keyType = "string";

                        switch (tableKeyType.KeyType)
                        {
                            case KeyType.@enum:
                                keyType = tableKeyType.TypeArg.ToString();
                                break;
                        }

                        var type = $"TableReferenceAccessor<{keyType}, {tableName}>";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = type, ReturnType = type, FieldName = fieldPair.Key, IsArray = isArray, OptionArg = ", m_gameDB", IgnoreGetter = true});
                        break;
                    case FieldType.color:
                    {
                        var @namespace = unity ? "GameDBLibraryUnity." : "";

                        var typeName = $"{@namespace}{typeof(ColorAccessor).Name}";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "UnityEngine.Color" : "Color", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    }
                    case FieldType.vector2:
                    {
                        var @namespace = unity ? "GameDBLibraryUnity." : "";

                        var typeName = $"{@namespace}{typeof(Vector2Accessor).Name}";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "UnityEngine.Vector2" : "Vector2", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    }
                    case FieldType.vector3:
                    {
                        var @namespace = unity ? "GameDBLibraryUnity." : "";

                        var typeName = $"{@namespace}{typeof(Vector3Accessor).Name}";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "UnityEngine.Vector3" : "Vector3", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    }
                    case FieldType.vector4:
                    {
                        var @namespace = unity ? "GameDBLibraryUnity." : "";

                        var typeName = $"{@namespace}{typeof(Vector4Accessor).Name}";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "UnityEngine.Vector4" : "Vector4", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    }
                    case FieldType.unityObject:
                    {
                        var @namespace = unity ? "GameDBLibraryUnity." : "";

                        var typeName = $"{@namespace}{typeof(UnityObjectAccessor).Name}";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = "string", FieldName = $"{fieldPair.Key}Path", IsArray = isArray, NameSuffix = "Path" });

                        if (unity)
                        {
                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = "UnityEngine.Object", FieldName = $"{fieldPair.Key}Object", Getter = "GetObject", IsArray = isArray, NameSuffix = "Object" });
                        }

                        break;
                    }
                    case FieldType.dictionary:
                        accessors.Add(GenerateDicitonaryAccessor(gameDB, unity, fieldPair.Key, fieldPair.Value, out typeArg));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                fieldKeys += string.Format("        public static string Field{0} = \"{0}\";\n", fieldPair.Key);

                fieldDefinitions.Add(string.Format(tableFieldTemplate, table.Name, fieldPair.Key, fieldPair.Value.Type, fieldPair.Value.IsArray.ToString().ToLower(), typeArg));
            }

            foreach (var accessor in accessors)
            {
                var valueGetter = string.Empty;

                var getterString = accessor.IgnoreGetter ? "" : (!string.IsNullOrEmpty(accessor.Getter) ? $".{accessor.Getter}()" : ".GetValue()");
                var fieldName = $"{table.Name}Schema.Field{accessor.Name}";

                if (!string.IsNullOrEmpty(accessor.NameSuffix))
                {
                    fieldName += $" + \"{accessor.NameSuffix}\"";
                }

                if (accessor.IsArray)
                {
                    valueGetter = string.Format(arrayFieldAccessorTemplate, $"List<{accessor.ReturnType}>", fieldName, table.Name, accessor.Name, accessor.Type, accessor.OptionArg, getterString);
                }
                else
                {
                    valueGetter = string.Format(baseFieldAccessorTemplate, accessor.Type, fieldName, table.Name, accessor.Name, accessor.OptionArg, getterString);
                }

                fieldAccessors += string.Format(baseFieldTemplate, accessor.IsArray ? $"List<{accessor.ReturnType}>" : accessor.ReturnType, accessor.FieldName, valueGetter);
            }

            return fieldAccessors;
        }

        private Accessor GenerateDicitonaryAccessor(GameDB gameDB, bool unity, string fieldName, FieldBase field, out string typeArg)
        {
            var dictType = field.GetTypeArg<DictionaryType>();

            var keyType = "string";
            var keyAccessorType = "typeof(StringAccessor)";
            string keyTypeArg = "null";

            string valueType = null;
            string valueAccessorType = null;
            string valueTypeArg = "null";

            var @namespace = unity ? "GameDBLibraryUnity." : "";
            var unityNamespace = unity ? "UnityEngine." : "";

            switch (dictType.KeyType)
            {
                case KeyType.@enum:
                    var enumType = dictType.GetKeySystemType();

                    keyType = enumType.ToString().Replace("+", ".");
                    keyTypeArg = $"typeof({keyType})";
                    keyAccessorType = $"typeof(EnumAccessor<{keyType}>)";
                    break;
            }

            switch (dictType.ValueType)
            {
                case FieldType.@string:
                    valueType = "string";
                    valueAccessorType = "typeof(StringAccessor)";
                    break;
                case FieldType.@int:
                    valueType = "int";
                    valueAccessorType = "typeof(IntAccessor)";
                    break;
                case FieldType.@float:
                    valueType = "float";
                    valueAccessorType = "typeof(FloatAccessor)";
                    break;
                case FieldType.@bool:
                    valueType = "bool";
                    valueAccessorType = "typeof(BoolAccessor)";
                    break;
                case FieldType.@enum:
                    var enumType = dictType.GetValueSystemType();

                    valueType = enumType.ToString().Replace("+", ".");
                    valueTypeArg = $"typeof({valueType})";
                    valueAccessorType = $"typeof(EnumAccessor<{valueType}>)";
                    break;
                case FieldType.tableRef:
                    var tableName = dictType.ValueTypeArg as string;
                    valueTypeArg = $"{tableName}Schema.TableName";
                    var tableKeyType = gameDB.Tables[tableName].TableKeyType;

                    var tableKeySystemType = "string";

                    switch (tableKeyType.KeyType)
                    {
                        case KeyType.@enum:
                            tableKeySystemType = tableKeyType.TypeArg.ToString();
                            break;
                    }

                    valueType = $"TableReferenceAccessor<{tableKeySystemType}, {tableName}>";
                    valueAccessorType = $"typeof({valueType})";
                    break;
                case FieldType.color:
                    valueType = $"{unityNamespace}Color";
                    valueAccessorType = $"typeof({@namespace}ColorAccessor)";
                    break;
                case FieldType.vector2:
                    valueType = $"{unityNamespace}Vector2";
                    valueAccessorType = $"typeof({@namespace}Vector2Accessor)";
                    break;
                case FieldType.vector3:
                    valueType = $"{unityNamespace}Vector3";
                    valueAccessorType = $"typeof({@namespace}Vector3Accessor)";
                    break;
                case FieldType.vector4:
                    valueType = $"{unityNamespace}Vector4";
                    valueAccessorType = $"typeof({@namespace}Vector4Accessor)";
                    break;
                case FieldType.unityObject:
                    valueType = $"{@namespace}UnityObjectAccessor";
                    valueAccessorType = $"typeof({valueType})";
                    break;
            }

            typeArg = $"new DictionaryType(KeyType.@{dictType.KeyType}, {keyTypeArg}, FieldType.@{dictType.ValueType}, {valueTypeArg})";

            var typeName = $"DictionaryAccessor<{keyType}, {valueType}>";
            var returnType = $"Dictionary<{keyType}, {valueType}>";
            var optionalArgs = $", m_gameDB, {keyAccessorType}, {valueAccessorType}";

            return new Accessor { Name = fieldName, Type = typeName, ReturnType = returnType, FieldName = fieldName, OptionArg = optionalArgs };
        }
    }
}
