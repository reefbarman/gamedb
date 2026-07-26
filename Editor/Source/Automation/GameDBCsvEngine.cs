using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameDBEditorLibrary.Automation
{
    internal sealed class GameDBCsvImportPlan
    {
        internal bool Success => Errors.Count == 0;
        internal GameDBCsvFailureKind FailureKind { get; set; }
        internal string Message { get; set; }
        internal List<GameDBTableRowInput> Rows { get; } = new List<GameDBTableRowInput>();
        internal List<GameDBCsvError> Errors { get; } = new List<GameDBCsvError>();
        internal Dictionary<string, GameDBCsvCell> KeyCells { get; }
            = new Dictionary<string, GameDBCsvCell>(StringComparer.Ordinal);
        internal Dictionary<string, Dictionary<string, GameDBCsvCell>> ValueCells { get; }
            = new Dictionary<string, Dictionary<string, GameDBCsvCell>>(StringComparer.Ordinal);
    }

    internal static class GameDBCsvEngine
    {
        internal const string KeyColumnName = "__key";
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;

        internal static GameDBCsvExportResult Export(string databasePath, GameDBSnapshot snapshot,
            string tableName, IReadOnlyList<GameDBValidationIssue> issues)
        {
            var result = new GameDBCsvExportResult
            {
                DatabasePath = databasePath,
                TableName = tableName,
                Revision = snapshot?.Revision,
                Issues = issues?.ToList() ?? new List<GameDBValidationIssue>()
            };
            if (snapshot == null)
            {
                return ExportFailure(result, GameDBCsvFailureKind.LoadFailed,
                    "Database snapshot is required.", "csv.snapshotRequired");
            }

            var table = snapshot.Tables.FirstOrDefault(item =>
                string.Equals(item.Name, tableName, StringComparison.Ordinal));
            if (table == null)
            {
                return ExportFailure(result, GameDBCsvFailureKind.InvalidRequest,
                    $"Table does not exist: {tableName}", "csv.tableMissing");
            }

            var schemaError = ValidateSupportedSchema(table);
            if (schemaError != null)
            {
                return ExportFailure(result, GameDBCsvFailureKind.UnsupportedSchema,
                    schemaError.Message, schemaError.Code, schemaError.FieldName);
            }

            if (result.Issues.Count > 0)
            {
                result.FailureKind = GameDBCsvFailureKind.ValidationFailed;
                result.Message = $"CSV export blocked by {result.Issues.Count} validation issue(s).";
                return result;
            }

            if (table.KeyType == KeyType.@enum)
            {
                var invalidKey = table.Rows.FirstOrDefault(row =>
                    !IsEnumName(table.KeyTypeArgument, row.Key));
                if (invalidKey != null)
                {
                    return ExportFailure(result, GameDBCsvFailureKind.ValidationFailed,
                        $"Row key is not a declared enum member: {invalidKey.Key}",
                        "csv.rowKeyInvalid", rowKey: invalidKey.Key);
                }
            }

            var fields = table.Fields.OrderBy(field => field.Name, NameComparer).ToArray();
            var records = new List<IReadOnlyList<string>>
            {
                new[] { KeyColumnName }
                    .Concat(fields.Select(field => GameDBCsvCodec.EscapeFormula(field.Name)))
                    .ToArray()
            };
            foreach (var row in table.Rows.OrderBy(item => item.Key, NameComparer))
            {
                var record = new List<string>
                {
                    GameDBCsvCodec.EscapeFormula(row.Key)
                };
                foreach (var field in fields)
                {
                    if (!row.Values.TryGetValue(field.Name, out var value))
                    {
                        return ExportFailure(result, GameDBCsvFailureKind.ValidationFailed,
                            $"Row {row.Key} is missing field {field.Name}.",
                            "csv.valueMissing", field.Name, row.Key);
                    }

                    if (!TryFormatScalar(field, value, out var text, out var error))
                    {
                        return ExportFailure(result, GameDBCsvFailureKind.ValidationFailed,
                            error, "csv.valueInvalid", field.Name, row.Key);
                    }
                    record.Add(GameDBCsvCodec.EscapeFormula(text));
                }
                records.Add(record);
            }

            result.Success = true;
            result.FailureKind = GameDBCsvFailureKind.None;
            result.Message = $"Exported {table.Rows.Count} row(s) from {table.Name}.";
            result.CsvText = GameDBCsvCodec.Write(records);
            result.RowCount = table.Rows.Count;
            return result;
        }

        internal static GameDBCsvImportPlan PrepareImport(GameDBSnapshot snapshot,
            GameDBCsvImportRequest request)
        {
            var plan = new GameDBCsvImportPlan
            {
                FailureKind = GameDBCsvFailureKind.InvalidCsv,
                Message = "CSV import validation failed."
            };
            var table = snapshot?.Tables.FirstOrDefault(item =>
                string.Equals(item.Name, request.TableName, StringComparison.Ordinal));
            if (table == null)
            {
                AddError(plan, Error("csv.tableMissing", $"Table does not exist: {request.TableName}"));
                plan.FailureKind = GameDBCsvFailureKind.InvalidRequest;
                return plan;
            }

            var schemaError = ValidateSupportedSchema(table);
            if (schemaError != null)
            {
                AddError(plan, schemaError);
                plan.FailureKind = GameDBCsvFailureKind.UnsupportedSchema;
                return plan;
            }

            var parsed = GameDBCsvCodec.Parse(request.CsvText);
            if (!parsed.Success)
            {
                AddError(plan, parsed.Error);
                return plan;
            }

            var header = parsed.Records[0];
            var decodedHeaders = header.Cells
                .Select(cell => GameDBCsvCodec.UnescapeFormula(cell.Text)).ToArray();
            if (decodedHeaders.Length == 0 || decodedHeaders[0] != KeyColumnName)
            {
                AddError(plan, CellError("csv.keyColumnRequired",
                    $"The first header must be {KeyColumnName}.", header.Cells[0], KeyColumnName));
                return plan;
            }

            var fieldsByName = table.Fields.ToDictionary(field => field.Name, NameComparer);
            var seenHeaders = new HashSet<string>(NameComparer) { KeyColumnName };
            for (var index = 1; index < decodedHeaders.Length; index++)
            {
                var name = decodedHeaders[index];
                var cell = header.Cells[index];
                if (name.Length == 0)
                {
                    AddError(plan, CellError("csv.headerEmpty", "Column names cannot be empty.", cell));
                    continue;
                }
                if (!seenHeaders.Add(name))
                {
                    AddError(plan, CellError("csv.headerDuplicate",
                        $"Column appears more than once: {name}", cell, name));
                    continue;
                }
                if (!fieldsByName.TryGetValue(name, out var field))
                {
                    AddError(plan, CellError("csv.headerUnknown",
                        $"Field does not exist: {name}", cell, name));
                    continue;
                }
            }

            if (request.Mode == GameDBCsvImportMode.Replace)
            {
                foreach (var field in table.Fields.OrderBy(item => item.Name, NameComparer))
                {
                    if (!seenHeaders.Contains(field.Name))
                    {
                        AddError(plan, Error("csv.replaceFieldMissing",
                            $"Replace mode requires field column: {field.Name}", fieldName: field.Name));
                    }
                }
            }
            if (plan.Errors.Count > 0)
            {
                return plan;
            }

            var seenKeys = new HashSet<string>(NameComparer);
            for (var recordIndex = 1; recordIndex < parsed.Records.Count; recordIndex++)
            {
                var record = parsed.Records[recordIndex];
                var keyCell = record.Cells[0];
                var rowKey = GameDBCsvCodec.UnescapeFormula(keyCell.Text);
                if (!TryValidateRowKey(table, rowKey, out var keyError))
                {
                    AddError(plan, CellError("csv.rowKeyInvalid", keyError,
                        keyCell, KeyColumnName, rowKey));
                    continue;
                }
                if (!seenKeys.Add(rowKey))
                {
                    AddError(plan, CellError("csv.rowKeyDuplicate",
                        $"Row key appears more than once: {rowKey}",
                        keyCell, KeyColumnName, rowKey));
                    continue;
                }

                plan.KeyCells.Add(rowKey, keyCell);
                var valueCells = new Dictionary<string, GameDBCsvCell>(NameComparer);
                plan.ValueCells.Add(rowKey, valueCells);
                var values = new Dictionary<string, object>(NameComparer);
                for (var columnIndex = 1; columnIndex < record.Cells.Count; columnIndex++)
                {
                    var field = fieldsByName[decodedHeaders[columnIndex]];
                    var cell = record.Cells[columnIndex];
                    valueCells.Add(field.Name, cell);
                    var text = GameDBCsvCodec.UnescapeFormula(cell.Text);
                    if (!TryParseScalar(field, text, out var value, out var valueError))
                    {
                        AddError(plan, CellError("csv.valueInvalid", valueError,
                            cell, field.Name, rowKey, field.Name));
                        continue;
                    }
                    values.Add(field.Name, value);
                }

                if (!plan.Errors.Any(error => error.RecordNumber == record.RecordNumber))
                {
                    plan.Rows.Add(new GameDBTableRowInput(rowKey, values));
                }
            }

            if (plan.Errors.Count == 0)
            {
                plan.FailureKind = GameDBCsvFailureKind.None;
                plan.Message = $"Validated {plan.Rows.Count} CSV row(s).";
            }
            return plan;
        }

        internal static List<GameDBCsvError> MapValidationIssues(GameDBCsvImportPlan plan,
            IEnumerable<GameDBValidationIssue> issues, string tableName)
        {
            var errors = new List<GameDBCsvError>();
            foreach (var issue in issues)
            {
                if (!string.Equals(issue.TableName, tableName, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(issue.RowKey))
                {
                    continue;
                }

                GameDBCsvCell cell = null;
                if (!string.IsNullOrEmpty(issue.FieldName))
                {
                    if (plan.ValueCells.TryGetValue(issue.RowKey, out var valueCells))
                    {
                        valueCells.TryGetValue(issue.FieldName, out cell);
                    }
                }
                else
                {
                    plan.KeyCells.TryGetValue(issue.RowKey, out cell);
                }
                if (cell != null)
                {
                    errors.Add(CellError(issue.Code, issue.Message, cell,
                        issue.FieldName ?? KeyColumnName, issue.RowKey, issue.FieldName));
                }
            }
            return errors;
        }

        private static GameDBCsvError ValidateSupportedSchema(GameDBTableSnapshot table)
        {
            var keyCollision = table.Fields.FirstOrDefault(field => field.Name == KeyColumnName);
            if (keyCollision != null)
            {
                return Error("csv.reservedColumnCollision",
                    $"Field name is reserved by the CSV dialect: {KeyColumnName}",
                    fieldName: keyCollision.Name);
            }

            var collection = table.Fields.FirstOrDefault(field =>
                field.IsArray || field.FieldType == FieldType.dictionary);
            return collection == null
                ? null
                : Error("csv.collectionUnsupported",
                    $"CSV does not support collection field: {collection.Name}",
                    fieldName: collection.Name);
        }

        private static bool TryValidateRowKey(GameDBTableSnapshot table, string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Row key cannot be empty or whitespace.";
                return false;
            }
            if (value == FieldBase.NullRefToken)
            {
                error = $"{FieldBase.NullRefToken} is reserved for null table references.";
                return false;
            }
            if (table.KeyType == KeyType.@enum && !IsEnumName(table.KeyTypeArgument, value))
            {
                error = $"Row key is not a declared enum member: {value}";
                return false;
            }
            return true;
        }

        private static bool TryFormatScalar(GameDBFieldSnapshot field, object value,
            out string text, out string error)
        {
            text = null;
            error = null;
            try
            {
                switch (field.FieldType)
                {
                    case FieldType.@string:
                        text = value as string;
                        break;
                    case FieldType.unityObject:
                        text = value is UnityObjectReference reference
                            ? JsonSerialization.Serialize(UnityObjectReferenceWire.Serialize(reference))
                            : null;
                        break;
                    case FieldType.tableRef:
                        text = value == null || Equals(value, FieldBase.NullRefToken)
                            ? string.Empty : value as string;
                        break;
                    case FieldType.@bool:
                        if (value is bool boolean)
                        {
                            text = boolean ? "true" : "false";
                        }
                        break;
                    case FieldType.@int:
                        var integer = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        if (integer >= int.MinValue && integer <= int.MaxValue)
                        {
                            text = integer.ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                    case FieldType.@float:
                        var single = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                        if (!float.IsNaN(single) && !float.IsInfinity(single))
                        {
                            text = single.ToString("R", CultureInfo.InvariantCulture);
                        }
                        break;
                    case FieldType.@enum:
                        text = value?.ToString();
                        if (!IsEnumName(field.TypeArgument, text))
                        {
                            text = null;
                        }
                        break;
                    case FieldType.color:
                        text = (value as Color)?.ToString();
                        break;
                    case FieldType.vector2:
                        text = (value as Vector2)?.ToString();
                        break;
                    case FieldType.vector3:
                        text = (value as Vector3)?.ToString();
                        break;
                    case FieldType.vector4:
                        text = (value as Vector4)?.ToString();
                        break;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (text != null)
            {
                return true;
            }
            error = error ?? $"Stored value is invalid for {field.FieldType}.";
            return false;
        }

        private static bool TryParseScalar(GameDBFieldSnapshot field, string text,
            out object value, out string error)
        {
            value = null;
            error = null;
            switch (field.FieldType)
            {
                case FieldType.@string:
                    value = text;
                    return true;
                case FieldType.unityObject:
                    try
                    {
                        var wireValue = JsonSerialization.Deserialize(text);
                        if (UnityObjectReferenceWire.TryParse(wireValue, out _))
                        {
                            value = wireValue;
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        error = $"Cell value is invalid for {field.FieldType}.";
                        return false;
                    }
                    break;
                case FieldType.tableRef:
                    if (text == FieldBase.NullRefToken)
                    {
                        error = $"Use an empty cell for an unset reference; {FieldBase.NullRefToken} is reserved.";
                        return false;
                    }
                    value = text.Length == 0 ? null : (object)text;
                    return true;
                case FieldType.@bool:
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        value = true;
                        return true;
                    }
                    if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        value = false;
                        return true;
                    }
                    break;
                case FieldType.@int:
                    if (int.TryParse(text, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out var integer))
                    {
                        value = integer;
                        return true;
                    }
                    break;
                case FieldType.@float:
                    var floatStyles = NumberStyles.AllowLeadingSign
                        | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
                    if (float.TryParse(text, floatStyles, CultureInfo.InvariantCulture,
                        out var single) && !float.IsNaN(single) && !float.IsInfinity(single))
                    {
                        value = single;
                        return true;
                    }
                    break;
                case FieldType.@enum:
                    if (IsEnumName(field.TypeArgument, text))
                    {
                        value = text;
                        return true;
                    }
                    break;
                case FieldType.color:
                    if (IsColor(text))
                    {
                        value = new Color(text).ToString();
                        return true;
                    }
                    break;
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                    if (TryNormalizeVector(field.FieldType, text, out var vector))
                    {
                        value = vector;
                        return true;
                    }
                    break;
            }

            error = $"Cell value is invalid for {field.FieldType}.";
            return false;
        }

        private static bool IsEnumName(string typeName, string value)
        {
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(value))
            {
                return false;
            }
            AssemblyExplorer.Instance.Load();
            var type = AssemblyExplorer.Instance.GetType(typeName);
            return type != null && type.IsEnum && Enum.GetNames(type).Contains(value);
        }

        private static bool IsColor(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            var hex = value.StartsWith("#", StringComparison.Ordinal)
                ? value.Substring(1)
                : value.StartsWith("0x", StringComparison.Ordinal)
                    ? value.Substring(2)
                    : value;
            if (hex.Length != 6 && hex.Length != 8)
            {
                return false;
            }
            return hex.All(character => Uri.IsHexDigit(character));
        }

        private static bool TryNormalizeVector(FieldType type, string text, out string normalized)
        {
            normalized = null;
            var count = type == FieldType.vector2 ? 2
                : type == FieldType.vector3 ? 3
                : type == FieldType.vector4 ? 4 : 0;
            var parts = text.Split(',');
            if (count == 0 || parts.Length != count)
            {
                return false;
            }

            var values = new float[count];
            var styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                | NumberStyles.AllowExponent;
            for (var index = 0; index < count; index++)
            {
                if (!float.TryParse(parts[index], styles, CultureInfo.InvariantCulture,
                    out values[index]) || float.IsNaN(values[index]) || float.IsInfinity(values[index]))
                {
                    return false;
                }
            }
            normalized = string.Join(",", values.Select(value =>
                value.ToString("R", CultureInfo.InvariantCulture)).ToArray());
            return true;
        }

        private static GameDBCsvExportResult ExportFailure(GameDBCsvExportResult result,
            GameDBCsvFailureKind kind, string message, string code,
            string fieldName = null, string rowKey = null)
        {
            result.Success = false;
            result.FailureKind = kind;
            result.Message = message;
            result.Errors.Add(Error(code, message, fieldName: fieldName, rowKey: rowKey));
            return result;
        }

        private static void AddError(GameDBCsvImportPlan plan, GameDBCsvError error)
        {
            plan.Errors.Add(error);
        }

        private static GameDBCsvError CellError(string code, string message, GameDBCsvCell cell,
            string columnName = null, string rowKey = null, string fieldName = null)
        {
            return Error(code, message, cell.RecordNumber, cell.LineNumber,
                cell.ColumnNumber, columnName, rowKey, fieldName);
        }

        private static GameDBCsvError Error(string code, string message,
            int recordNumber = -1, int lineNumber = -1, int columnNumber = -1,
            string columnName = null, string rowKey = null, string fieldName = null)
        {
            return new GameDBCsvError
            {
                Code = code,
                Message = message,
                RecordNumber = recordNumber,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                ColumnName = columnName,
                RowKey = rowKey,
                FieldName = fieldName
            };
        }
    }
}
