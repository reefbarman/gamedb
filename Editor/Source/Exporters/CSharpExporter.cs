using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class CSharpExporter
    {
        internal sealed class ValidationIssue
        {
            public string Code;
            public string Message;
            public string TableName;
            public string FieldName;
            public string RowKey;
        }

        private static readonly Regex CSharpIdentifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex RowKeyWhitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "add", "alias", "and", "as", "ascending", "async", "await", "base", "bool", "break", "by",
            "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "descending", "do", "double", "dynamic", "else", "enum", "equals", "event", "explicit",
            "extern", "false", "file", "finally", "fixed", "float", "for", "foreach", "from", "get", "global",
            "goto", "group", "if", "implicit", "in", "init", "int", "interface", "internal", "into", "is", "join",
            "let", "lock", "long", "managed", "nameof", "namespace", "new", "nint", "not", "notnull", "nuint",
            "null", "object", "on", "operator", "or", "orderby", "out", "override", "params", "partial", "private",
            "protected", "public", "readonly", "record", "ref", "remove", "required", "return", "sbyte", "scoped",
            "sealed", "select", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unmanaged", "unsafe", "ushort", "using",
            "value", "var", "virtual", "void", "volatile", "when", "where", "while", "with", "yield"
        };
        private static readonly HashSet<string> ReservedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

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

        internal void Export(string exportPath, GameDB gameDB, bool unity = true)
        {
            var issues = Validate(gameDB, unity);
            if (issues.Count > 0)
            {
                throw new InvalidOperationException(FormatValidationIssues(issues));
            }

            var exportRoot = Path.IsPathRooted(exportPath)
                ? Path.GetFullPath(exportPath)
                : Path.GetFullPath(Path.Combine(Application.dataPath, exportPath));
            ValidateScopeDirectory(exportRoot, gameDB.ScopeName);
            var exportDirectory = Path.Combine(exportRoot, gameDB.ScopeName);
            var operationId = Guid.NewGuid().ToString("N");
            var stagingDirectory = Path.Combine(exportRoot, $".{gameDB.ScopeName}.{operationId}.staging");
            var backupDirectory = Path.Combine(exportRoot, $".{gameDB.ScopeName}.{operationId}.backup");

            Directory.CreateDirectory(exportRoot);
            Directory.CreateDirectory(stagingDirectory);

            UnityEditor.AssetDatabase.DisallowAutoRefresh();
            try
            {
                GenerateFiles(stagingDirectory, gameDB, unity);
                PreserveMetadata(exportDirectory, stagingDirectory);
                ReplaceDirectory(stagingDirectory, exportDirectory, backupDirectory);
            }
            finally
            {
                DeleteDirectory(stagingDirectory);
                UnityEditor.AssetDatabase.AllowAutoRefresh();
            }
        }

        private void GenerateFiles(string exportDirectory, GameDB gameDB, bool unity)
        {
            var dataTemplate = LoadTemplate("data");
            var schemaTemplate = LoadTemplate("schema");
            var tableTemplate = LoadTemplate("table");
            var tableDefTemplate = LoadTemplate("tableDef");
            var tableAccessorTemplate = LoadTemplate("tableAccessor");
            var gameDBUnityTemplate = LoadTemplate("gameDBUnity");

            var gameDBTemplate = LoadTemplate("gameDB");
            var loadTemplate = LoadTemplate("unityLoad");
            var localizationCode = string.Empty;

            if (gameDB.LocalizationDB)
            {
                loadTemplate = LoadTemplate("unityLocalizationLoad");
                gameDBTemplate = LoadTemplate("gameDBLocalization");
                var languages = gameDB.Tables.Values
                    .SelectMany(table => table.Fields.Keys)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(language => language, StringComparer.Ordinal)
                    .Select(language => $"\"{EscapeStringLiteral(language)}\"");
                localizationCode = string.Format(
                    LoadTemplate("localizationLoad"), string.Join(", ", languages));
            }

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

                foreach (var rowPair in table.Data)
                {
                    var keyName = NormalizeRowKey(rowPair.Key);

                    switch (table.TableKeyType.KeyType)
                    {
                        case KeyType.@enum:
                            var typeName = GetCSharpTypeName(table.TableKeyType.TypeArg as Type);
                            rowKeys += $"        public static readonly {typeName} Key{keyName} = {typeName}.{EscapeIdentifier(rowPair.Key)};\n";
                            break;
                        default:
                            rowKeys += $"        public const string Key{keyName} = \"{EscapeStringLiteral(rowPair.Key)}\";\n";
                            break;
                    }
                }

                tableAccessors += string.Format(tableAccessorTemplate, table.Name);
                tableDefs.Add(string.Format(tableDefTemplate, table.Name));

                var dataClass = string.Format(dataTemplate, "", gameDB.ScopeName,
                                              table.Name, fieldAccessors);
                var schema = string.Format(schemaTemplate, gameDB.ScopeName, table.Name, fieldKeys, rowKeys);

                var keyPrefix = "@";
                var keyType = $"global::GameDBLibrary.KeyType.{keyPrefix}{table.TableKeyType.KeyType}";

                var keyTypeArg = "null";
                var keySystemType = "string";
                var keyAccessor = "key";
                var keyStringToType = "entry.Key";

                switch (table.TableKeyType.KeyType)
                {
                    case KeyType.@enum:
                        var type = GetCSharpTypeName(table.TableKeyType.TypeArg as Type);
                        keyTypeArg = $"typeof({type})";
                        keySystemType = type;
                        keyAccessor = "key.ToString()";
                        keyStringToType = $"({type})global::System.Enum.Parse(typeof({type}), entry.Key)";
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
                extraGameDBCode = string.Format(gameDBUnityTemplate, loadTemplate);
            }

            var gameDBClass = gameDB.LocalizationDB
                ? string.Format(gameDBTemplate, gameDB.ScopeName, tableAccessors,
                    string.Join("\n", tableDefs.ToArray()), extraGameDBCode,
                    localizationCode)
                : string.Format(gameDBTemplate, gameDB.ScopeName, tableAccessors,
                    string.Join("\n", tableDefs.ToArray()), extraGameDBCode);

            File.WriteAllText(Path.Combine(exportDirectory, "GameDB.cs"), gameDBClass);
        }

        private string GenerateLocalizationFieldAccessors(TableBase table, List<string> fieldDefinitions, ref string fieldKeys)
        {
            var baseFieldTemplate = LoadTemplate("localizationDataField");
            var tableFieldTemplate = LoadTemplate("tableField");

            var fields = new SortedDictionary<string, FieldBase>(table.Fields);

            foreach (var fieldPair in fields)
            {
                fieldKeys += string.Format("        public const string Field{0} = \"{0}\";\n", fieldPair.Key);
                fieldDefinitions.Add(string.Format(tableFieldTemplate, table.Name, fieldPair.Key, fieldPair.Value.Type, fieldPair.Value.IsArray.ToString().ToLower(), "null"));
            }

            return string.Format(baseFieldTemplate, table.Name);
        }

        private string GenerateFieldAccesors(GameDB gameDB, TableBase table, bool unity, List<string> fieldDefinitions, ref string fieldKeys)
        {
            var fieldAccessors = string.Empty;

            var baseFieldTemplate = LoadTemplate("dataField");
            var baseFieldAccessorTemplate = LoadTemplate("baseFieldAccessor");
            var arrayFieldAccessorTemplate = LoadTemplate("arrayFieldAccessor");
            var tableFieldTemplate = LoadTemplate("tableField");

            var fields = new SortedDictionary<string, FieldBase>(table.Fields);

            var accessors = new List<Accessor>();

            foreach (var fieldPair in fields)
            {
                var isArray = fieldPair.Value.IsArray;
                var typeArg = "null";

                switch (fieldPair.Value.Type)
                {
                    case FieldType.@string:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.StringAccessor", ReturnType = "string", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@int:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.IntAccessor", ReturnType = "int", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@long:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.LongAccessor", ReturnType = "long", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@float:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.FloatAccessor", ReturnType = "float", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@double:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.DoubleAccessor", ReturnType = "double", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@bool:
                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = "global::GameDBLibrary.BoolAccessor", ReturnType = "bool", FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.@enum:
                        var enumName = GetCSharpTypeName(fieldPair.Value.GetSystemType());
                        typeArg = $"typeof({enumName})";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = $"global::GameDBLibrary.EnumAccessor<{enumName}>", ReturnType = enumName, FieldName = fieldPair.Key, IsArray = isArray });
                        break;
                    case FieldType.tableRef:
                        var tableName = fieldPair.Value.GetTypeArg<string>();
                        var tableKeyType = gameDB.Tables[tableName].TableKeyType;
                        typeArg = $"{tableName}Schema.TableName";

                        var keyType = "string";

                        switch (tableKeyType.KeyType)
                        {
                            case KeyType.@enum:
                                keyType = GetCSharpTypeName(tableKeyType.TypeArg as Type);
                                break;
                        }

                        var type = $"global::GameDBLibrary.TableReferenceAccessor<{keyType}, global::GameDB{gameDB.ScopeName}.{tableName}>";

                        accessors.Add(new Accessor { Name = fieldPair.Key, Type = type, ReturnType = type, FieldName = fieldPair.Key, IsArray = isArray, OptionArg = ", m_gameDB", IgnoreGetter = true });
                        break;
                    case FieldType.color:
                        {
                            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";

                            var typeName = $"{@namespace}{typeof(ColorAccessor).Name}";

                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "global::UnityEngine.Color" : "global::GameDBLibrary.Color", FieldName = fieldPair.Key, IsArray = isArray });
                            break;
                        }
                    case FieldType.vector2:
                        {
                            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";

                            var typeName = $"{@namespace}{typeof(Vector2Accessor).Name}";

                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "global::UnityEngine.Vector2" : "global::GameDBLibrary.Vector2", FieldName = fieldPair.Key, IsArray = isArray });
                            break;
                        }
                    case FieldType.vector3:
                        {
                            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";

                            var typeName = $"{@namespace}{typeof(Vector3Accessor).Name}";

                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "global::UnityEngine.Vector3" : "global::GameDBLibrary.Vector3", FieldName = fieldPair.Key, IsArray = isArray });
                            break;
                        }
                    case FieldType.vector4:
                        {
                            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";

                            var typeName = $"{@namespace}{typeof(Vector4Accessor).Name}";

                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = unity ? "global::UnityEngine.Vector4" : "global::GameDBLibrary.Vector4", FieldName = fieldPair.Key, IsArray = isArray });
                            break;
                        }
                    case FieldType.unityObject:
                        {
                            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";

                            var typeName = $"{@namespace}{typeof(UnityObjectAccessor).Name}";

                            accessors.Add(new Accessor
                            {
                                Name = fieldPair.Key,
                                Type = typeName,
                                ReturnType = "global::GameDBLibrary.UnityObjectReference",
                                FieldName = fieldPair.Key,
                                IsArray = isArray
                            });
                            accessors.Add(new Accessor
                            {
                                Name = fieldPair.Key,
                                Type = typeName,
                                ReturnType = "string",
                                FieldName = $"{fieldPair.Key}Guid",
                                Getter = "GetGuid",
                                IsArray = isArray,
                                NameSuffix = "Guid"
                            });
                            accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = "string", FieldName = $"{fieldPair.Key}Path", Getter = "GetPath", IsArray = isArray, NameSuffix = "Path" });

                            if (unity)
                            {
                                accessors.Add(new Accessor { Name = fieldPair.Key, Type = typeName, ReturnType = "global::UnityEngine.Object", FieldName = $"{fieldPair.Key}Object", Getter = "GetObject", IsArray = isArray, NameSuffix = "Object" });
                            }

                            break;
                        }
                    case FieldType.dictionary:
                        accessors.Add(GenerateDicitonaryAccessor(gameDB, unity, fieldPair.Key, fieldPair.Value, out typeArg));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                fieldKeys += string.Format("        public const string Field{0} = \"{0}\";\n", fieldPair.Key);

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
                    valueGetter = string.Format(arrayFieldAccessorTemplate, $"global::System.Collections.Generic.List<{accessor.ReturnType}>", fieldName, table.Name, accessor.Name, accessor.Type, accessor.OptionArg, getterString);
                }
                else
                {
                    valueGetter = string.Format(baseFieldAccessorTemplate, accessor.Type, fieldName, table.Name, accessor.Name, accessor.OptionArg, getterString);
                }

                fieldAccessors += string.Format(baseFieldTemplate, accessor.IsArray ? $"global::System.Collections.Generic.List<{accessor.ReturnType}>" : accessor.ReturnType, accessor.FieldName, valueGetter);
            }

            return fieldAccessors;
        }

        internal static string LoadTemplate(string name)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CSharpExporter).Assembly);
            if (packageInfo == null)
            {
                throw new InvalidOperationException("Unable to resolve the GameDB package path.");
            }

            var path = Path.Combine(packageInfo.resolvedPath, "Editor", "Templates", $"{name}.tmpl");
            return File.ReadAllText(path);
        }

        private Accessor GenerateDicitonaryAccessor(GameDB gameDB, bool unity, string fieldName, FieldBase field, out string typeArg)
        {
            var dictType = field.GetTypeArg<DictionaryType>();

            var keyType = "string";
            var keyAccessorType = "typeof(global::GameDBLibrary.StringAccessor)";
            string keyTypeArg = "null";

            string valueType = null;
            string valueAccessorType = null;
            string valueTypeArg = "null";

            var @namespace = unity ? "global::GameDBLibraryUnity." : "global::GameDBLibrary.";
            var unityNamespace = unity ? "global::UnityEngine." : "global::GameDBLibrary.";

            switch (dictType.KeyType)
            {
                case KeyType.@enum:
                    var enumType = dictType.GetKeySystemType();

                    keyType = GetCSharpTypeName(enumType);
                    keyTypeArg = $"typeof({keyType})";
                    keyAccessorType = $"typeof(global::GameDBLibrary.EnumAccessor<{keyType}>)";
                    break;
            }

            switch (dictType.ValueType)
            {
                case FieldType.@string:
                    valueType = "string";
                    valueAccessorType = "typeof(global::GameDBLibrary.StringAccessor)";
                    break;
                case FieldType.@int:
                    valueType = "int";
                    valueAccessorType = "typeof(global::GameDBLibrary.IntAccessor)";
                    break;
                case FieldType.@long:
                    valueType = "long";
                    valueAccessorType = "typeof(global::GameDBLibrary.LongAccessor)";
                    break;
                case FieldType.@float:
                    valueType = "float";
                    valueAccessorType = "typeof(global::GameDBLibrary.FloatAccessor)";
                    break;
                case FieldType.@double:
                    valueType = "double";
                    valueAccessorType = "typeof(global::GameDBLibrary.DoubleAccessor)";
                    break;
                case FieldType.@bool:
                    valueType = "bool";
                    valueAccessorType = "typeof(global::GameDBLibrary.BoolAccessor)";
                    break;
                case FieldType.@enum:
                    var enumType = dictType.GetValueSystemType();

                    valueType = GetCSharpTypeName(enumType);
                    valueTypeArg = $"typeof({valueType})";
                    valueAccessorType = $"typeof(global::GameDBLibrary.EnumAccessor<{valueType}>)";
                    break;
                case FieldType.tableRef:
                    var tableName = dictType.ValueTypeArg as string;
                    valueTypeArg = $"{tableName}Schema.TableName";
                    var tableKeyType = gameDB.Tables[tableName].TableKeyType;

                    var tableKeySystemType = "string";

                    switch (tableKeyType.KeyType)
                    {
                        case KeyType.@enum:
                            tableKeySystemType = GetCSharpTypeName(tableKeyType.TypeArg as Type);
                            break;
                    }

                    valueType = $"global::GameDBLibrary.TableReferenceAccessor<{tableKeySystemType}, global::GameDB{gameDB.ScopeName}.{tableName}>";
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

            typeArg = $"new global::GameDBLibrary.DictionaryType(global::GameDBLibrary.KeyType.@{dictType.KeyType}, {keyTypeArg}, global::GameDBLibrary.FieldType.@{dictType.ValueType}, {valueTypeArg})";

            var typeName = $"global::GameDBLibrary.DictionaryAccessor<{keyType}, {valueType}>";
            var returnType = $"global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>";
            var optionalArgs = $", m_gameDB, {keyAccessorType}, {valueAccessorType}";

            return new Accessor { Name = fieldName, Type = typeName, ReturnType = returnType, FieldName = fieldName, OptionArg = optionalArgs };
        }

        internal static List<ValidationIssue> Validate(GameDB gameDB, bool unity = true)
        {
            if (gameDB == null)
            {
                throw new ArgumentNullException(nameof(gameDB));
            }

            var issues = new List<ValidationIssue>();
            ValidateSourceIdentifier(gameDB.ScopeName, "scope.identifier.invalid", "ScopeName", issues);
            if (ReservedFileNames.Contains(gameDB.ScopeName ?? string.Empty))
            {
                issues.Add(CreateIssue("scope.name.reserved", $"ScopeName '{gameDB.ScopeName}' is reserved on Windows."));
            }

            var typeNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var fileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RegisterGeneratedName(typeNames, "GameDB", "database type", "type.name.collision", issues);
            RegisterFileName(fileNames, "GameDB.cs", "database type", issues);

            foreach (var tablePair in gameDB.Tables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var tableName = tablePair.Key;
                var table = tablePair.Value;
                ValidateSourceIdentifier(tableName, "table.identifier.invalid", "Table name", issues, tableName);

                RegisterGeneratedName(typeNames, tableName, $"table '{tableName}' row type", "type.name.collision", issues, tableName);
                RegisterGeneratedName(typeNames, tableName + "Schema", $"table '{tableName}' schema type", "type.name.collision", issues, tableName);
                RegisterGeneratedName(typeNames, tableName + "Table", $"table '{tableName}' table type", "type.name.collision", issues, tableName);
                RegisterFileName(fileNames, tableName + ".cs", $"table '{tableName}' row type", issues, tableName);
                RegisterFileName(fileNames, tableName + "Schema.cs", $"table '{tableName}' schema type", issues, tableName);
                RegisterFileName(fileNames, tableName + "Table.cs", $"table '{tableName}' table type", issues, tableName);

                var rowMembers = new Dictionary<string, string>(StringComparer.Ordinal);
                RegisterGeneratedName(rowMembers, tableName, "containing row type", "member.name.collision", issues, tableName);
                RegisterGeneratedName(rowMembers, "m_gameDB", "generated database field", "member.name.collision", issues, tableName);
                if (gameDB.LocalizationDB)
                {
                    RegisterGeneratedName(rowMembers, "TranslatedVal", "localization accessor", "member.name.collision", issues, tableName);
                    RegisterGeneratedName(rowMembers, "LanguageVal", "localization accessor", "member.name.collision", issues, tableName);
                    RegisterGeneratedName(rowMembers, "ResolvedLanguageVal", "localization accessor", "member.name.collision", issues, tableName);
                }

                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var fieldName = fieldPair.Key;
                    var field = fieldPair.Value;
                    ValidateSourceIdentifier(fieldName, "field.identifier.invalid", "Field name", issues, tableName, fieldName);
                    if (gameDB.LocalizationDB &&
                        (field.Type != FieldType.@string || field.IsArray))
                    {
                        issues.Add(CreateIssue("localization.field.invalid",
                            $"Localization field '{tableName}.{fieldName}' must be a scalar string.",
                            tableName, fieldName));
                    }

                    if (field.Type == FieldType.@enum)
                    {
                        ValidateEnumType(field.GetSystemType(), "field.enumType.invalid",
                            $"Field '{tableName}.{fieldName}' enum", issues, tableName, fieldName);
                    }
                    else if (field.Type == FieldType.tableRef)
                    {
                        ValidateTableReferenceTarget(gameDB, tableName, fieldName, field.GetTypeArg<string>(), issues);
                    }
                    else if (field.Type == FieldType.dictionary)
                    {
                        var dictionaryType = field.GetTypeArg<DictionaryType>();
                        if (dictionaryType != null && dictionaryType.KeyType == KeyType.@enum)
                        {
                            ValidateEnumType(dictionaryType.KeyTypeArg as Type, "field.dictionaryKeyEnumType.invalid",
                                $"Dictionary field '{tableName}.{fieldName}' key enum", issues, tableName, fieldName);
                        }

                        if (dictionaryType != null && dictionaryType.ValueType == FieldType.@enum)
                        {
                            ValidateEnumType(dictionaryType.ValueTypeArg as Type, "field.dictionaryValueEnumType.invalid",
                                $"Dictionary field '{tableName}.{fieldName}' value enum", issues, tableName, fieldName);
                        }
                        else if (dictionaryType != null && dictionaryType.ValueType == FieldType.tableRef)
                        {
                            ValidateTableReferenceTarget(gameDB, tableName, fieldName,
                                dictionaryType.ValueTypeArg as string, issues);
                        }
                    }

                    if (gameDB.LocalizationDB)
                    {
                        continue;
                    }

                    if (fieldPair.Value.Type == FieldType.unityObject)
                    {
                        RegisterGeneratedName(rowMembers, fieldName + "Val", $"field '{fieldName}' value accessor",
                            "member.name.collision", issues, tableName, fieldName);
                        RegisterGeneratedName(rowMembers, fieldName + "GuidVal", $"field '{fieldName}' GUID accessor",
                            "member.name.collision", issues, tableName, fieldName);
                        RegisterGeneratedName(rowMembers, fieldName + "PathVal", $"field '{fieldName}' path accessor",
                            "member.name.collision", issues, tableName, fieldName);
                        if (unity)
                        {
                            RegisterGeneratedName(rowMembers, fieldName + "ObjectVal", $"field '{fieldName}' object accessor",
                                "member.name.collision", issues, tableName, fieldName);
                        }
                    }
                    else
                    {
                        RegisterGeneratedName(rowMembers, fieldName + "Val", $"field '{fieldName}' accessor",
                            "member.name.collision", issues, tableName, fieldName);
                    }
                }

                var schemaMembers = new Dictionary<string, string>(StringComparer.Ordinal);
                RegisterGeneratedName(schemaMembers, tableName + "Schema", "containing schema type",
                    "member.name.collision", issues, tableName);
                RegisterGeneratedName(schemaMembers, "TableName", "table name", "member.name.collision", issues, tableName);
                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    RegisterGeneratedName(schemaMembers, "Field" + fieldPair.Key, $"field '{fieldPair.Key}' schema member",
                        "member.name.collision", issues, tableName, fieldPair.Key);
                }

                Type enumKeyType = null;
                HashSet<string> enumKeyNames = null;
                if (table.TableKeyType.KeyType == KeyType.@enum)
                {
                    enumKeyType = table.TableKeyType.TypeArg as Type;
                    if (ValidateEnumType(enumKeyType, "table.enumType.invalid",
                        $"Table '{tableName}' enum key", issues, tableName))
                    {
                        enumKeyNames = new HashSet<string>(Enum.GetNames(enumKeyType), StringComparer.Ordinal);
                    }
                }

                foreach (var rowPair in table.Data.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var normalizedKey = NormalizeRowKey(rowPair.Key);
                    var generatedKey = "Key" + normalizedKey;
                    if (normalizedKey.Length == 0)
                    {
                        issues.Add(CreateIssue("row.identifier.invalid",
                            $"Row key '{rowPair.Key}' has no identifier characters after whitespace removal.", tableName, rowKey: rowPair.Key));
                    }
                    else if (!IsValidGeneratedIdentifier(generatedKey))
                    {
                        issues.Add(CreateIssue("row.identifier.invalid",
                            $"Row key '{rowPair.Key}' generates invalid C# member '{generatedKey}'.", tableName, rowKey: rowPair.Key));
                    }
                    else
                    {
                        RegisterGeneratedName(schemaMembers, generatedKey, $"row key '{rowPair.Key}'",
                            "row.identifier.collision", issues, tableName, rowKey: rowPair.Key);
                    }

                    if (enumKeyNames != null && !enumKeyNames.Contains(rowPair.Key))
                    {
                        issues.Add(CreateIssue("row.enumMember.invalid",
                            $"Enum row key '{rowPair.Key}' does not exist in {GetCSharpTypeName(enumKeyType)}.", tableName, rowKey: rowPair.Key));
                    }
                }
            }

            return issues;
        }

        internal static string FormatValidationIssues(IEnumerable<ValidationIssue> issues)
        {
            var messages = issues.Select(issue => string.IsNullOrEmpty(issue.Code)
                ? issue.Message
                : $"{issue.Code}: {issue.Message}");
            return "C# generation validation failed:\n" + string.Join("\n", messages);
        }

        private static void ValidateSourceIdentifier(string value, string code, string label, List<ValidationIssue> issues,
            string tableName = null, string fieldName = null)
        {
            if (!IsValidSourceIdentifier(value))
            {
                issues.Add(CreateIssue(code, $"{label} '{value}' is not a valid non-keyword C# identifier.", tableName, fieldName));
            }
        }

        private static bool IsValidSourceIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value) && CSharpIdentifier.IsMatch(value) && !CSharpKeywords.Contains(value);
        }

        private static bool IsValidGeneratedIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value) && CSharpIdentifier.IsMatch(value) && !CSharpKeywords.Contains(value);
        }

        private static bool ValidateEnumType(Type type, string code, string label, List<ValidationIssue> issues,
            string tableName, string fieldName = null)
        {
            if (type == null || !type.IsEnum)
            {
                issues.Add(CreateIssue(code, $"{label} type could not be resolved.", tableName, fieldName));
                return false;
            }

            for (var declaringType = type.DeclaringType; declaringType != null; declaringType = declaringType.DeclaringType)
            {
                if (!declaringType.IsGenericType && !declaringType.ContainsGenericParameters)
                {
                    continue;
                }

                issues.Add(CreateIssue(code,
                    $"{label} type '{type}' is nested in a generic type and cannot be emitted as a C# type reference.",
                    tableName, fieldName));
                return false;
            }

            return true;
        }

        private static void ValidateTableReferenceTarget(GameDB gameDB, string tableName, string fieldName,
            string referencedTableName, List<ValidationIssue> issues)
        {
            if (!gameDB.Tables.ContainsKey(referencedTableName ?? string.Empty))
            {
                issues.Add(CreateIssue("tableRef.table.missing",
                    $"Referenced table does not exist: {referencedTableName}", tableName, fieldName));
            }
        }

        private static void RegisterGeneratedName(Dictionary<string, string> names, string generatedName, string source,
            string code, List<ValidationIssue> issues, string tableName = null, string fieldName = null, string rowKey = null)
        {
            if (names.TryGetValue(generatedName, out var existing))
            {
                issues.Add(CreateIssue(code, $"Generated C# name '{generatedName}' collides between {existing} and {source}.",
                    tableName, fieldName, rowKey));
                return;
            }

            names.Add(generatedName, source);
        }

        private static void RegisterFileName(Dictionary<string, string> names, string fileName, string source,
            List<ValidationIssue> issues, string tableName = null)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (ReservedFileNames.Contains(stem))
            {
                issues.Add(CreateIssue("file.name.reserved", $"Generated filename '{fileName}' is reserved on Windows.", tableName));
            }

            RegisterGeneratedName(names, fileName, source, "file.name.collision", issues, tableName);
        }

        private static ValidationIssue CreateIssue(string code, string message, string tableName = null,
            string fieldName = null, string rowKey = null)
        {
            return new ValidationIssue
            {
                Code = code,
                Message = message,
                TableName = tableName,
                FieldName = fieldName,
                RowKey = rowKey
            };
        }

        private static string NormalizeRowKey(string rowKey)
        {
            return RowKeyWhitespace.Replace(rowKey ?? string.Empty, string.Empty);
        }

        private static string EscapeIdentifier(string identifier)
        {
            return CSharpKeywords.Contains(identifier) ? "@" + identifier : identifier;
        }

        private static string GetCSharpTypeName(Type type)
        {
            if (type == null)
            {
                throw new InvalidOperationException("A generated enum type could not be resolved.");
            }

            return "global::" + string.Join(".", (type.FullName ?? type.Name).Replace("+", ".").Split('.').Select(EscapeIdentifier));
        }

        private static string EscapeStringLiteral(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\0': builder.Append("\\0"); break;
                    case '\a': builder.Append("\\a"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\v': builder.Append("\\v"); break;
                    default:
                        if (char.IsControl(character))
                        {
                            builder.Append($"\\u{(int)character:x4}");
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private static void ValidateScopeDirectory(string exportRoot, string scopeName)
        {
            if (!Directory.Exists(exportRoot))
            {
                return;
            }

            var conflictingDirectory = Directory.EnumerateDirectories(exportRoot)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, scopeName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, scopeName, StringComparison.Ordinal));
            if (conflictingDirectory != null)
            {
                throw new InvalidOperationException(
                    $"Scope output '{scopeName}' conflicts with existing directory '{conflictingDirectory}' on case-insensitive filesystems.");
            }
        }

        private static void PreserveMetadata(string sourceDirectory, string stagingDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            foreach (var stagingFile in Directory.EnumerateFiles(stagingDirectory, "*.cs"))
            {
                var metadataName = Path.GetFileName(stagingFile) + ".meta";
                var sourceMetadata = Path.Combine(sourceDirectory, metadataName);
                if (File.Exists(sourceMetadata))
                {
                    File.Copy(sourceMetadata, Path.Combine(stagingDirectory, metadataName));
                }
            }
        }

        private static void ReplaceDirectory(string stagingDirectory, string destinationDirectory, string backupDirectory)
        {
            var destinationMovedToBackup = false;
            var stagingMovedToDestination = false;
            try
            {
                if (Directory.Exists(destinationDirectory))
                {
                    Directory.Move(destinationDirectory, backupDirectory);
                    destinationMovedToBackup = true;
                }

                Directory.Move(stagingDirectory, destinationDirectory);
                stagingMovedToDestination = true;
                DeleteDirectory(backupDirectory);
            }
            catch
            {
                if (stagingMovedToDestination)
                {
                    DeleteDirectory(destinationDirectory);
                }

                if (destinationMovedToBackup && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, destinationDirectory);
                }

                throw;
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
