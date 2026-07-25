using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GameDBEditorLibrary.Documents
{
    internal sealed class GameDBDictionaryTypeSpec
    {
        internal KeyType KeyType { get; }
        internal string KeyTypeArgument { get; }
        internal FieldType ValueType { get; }
        internal string ValueTypeArgument { get; }

        internal GameDBDictionaryTypeSpec(KeyType keyType, string keyTypeArgument,
            FieldType valueType, string valueTypeArgument)
        {
            KeyType = keyType;
            KeyTypeArgument = keyTypeArgument;
            ValueType = valueType;
            ValueTypeArgument = valueTypeArgument;
        }
    }

    internal sealed class GameDBFieldTypeSpec
    {
        internal FieldType FieldType { get; }
        internal bool IsArray { get; }
        internal string TypeArgument { get; }
        internal GameDBDictionaryTypeSpec DictionaryType { get; }

        internal GameDBFieldTypeSpec(FieldType fieldType, bool isArray, string typeArgument,
            GameDBDictionaryTypeSpec dictionaryType = null)
        {
            FieldType = fieldType;
            IsArray = isArray;
            TypeArgument = typeArgument;
            DictionaryType = dictionaryType;
        }
    }

    internal static class GameDBModelOperations
    {
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;

        internal static void RequireName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }
        }

        internal static void RequireRowKey(string value, string parameterName)
        {
            RequireName(value, parameterName);
            if (value == FieldBase.NullRefToken)
            {
                throw new ArgumentException($"{FieldBase.NullRefToken} is reserved for null table references.",
                    parameterName);
            }
        }

        internal static TableModel GetTable(GameDB gameDB, string tableName)
        {
            RequireName(tableName, nameof(tableName));
            if (!gameDB.Tables.TryGetValue(tableName, out var table))
            {
                throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Table does not exist.");
            }

            return (TableModel)table;
        }

        internal static string ValidateValues(TableModel table, IDictionary<string, object> values)
        {
            foreach (var pair in values)
            {
                if (!table.Fields.TryGetValue(pair.Key, out var field))
                {
                    return $"Field does not exist: {pair.Key}";
                }

                if (!IsWireValueValid(field, pair.Value))
                {
                    return $"Value is invalid for {pair.Key}; expected {field.Type}{(field.IsArray ? "[]" : string.Empty)}.";
                }
            }

            return null;
        }

        internal static bool IsWireValueValid(FieldBase field, object value)
        {
            if (field.Type == FieldType.dictionary)
            {
                return !field.IsArray && IsWireDictionaryValueValid(field.GetTypeArg<DictionaryType>(), value);
            }

            if (field.IsArray)
            {
                if (!(value is List<object> values))
                {
                    return false;
                }

                return values.All(item => IsWireScalarValueValid(field.Type, field.GetTypeArg<object>(), item));
            }

            return IsWireScalarValueValid(field.Type, field.GetTypeArg<object>(), value);
        }

        internal static object ResolveFieldTypeArgument(GameDB gameDB, GameDBFieldTypeSpec spec)
        {
            if (spec.FieldType == FieldType.dictionary)
            {
                if (spec.IsArray)
                {
                    throw new ArgumentException("Dictionary fields cannot be arrays.");
                }

                if (spec.DictionaryType == null)
                {
                    throw new ArgumentException("DictionaryType is required for dictionary fields.");
                }

                if (spec.DictionaryType.ValueType == FieldType.dictionary)
                {
                    throw new ArgumentException("Nested dictionary fields are not supported.");
                }

                var keyArgument = ResolveKeyTypeArgument(spec.DictionaryType.KeyType, spec.DictionaryType.KeyTypeArgument);
                var valueArgument = ResolveSimpleFieldTypeArgument(gameDB,
                    spec.DictionaryType.ValueType, spec.DictionaryType.ValueTypeArgument);
                return new DictionaryType(spec.DictionaryType.KeyType, keyArgument,
                    spec.DictionaryType.ValueType, valueArgument);
            }

            return ResolveSimpleFieldTypeArgument(gameDB, spec.FieldType, spec.TypeArgument);
        }

        internal static object ResolveKeyTypeArgument(KeyType type, string typeArgument)
        {
            if (type == KeyType.@enum)
            {
                return ResolveEnumType(typeArgument);
            }

            if (!string.IsNullOrWhiteSpace(typeArgument))
            {
                throw new ArgumentException("String keys do not accept a type argument.");
            }

            return null;
        }

        internal static List<GameDBValidationIssue> Validate(GameDB gameDB)
        {
            var issues = new List<GameDBValidationIssue>();
            if (string.IsNullOrWhiteSpace(gameDB.ScopeName))
            {
                issues.Add(Issue("scope.empty", "ScopeName is required."));
            }

            foreach (var tablePair in gameDB.Tables.OrderBy(pair => pair.Key, NameComparer))
            {
                var table = (TableModel)tablePair.Value;
                if (string.IsNullOrWhiteSpace(tablePair.Key))
                {
                    issues.Add(Issue("table.name.empty", "Table name is required.", tablePair.Key));
                }

                if (table.Data.ContainsKey(FieldBase.NullRefToken))
                {
                    issues.Add(Issue("row.key.reserved",
                        $"{FieldBase.NullRefToken} is reserved for null table references.",
                        tablePair.Key, rowKey: FieldBase.NullRefToken));
                }

                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, NameComparer))
                {
                    var field = fieldPair.Value;
                    if (string.IsNullOrWhiteSpace(fieldPair.Key))
                    {
                        issues.Add(Issue("field.name.empty", "Field name is required.", tablePair.Key, fieldPair.Key));
                    }

                    foreach (var rowPair in table.Data.OrderBy(pair => pair.Key, NameComparer))
                    {
                        if (!rowPair.Value.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            issues.Add(Issue("field.value.missing", "Row is missing the field value.",
                                tablePair.Key, fieldPair.Key, rowPair.Key));
                        }
                        else if (!IsStoredValueValid(field, value))
                        {
                            issues.Add(Issue("field.value.invalid",
                                $"Value is invalid for {field.Type}{(field.IsArray ? "[]" : string.Empty)}.",
                                tablePair.Key, fieldPair.Key, rowPair.Key));
                        }
                    }

                    ValidateFieldReferences(gameDB, tablePair.Key, fieldPair.Key, field, issues);
                }
            }

            return issues;
        }

        internal static void RenameTableReferences(GameDB gameDB, string oldName, string newName)
        {
            foreach (var table in gameDB.Tables.Values.Cast<TableModel>())
            {
                foreach (var field in table.Fields.Values.Cast<Field>())
                {
                    if (field.Type == FieldType.tableRef && field.GetTypeArg<string>() == oldName)
                    {
                        field.SetTypeArgument(newName);
                    }
                    else if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        if (dictionary.ValueType == FieldType.tableRef && (string)dictionary.ValueTypeArg == oldName)
                        {
                            field.SetTypeArgument(new DictionaryType(dictionary.KeyType, dictionary.KeyTypeArg,
                                dictionary.ValueType, newName));
                        }
                    }
                }
            }
        }

        internal static void RenameRowReferences(GameDB gameDB, string referencedTableName,
            string oldKey, string newKey)
        {
            if (oldKey == FieldBase.NullRefToken || newKey == FieldBase.NullRefToken)
            {
                return;
            }

            foreach (var table in gameDB.Tables.Values.Cast<TableModel>())
            {
                foreach (var fieldPair in table.Fields)
                {
                    var field = fieldPair.Value;
                    var directReference = field.Type == FieldType.tableRef
                        && field.GetTypeArg<string>() == referencedTableName;
                    var dictionaryReference = field.Type == FieldType.dictionary
                        && field.GetTypeArg<DictionaryType>().ValueType == FieldType.tableRef
                        && (string)field.GetTypeArg<DictionaryType>().ValueTypeArg == referencedTableName;
                    if (!directReference && !dictionaryReference)
                    {
                        continue;
                    }

                    foreach (var row in table.Data.Values.Cast<RowModel>())
                    {
                        if (!row.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            continue;
                        }

                        if (dictionaryReference && value is IDictionary dictionary)
                        {
                            var keysToUpdate = new List<object>();
                            foreach (DictionaryEntry entry in dictionary)
                            {
                                if (Equals(entry.Value, oldKey))
                                {
                                    keysToUpdate.Add(entry.Key);
                                }
                            }

                            foreach (var key in keysToUpdate)
                            {
                                dictionary[key] = newKey;
                            }
                        }
                        else if (field.IsArray && value is IList values)
                        {
                            for (var index = 0; index < values.Count; index++)
                            {
                                if (Equals(values[index], oldKey))
                                {
                                    values[index] = newKey;
                                }
                            }
                        }
                        else if (Equals(value, oldKey))
                        {
                            row.SetValue(fieldPair.Key, newKey);
                        }
                    }
                }
            }
        }

        internal static List<string> FindTableReferences(GameDB gameDB, string tableName)
        {
            var references = new List<string>();
            foreach (var tablePair in gameDB.Tables)
            {
                foreach (var fieldPair in tablePair.Value.Fields)
                {
                    var field = fieldPair.Value;
                    if (field.Type == FieldType.tableRef && field.GetTypeArg<string>() == tableName)
                    {
                        references.Add($"{tablePair.Key}.{fieldPair.Key}");
                    }
                    else if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        if (dictionary.ValueType == FieldType.tableRef
                            && (string)dictionary.ValueTypeArg == tableName)
                        {
                            references.Add($"{tablePair.Key}.{fieldPair.Key}");
                        }
                    }
                }
            }

            return references;
        }

        internal static List<string> FindRowReferences(GameDB gameDB, string tableName, string rowKey)
        {
            var references = new List<string>();
            if (rowKey == FieldBase.NullRefToken)
            {
                return references;
            }
            foreach (var tablePair in gameDB.Tables)
            {
                var table = (TableModel)tablePair.Value;
                foreach (var fieldPair in table.Fields)
                {
                    var field = fieldPair.Value;
                    var directReference = field.Type == FieldType.tableRef
                        && field.GetTypeArg<string>() == tableName;
                    var dictionaryReference = field.Type == FieldType.dictionary
                        && field.GetTypeArg<DictionaryType>().ValueType == FieldType.tableRef
                        && (string)field.GetTypeArg<DictionaryType>().ValueTypeArg == tableName;
                    if (!directReference && !dictionaryReference)
                    {
                        continue;
                    }

                    foreach (var dataPair in table.Data)
                    {
                        if (!dataPair.Value.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            continue;
                        }

                        var found = dictionaryReference && value is IDictionary dictionary
                            ? dictionary.Values.Cast<object>().Any(item => Equals(item, rowKey))
                            : field.IsArray && value is IEnumerable values && !(value is string)
                                ? values.Cast<object>().Any(item => Equals(item, rowKey))
                                : Equals(value, rowKey);
                        if (found)
                        {
                            references.Add($"{tablePair.Key}[{dataPair.Key}].{fieldPair.Key}");
                        }
                    }
                }
            }

            return references;
        }

        internal static Dictionary<string, object> CopyWireValues(IDictionary<string, object> values)
        {
            var copy = new Dictionary<string, object>();
            if (values == null)
            {
                return copy;
            }

            foreach (var pair in values)
            {
                copy.Add(pair.Key, CopyWireValue(pair.Value));
            }

            return copy;
        }

        internal static object CopyWireValue(object value)
        {
            if (value is List<object> list)
            {
                return list.Select(CopyWireValue).ToList();
            }

            if (value is IDictionary<string, object> dictionary)
            {
                return dictionary.ToDictionary(pair => pair.Key, pair => CopyWireValue(pair.Value));
            }

            return value;
        }

        private static object ResolveSimpleFieldTypeArgument(GameDB gameDB, FieldType type, string typeArgument)
        {
            switch (type)
            {
                case FieldType.@enum:
                    return ResolveEnumType(typeArgument);
                case FieldType.tableRef:
                    RequireName(typeArgument, nameof(typeArgument));
                    if (!gameDB.Tables.ContainsKey(typeArgument))
                    {
                        throw new ArgumentOutOfRangeException(nameof(typeArgument), typeArgument,
                            "Referenced table does not exist.");
                    }
                    return typeArgument;
                case FieldType.dictionary:
                    throw new ArgumentException("Use DictionaryType to describe dictionary fields.");
                default:
                    if (!string.IsNullOrWhiteSpace(typeArgument))
                    {
                        throw new ArgumentException($"{type} fields do not accept a type argument.");
                    }
                    return null;
            }
        }

        private static Type ResolveEnumType(string typeName)
        {
            RequireName(typeName, nameof(typeName));
            AssemblyExplorer.Instance.Load();
            var type = AssemblyExplorer.Instance.GetType(typeName);
            if (type == null || !type.IsEnum)
            {
                throw new ArgumentException($"Public project enum type was not found: {typeName}");
            }

            return type;
        }

        private static bool IsStoredValueValid(FieldBase field, object value)
        {
            if (field.Type == FieldType.dictionary)
            {
                return !field.IsArray && IsStoredDictionaryValueValid(field.GetTypeArg<DictionaryType>(), value);
            }

            if (field.IsArray)
            {
                if (!(value is IEnumerable values) || value is string || value is IDictionary)
                {
                    return false;
                }

                return values.Cast<object>().All(item =>
                    IsStoredScalarValueValid(field.Type, field.GetTypeArg<object>(), item));
            }

            return IsStoredScalarValueValid(field.Type, field.GetTypeArg<object>(), value);
        }

        private static bool IsWireDictionaryValueValid(DictionaryType dictionaryType, object value)
        {
            if (!(value is IDictionary<string, object> dictionary))
            {
                return false;
            }

            return dictionary.All(entry => IsWireDictionaryKeyValid(dictionaryType, entry.Key)
                && IsWireScalarValueValid(dictionaryType.ValueType, dictionaryType.ValueTypeArg, entry.Value));
        }

        private static bool IsStoredDictionaryValueValid(DictionaryType dictionaryType, object value)
        {
            if (!(value is IDictionary dictionary))
            {
                return false;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (!IsStoredDictionaryKeyValid(dictionaryType, entry.Key)
                    || !IsStoredScalarValueValid(dictionaryType.ValueType, dictionaryType.ValueTypeArg, entry.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWireDictionaryKeyValid(DictionaryType dictionaryType, string value)
        {
            return dictionaryType.KeyType == KeyType.@string
                || dictionaryType.KeyType == KeyType.@enum
                && IsWireEnumValueValid((Type)dictionaryType.KeyTypeArg, value);
        }

        private static bool IsStoredDictionaryKeyValid(DictionaryType dictionaryType, object value)
        {
            return dictionaryType.KeyType == KeyType.@string && value is string
                || dictionaryType.KeyType == KeyType.@enum
                && IsStoredEnumValueValid((Type)dictionaryType.KeyTypeArg, value);
        }

        private static bool IsWireScalarValueValid(FieldType type, object typeArgument, object value)
        {
            if (value == null)
            {
                return type == FieldType.tableRef;
            }

            switch (type)
            {
                case FieldType.@bool:
                    return value is bool;
                case FieldType.@int:
                    return IsInt32Value(value);
                case FieldType.@float:
                    return IsFiniteNumber(value);
                case FieldType.@string:
                case FieldType.tableRef:
                case FieldType.unityObject:
                case FieldType.color:
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                    return value is string;
                case FieldType.@enum:
                    return value is string name && IsWireEnumValueValid((Type)typeArgument, name);
                default:
                    return false;
            }
        }

        private static bool IsStoredScalarValueValid(FieldType type, object typeArgument, object value)
        {
            if (value == null)
            {
                return type == FieldType.tableRef;
            }

            switch (type)
            {
                case FieldType.@bool:
                    return value is bool;
                case FieldType.@int:
                    return IsInt32Value(value);
                case FieldType.@float:
                    return IsFiniteNumber(value);
                case FieldType.@string:
                case FieldType.tableRef:
                case FieldType.unityObject:
                    return value is string;
                case FieldType.@enum:
                    return IsStoredEnumValueValid((Type)typeArgument, value);
                case FieldType.color:
                    return value is Color;
                case FieldType.vector2:
                    return value is Vector2;
                case FieldType.vector3:
                    return value is Vector3;
                case FieldType.vector4:
                    return value is Vector4;
                default:
                    return false;
            }
        }

        private static bool IsWireEnumValueValid(Type enumType, string value)
        {
            return enumType != null && Enum.GetNames(enumType).Contains(value);
        }

        private static bool IsStoredEnumValueValid(Type enumType, object value)
        {
            return enumType != null && value != null && value.GetType() == enumType
                && Enum.IsDefined(enumType, value);
        }

        private static bool IsInt32Value(object value)
        {
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                var converted = Convert.ToInt64(value);
                return converted >= int.MinValue && converted <= int.MaxValue
                    && Convert.ToDecimal(value) == converted;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsFiniteNumber(object value)
        {
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                var converted = Convert.ToDouble(value);
                return !double.IsNaN(converted) && !double.IsInfinity(converted)
                    && converted <= float.MaxValue && converted >= -float.MaxValue;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static void ValidateFieldReferences(GameDB gameDB, string tableName,
            string fieldName, FieldBase field, List<GameDBValidationIssue> issues)
        {
            if (field.Type == FieldType.tableRef)
            {
                ValidateTableReferenceField(gameDB, tableName, fieldName,
                    field.GetTypeArg<string>(), field, issues);
            }
            else if (field.Type == FieldType.dictionary)
            {
                var dictionaryType = field.GetTypeArg<DictionaryType>();
                if (dictionaryType.ValueType == FieldType.tableRef)
                {
                    ValidateDictionaryTableReferences(gameDB, tableName, fieldName,
                        dictionaryType.ValueTypeArg as string, field, issues);
                }
            }
        }

        private static void ValidateTableReferenceField(GameDB gameDB, string tableName,
            string fieldName, string referencedTableName, FieldBase field,
            List<GameDBValidationIssue> issues)
        {
            if (!gameDB.Tables.TryGetValue(referencedTableName ?? string.Empty, out var referencedTable))
            {
                issues.Add(Issue("tableRef.table.missing",
                    $"Referenced table does not exist: {referencedTableName}", tableName, fieldName));
                return;
            }

            var table = (TableModel)gameDB.Tables[tableName];
            foreach (var rowPair in table.Data)
            {
                if (!rowPair.Value.Data.TryGetValue(fieldName, out var value))
                {
                    continue;
                }

                if (field.IsArray && value is IEnumerable values && !(value is string))
                {
                    foreach (var item in values)
                    {
                        ValidateReferenceValue(referencedTable, item as string,
                            tableName, fieldName, rowPair.Key, issues);
                    }
                }
                else
                {
                    ValidateReferenceValue(referencedTable, value as string,
                        tableName, fieldName, rowPair.Key, issues);
                }
            }
        }

        private static void ValidateDictionaryTableReferences(GameDB gameDB, string tableName,
            string fieldName, string referencedTableName, FieldBase field,
            List<GameDBValidationIssue> issues)
        {
            if (!gameDB.Tables.TryGetValue(referencedTableName ?? string.Empty, out var referencedTable))
            {
                issues.Add(Issue("tableRef.table.missing",
                    $"Referenced table does not exist: {referencedTableName}", tableName, fieldName));
                return;
            }

            var table = (TableModel)gameDB.Tables[tableName];
            foreach (var rowPair in table.Data)
            {
                if (!rowPair.Value.Data.TryGetValue(fieldName, out var value)
                    || !(value is IDictionary dictionary))
                {
                    continue;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    ValidateReferenceValue(referencedTable, entry.Value as string,
                        tableName, fieldName, rowPair.Key, issues);
                }
            }
        }

        private static void ValidateReferenceValue(TableBase referencedTable, string value,
            string tableName, string fieldName, string rowKey, List<GameDBValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(value) || value == FieldBase.NullRefToken)
            {
                return;
            }

            if (!referencedTable.Data.ContainsKey(value))
            {
                issues.Add(Issue("tableRef.row.missing",
                    $"Referenced row does not exist: {value}", tableName, fieldName, rowKey));
            }
        }

        private static GameDBValidationIssue Issue(string code, string message,
            string tableName = null, string fieldName = null, string rowKey = null)
        {
            return new GameDBValidationIssue
            {
                Code = code,
                Message = message,
                TableName = tableName,
                FieldName = fieldName,
                RowKey = rowKey
            };
        }
    }
}
