using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace GameDBEditorLibrary.Automation
{
    internal static class GameDBQueryEngine
    {
        private const int MaximumLimit = 1000;
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;

        internal static GameDBQueryResult Preflight(string databasePath, GameDBQueryRequest request)
        {
            GameDBQueryResult invalid;
            if (!TryValidateRequestShape(databasePath, request, out invalid))
            {
                return invalid;
            }

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                QueryCursor cursor;
                string cursorError;
                if (!GameDBQueryCursorCodec.TryDecode(request.Cursor, out cursor, out cursorError))
                {
                    return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                        cursorError, "The query cursor is invalid.");
                }

                if (!string.Equals(cursor.DatabasePathHash, Sha256(databasePath), StringComparison.Ordinal))
                {
                    return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                        "cursor.mismatch", "The query cursor does not match this database and query.");
                }
            }

            return null;
        }

        internal static GameDBQueryResult Execute(string databasePath, GameDBSnapshot snapshot,
            GameDBQueryRequest request)
        {
            if (snapshot == null)
            {
                return Failure(databasePath, GameDBQueryFailureKind.EvaluationFailed,
                    "query.snapshotMissing", "The database snapshot is unavailable.");
            }

            var preflight = Preflight(databasePath, request);
            if (preflight != null)
            {
                preflight.Revision = snapshot.Revision;
                return preflight;
            }

            QueryCursor cursor = null;
            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                string cursorError;
                if (!GameDBQueryCursorCodec.TryDecode(request.Cursor, out cursor, out cursorError))
                {
                    return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                        cursorError, "The query cursor is invalid.", snapshot.Revision);
                }

                if (!string.Equals(cursor.DatabasePathHash, Sha256(databasePath), StringComparison.Ordinal))
                {
                    return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                        "cursor.mismatch", "The query cursor does not match this database and query.",
                        snapshot.Revision);
                }

                if (!string.Equals(cursor.Revision, snapshot.Revision, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(databasePath, GameDBQueryFailureKind.StaleCursor,
                        "cursor.stale", "The database changed after the query cursor was issued.",
                        snapshot.Revision);
                }
            }

            QueryPlan plan;
            GameDBQueryResult invalid;
            try
            {
                if (!TryCreatePlan(databasePath, snapshot, request, out plan, out invalid))
                {
                    return invalid;
                }
            }
            catch (Exception exception)
            {
                return Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "query.planFailed", exception.Message, snapshot.Revision);
            }

            if (cursor != null && !string.Equals(cursor.QueryHash, plan.QueryHash, StringComparison.Ordinal))
            {
                return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                    "cursor.mismatch", "The query cursor does not match this database and query.",
                    snapshot.Revision);
            }

            try
            {
                return Evaluate(databasePath, snapshot, plan, request.Limit, cursor);
            }
            catch (Exception exception)
            {
                return Failure(databasePath, GameDBQueryFailureKind.EvaluationFailed,
                    "query.evaluationFailed", exception.Message, snapshot.Revision);
            }
        }

        private static bool TryValidateRequestShape(string databasePath, GameDBQueryRequest request,
            out GameDBQueryResult invalid)
        {
            invalid = null;
            if (request == null)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "request.required", "Request is required.");
                return false;
            }

            if (request.Limit < 1 || request.Limit > MaximumLimit)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "limit.outOfRange", $"Limit must be between 1 and {MaximumLimit}.");
                return false;
            }

            if (request.Tables == null || request.Tables.Count == 0)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "projection.required", "At least one table projection is required.");
                return false;
            }

            var seenTables = new HashSet<string>(NameComparer);
            for (var projectionIndex = 0; projectionIndex < request.Tables.Count; projectionIndex++)
            {
                var projection = request.Tables[projectionIndex];
                if (projection == null || string.IsNullOrWhiteSpace(projection.TableName))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        "projection.tableRequired", "Each projection requires a table name.",
                        projectionIndex: projectionIndex);
                    return false;
                }

                if (!seenTables.Add(projection.TableName))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        "projection.duplicateTable", $"Table is projected more than once: {projection.TableName}",
                        projectionIndex: projectionIndex, tableName: projection.TableName);
                    return false;
                }

                if (!TryValidateSelectorShape(databasePath, projection.RowKeys, "row",
                    projectionIndex, projection.TableName, out invalid)
                    || !TryValidateSelectorShape(databasePath, projection.FieldNames, "field",
                        projectionIndex, projection.TableName, out invalid))
                {
                    return false;
                }

                var predicates = projection.Predicates ?? new List<GameDBQueryPredicate>();
                for (var predicateIndex = 0; predicateIndex < predicates.Count; predicateIndex++)
                {
                    var predicate = predicates[predicateIndex];
                    if (predicate == null)
                    {
                        invalid = PredicateFailure(databasePath, null, "predicate.required",
                            "Predicates cannot contain null entries.", projectionIndex, predicateIndex,
                            projection.TableName, null);
                        return false;
                    }

                    if (!Enum.IsDefined(typeof(GameDBQueryPredicateKind), predicate.Kind)
                        || predicate.Kind == GameDBQueryPredicateKind.Unspecified)
                    {
                        invalid = PredicateFailure(databasePath, null, "predicate.unspecified",
                            $"Unsupported predicate kind: {predicate.Kind}.", projectionIndex,
                            predicateIndex, projection.TableName, predicate.FieldName);
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(predicate.FieldName))
                    {
                        invalid = PredicateFailure(databasePath, null, "field.required",
                            "Predicate field name is required.", projectionIndex, predicateIndex,
                            projection.TableName, predicate.FieldName);
                        return false;
                    }

                    if (predicate.Kind == GameDBQueryPredicateKind.NumericRange
                        && predicate.Minimum == null && predicate.Maximum == null)
                    {
                        invalid = PredicateFailure(databasePath, null, "range.boundsMissing",
                            "NumericRange requires Minimum or Maximum.", projectionIndex,
                            predicateIndex, projection.TableName, predicate.FieldName);
                        return false;
                    }

                    var payloadInvalid = predicate.Kind == GameDBQueryPredicateKind.NumericRange
                        ? predicate.Value != null
                        : predicate.Minimum != null || predicate.Maximum != null;
                    if (payloadInvalid)
                    {
                        invalid = PredicateFailure(databasePath, null, "predicate.payloadInvalid",
                            "Predicate payload properties do not match the predicate kind.", projectionIndex,
                            predicateIndex, projection.TableName, predicate.FieldName);
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryValidateSelectorShape(string databasePath, IList<string> requested,
            string selectorName, int projectionIndex, string tableName,
            out GameDBQueryResult invalid)
        {
            invalid = null;
            if (requested == null || requested.Count == 0)
            {
                return true;
            }

            var seen = new HashSet<string>(NameComparer);
            foreach (var name in requested)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        $"{selectorName}.required", $"Projected {selectorName} names cannot be empty.",
                        projectionIndex: projectionIndex, tableName: tableName);
                    return false;
                }

                if (!seen.Add(name))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        $"{selectorName}.duplicate", $"{selectorName} is selected more than once: {name}",
                        projectionIndex: projectionIndex, tableName: tableName,
                        fieldName: selectorName == "field" ? name : null);
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreatePlan(string databasePath, GameDBSnapshot snapshot,
            GameDBQueryRequest request, out QueryPlan plan, out GameDBQueryResult invalid)
        {
            plan = null;
            invalid = null;
            if (request == null)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "request.required", "Request is required.", snapshot.Revision);
                return false;
            }

            if (request.Limit < 1 || request.Limit > MaximumLimit)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "limit.outOfRange", $"Limit must be between 1 and {MaximumLimit}.", snapshot.Revision);
                return false;
            }

            if (request.Tables == null || request.Tables.Count == 0)
            {
                invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                    "projection.required", "At least one table projection is required.", snapshot.Revision);
                return false;
            }

            var tablesByName = snapshot.Tables.ToDictionary(table => table.Name, NameComparer);
            var seenTables = new HashSet<string>(NameComparer);
            var tablePlans = new List<QueryTablePlan>();
            for (var projectionIndex = 0; projectionIndex < request.Tables.Count; projectionIndex++)
            {
                var projection = request.Tables[projectionIndex];
                if (projection == null || string.IsNullOrWhiteSpace(projection.TableName))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        "projection.tableRequired", "Each projection requires a table name.",
                        snapshot.Revision, projectionIndex: projectionIndex);
                    return false;
                }

                if (!seenTables.Add(projection.TableName))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        "projection.duplicateTable", $"Table is projected more than once: {projection.TableName}",
                        snapshot.Revision, projectionIndex: projectionIndex, tableName: projection.TableName);
                    return false;
                }

                GameDBTableSnapshot table;
                if (!tablesByName.TryGetValue(projection.TableName, out table))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        "table.notFound", $"Table does not exist: {projection.TableName}",
                        snapshot.Revision, projectionIndex: projectionIndex, tableName: projection.TableName);
                    return false;
                }

                QueryTablePlan tablePlan;
                if (!TryCreateTablePlan(databasePath, snapshot, table, projection,
                    projectionIndex, out tablePlan, out invalid))
                {
                    return false;
                }

                tablePlans.Add(tablePlan);
            }

            tablePlans.Sort((left, right) => NameComparer.Compare(left.Table.Name, right.Table.Name));
            plan = new QueryPlan(tablePlans, ComputeQueryHash(tablePlans));
            return true;
        }

        private static bool TryCreateTablePlan(string databasePath, GameDBSnapshot snapshot,
            GameDBTableSnapshot table, GameDBQueryTableProjection projection, int projectionIndex,
            out QueryTablePlan plan, out GameDBQueryResult invalid)
        {
            plan = null;
            invalid = null;
            HashSet<string> rowKeys;
            if (!TryCreateSelector(databasePath, snapshot.Revision, projection.RowKeys,
                table.Rows.Select(row => row.Key), "row", projectionIndex, table.Name,
                out rowKeys, out invalid))
            {
                return false;
            }

            HashSet<string> fieldNames;
            if (!TryCreateSelector(databasePath, snapshot.Revision, projection.FieldNames,
                table.Fields.Select(field => field.Name), "field", projectionIndex, table.Name,
                out fieldNames, out invalid))
            {
                return false;
            }

            var projectedFields = (fieldNames == null
                    ? table.Fields
                    : table.Fields.Where(field => fieldNames.Contains(field.Name)))
                .OrderBy(field => field.Name, NameComparer)
                .ToList();
            var fieldsByName = table.Fields.ToDictionary(field => field.Name, NameComparer);
            var predicates = new List<QueryPredicatePlan>();
            var predicateRequests = projection.Predicates ?? new List<GameDBQueryPredicate>();
            for (var predicateIndex = 0; predicateIndex < predicateRequests.Count; predicateIndex++)
            {
                QueryPredicatePlan predicate;
                if (!TryCreatePredicate(databasePath, snapshot, table, fieldsByName,
                    predicateRequests[predicateIndex], projectionIndex, predicateIndex,
                    out predicate, out invalid))
                {
                    return false;
                }

                predicates.Add(predicate);
            }

            plan = new QueryTablePlan(table, rowKeys, projectedFields, predicates);
            return true;
        }

        private static bool TryCreateSelector(string databasePath, string revision,
            IList<string> requested, IEnumerable<string> available, string selectorName,
            int projectionIndex, string tableName, out HashSet<string> selector,
            out GameDBQueryResult invalid)
        {
            selector = null;
            invalid = null;
            if (requested == null || requested.Count == 0)
            {
                return true;
            }

            selector = new HashSet<string>(NameComparer);
            var availableSet = new HashSet<string>(available, NameComparer);
            foreach (var name in requested)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        $"{selectorName}.required", $"Projected {selectorName} names cannot be empty.",
                        revision, projectionIndex: projectionIndex, tableName: tableName);
                    return false;
                }

                if (!selector.Add(name))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        $"{selectorName}.duplicate", $"{selectorName} is selected more than once: {name}",
                        revision, projectionIndex: projectionIndex, tableName: tableName,
                        fieldName: selectorName == "field" ? name : null);
                    return false;
                }

                if (!availableSet.Contains(name))
                {
                    invalid = Failure(databasePath, GameDBQueryFailureKind.InvalidRequest,
                        $"{selectorName}.notFound", $"{selectorName} does not exist in {tableName}: {name}",
                        revision, projectionIndex: projectionIndex, tableName: tableName,
                        fieldName: selectorName == "field" ? name : null);
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreatePredicate(string databasePath, GameDBSnapshot snapshot,
            GameDBTableSnapshot table, IDictionary<string, GameDBFieldSnapshot> fieldsByName,
            GameDBQueryPredicate request, int projectionIndex, int predicateIndex,
            out QueryPredicatePlan predicate, out GameDBQueryResult invalid)
        {
            predicate = null;
            invalid = null;
            if (request == null)
            {
                invalid = PredicateFailure(databasePath, snapshot.Revision, "predicate.required",
                    "Predicates cannot contain null entries.", projectionIndex, predicateIndex,
                    table.Name, null);
                return false;
            }

            if (!Enum.IsDefined(typeof(GameDBQueryPredicateKind), request.Kind)
                || request.Kind == GameDBQueryPredicateKind.Unspecified)
            {
                invalid = PredicateFailure(databasePath, snapshot.Revision, "predicate.unspecified",
                    $"Unsupported predicate kind: {request.Kind}.", projectionIndex, predicateIndex,
                    table.Name, request.FieldName);
                return false;
            }

            GameDBFieldSnapshot field;
            if (string.IsNullOrWhiteSpace(request.FieldName)
                || !fieldsByName.TryGetValue(request.FieldName, out field))
            {
                invalid = PredicateFailure(databasePath, snapshot.Revision, "field.notFound",
                    $"Predicate field does not exist in {table.Name}: {request.FieldName}",
                    projectionIndex, predicateIndex, table.Name, request.FieldName);
                return false;
            }

            string validationError = null;
            string validationCode = null;
            object normalizedValue = null;
            object normalizedMinimum = null;
            object normalizedMaximum = null;
            switch (request.Kind)
            {
                case GameDBQueryPredicateKind.Equals:
                    if (request.Minimum != null || request.Maximum != null
                        || !TryNormalizeScalar(field, request.Value, true,
                            out normalizedValue, out validationCode, out validationError))
                    {
                        invalid = PredicateFailure(databasePath, snapshot.Revision,
                            validationCode ?? "predicate.payloadInvalid",
                            validationError ?? "Equals accepts only Value.", projectionIndex,
                            predicateIndex, table.Name, field.Name);
                        return false;
                    }
                    break;
                case GameDBQueryPredicateKind.Contains:
                    if (request.Minimum != null || request.Maximum != null
                        || !TryNormalizeContainsValue(field, request.Value,
                            out normalizedValue, out validationCode, out validationError))
                    {
                        invalid = PredicateFailure(databasePath, snapshot.Revision,
                            validationCode ?? "predicate.payloadInvalid",
                            validationError ?? "Contains accepts only Value.", projectionIndex,
                            predicateIndex, table.Name, field.Name);
                        return false;
                    }
                    break;
                case GameDBQueryPredicateKind.NumericRange:
                    if (request.Value != null || request.Minimum == null && request.Maximum == null
                        || !TryNormalizeRange(field, request.Minimum, request.Maximum,
                            out normalizedMinimum, out normalizedMaximum,
                            out validationCode, out validationError))
                    {
                        invalid = PredicateFailure(databasePath, snapshot.Revision,
                            validationCode ?? (request.Minimum == null && request.Maximum == null
                                ? "range.boundsMissing" : "predicate.payloadInvalid"),
                            validationError ?? "NumericRange requires Minimum or Maximum and does not accept Value.",
                            projectionIndex, predicateIndex, table.Name, field.Name);
                        return false;
                    }
                    break;
                case GameDBQueryPredicateKind.ReferencesRow:
                    string referencedTable;
                    if (request.Minimum != null || request.Maximum != null
                        || !TryValidateReference(snapshot, field, request.Value,
                            out normalizedValue, out referencedTable, out validationCode, out validationError))
                    {
                        invalid = PredicateFailure(databasePath, snapshot.Revision,
                            validationCode ?? "predicate.payloadInvalid",
                            validationError ?? "ReferencesRow accepts only Value.", projectionIndex,
                            predicateIndex, table.Name, field.Name);
                        return false;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            predicate = new QueryPredicatePlan(request.Kind, field,
                normalizedValue, normalizedMinimum, normalizedMaximum);
            return true;
        }

        private static bool TryNormalizeScalar(GameDBFieldSnapshot field, object value,
            bool allowNullReference, out object normalized, out string code, out string error)
        {
            normalized = null;
            code = "predicate.incompatible";
            error = null;
            if (field.IsArray || field.FieldType == FieldType.dictionary)
            {
                error = $"Equals is not supported for collection field {field.Name}.";
                return false;
            }

            code = "predicate.valueInvalid";
            if (value == null)
            {
                if (allowNullReference && field.FieldType == FieldType.tableRef)
                {
                    return true;
                }

                error = $"Equals requires a non-null value for {field.FieldType}.";
                return false;
            }

            switch (field.FieldType)
            {
                case FieldType.@bool:
                    if (value is bool)
                    {
                        normalized = value;
                        return true;
                    }
                    break;
                case FieldType.@int:
                    long integer;
                    if (TryInt32(value, out integer))
                    {
                        normalized = integer;
                        return true;
                    }
                    break;
                case FieldType.@long:
                    long longInteger;
                    if (TryInt64(value, out longInteger))
                    {
                        normalized = longInteger;
                        return true;
                    }
                    break;
                case FieldType.@float:
                    double number;
                    if (TryFiniteSingle(value, out number))
                    {
                        normalized = number;
                        return true;
                    }
                    break;
                case FieldType.@double:
                    double doubleNumber;
                    if (TryFiniteDouble(value, out doubleNumber))
                    {
                        normalized = doubleNumber;
                        return true;
                    }
                    break;
                case FieldType.@string:
                case FieldType.tableRef:
                    if (value is string)
                    {
                        normalized = value;
                        return true;
                    }
                    break;
                case FieldType.unityObject:
                    if (UnityObjectReferenceWire.TryParse(value, out var referenceValue))
                    {
                        normalized = referenceValue;
                        return true;
                    }
                    break;
                case FieldType.@enum:
                    if (value is string enumName && IsEnumName(field.TypeArgument, enumName))
                    {
                        normalized = enumName;
                        return true;
                    }
                    break;
                case FieldType.color:
                    if (value is string color && IsColor(color))
                    {
                        normalized = new Color(color).ToString();
                        return true;
                    }
                    break;
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                    string vector;
                    if (TryNormalizeVector(field.FieldType, value, out vector))
                    {
                        normalized = vector;
                        return true;
                    }
                    break;
            }

            error = $"Value is invalid for {field.FieldType}.";
            return false;
        }

        private static bool TryNormalizeContainsValue(GameDBFieldSnapshot field, object value,
            out object normalized, out string code, out string error)
        {
            normalized = null;
            code = "predicate.incompatible";
            error = null;
            if (field.FieldType == FieldType.dictionary)
            {
                if (!(value is string key))
                {
                    code = "predicate.valueInvalid";
                    error = "Dictionary Contains requires a string key.";
                    return false;
                }

                if (field.DictionaryType.KeyType == KeyType.@enum
                    && !IsEnumName(field.DictionaryType.KeyTypeArgument, key))
                {
                    code = "predicate.valueInvalid";
                    error = $"Dictionary key is not a declared enum member: {key}";
                    return false;
                }

                normalized = key;
                return true;
            }

            if (field.IsArray)
            {
                if (field.FieldType == FieldType.tableRef)
                {
                    error = "Contains is not supported for table-reference arrays; use ReferencesRow.";
                    return false;
                }

                var scalarField = CopyAsScalar(field);
                return TryNormalizeScalar(scalarField, value, false,
                    out normalized, out code, out error);
            }

            if (field.FieldType == FieldType.@string)
            {
                if (value is string text)
                {
                    normalized = text;
                    return true;
                }

                code = "predicate.valueInvalid";
                error = "String Contains requires a string value.";
                return false;
            }

            error = $"Contains is not supported for scalar {field.FieldType}.";
            return false;
        }

        private static bool TryNormalizeRange(GameDBFieldSnapshot field, object minimum,
            object maximum, out object normalizedMinimum, out object normalizedMaximum,
            out string code, out string error)
        {
            normalizedMinimum = null;
            normalizedMaximum = null;
            code = "predicate.incompatible";
            error = null;
            if (field.IsArray || field.FieldType == FieldType.dictionary
                || field.FieldType != FieldType.@int && field.FieldType != FieldType.@long
                && field.FieldType != FieldType.@float && field.FieldType != FieldType.@double)
            {
                error = $"NumericRange is not supported for {field.FieldType}{(field.IsArray ? "[]" : string.Empty)}.";
                return false;
            }

            code = "predicate.valueInvalid";
            if (field.FieldType == FieldType.@int || field.FieldType == FieldType.@long)
            {
                var isInt32 = field.FieldType == FieldType.@int;
                long parsedMinimum = 0;
                if (minimum != null && !(isInt32
                        ? TryInt32(minimum, out parsedMinimum)
                        : TryInt64(minimum, out parsedMinimum)))
                {
                    error = $"Minimum is not an {(isInt32 ? "Int32" : "Int64")} value.";
                    return false;
                }
                normalizedMinimum = minimum == null ? null : (object)parsedMinimum;
                long parsedMaximum = 0;
                if (maximum != null && !(isInt32
                        ? TryInt32(maximum, out parsedMaximum)
                        : TryInt64(maximum, out parsedMaximum)))
                {
                    error = $"Maximum is not an {(isInt32 ? "Int32" : "Int64")} value.";
                    return false;
                }
                normalizedMaximum = maximum == null ? null : (object)parsedMaximum;
            }
            else
            {
                var isSingle = field.FieldType == FieldType.@float;
                double parsedMinimum = 0;
                if (minimum != null && !(isSingle
                        ? TryFiniteSingle(minimum, out parsedMinimum)
                        : TryFiniteDouble(minimum, out parsedMinimum)))
                {
                    error = $"Minimum is not a finite {(isSingle ? "Single" : "Double")} value.";
                    return false;
                }
                normalizedMinimum = minimum == null ? null : (object)parsedMinimum;
                double parsedMaximum = 0;
                if (maximum != null && !(isSingle
                        ? TryFiniteSingle(maximum, out parsedMaximum)
                        : TryFiniteDouble(maximum, out parsedMaximum)))
                {
                    error = $"Maximum is not a finite {(isSingle ? "Single" : "Double")} value.";
                    return false;
                }
                normalizedMaximum = maximum == null ? null : (object)parsedMaximum;
            }

            var orderInvalid = normalizedMinimum != null && normalizedMaximum != null
                && (field.FieldType == FieldType.@int || field.FieldType == FieldType.@long
                    ? (long)normalizedMinimum > (long)normalizedMaximum
                    : (double)normalizedMinimum > (double)normalizedMaximum);
            if (orderInvalid)
            {
                code = "range.orderInvalid";
                error = "Minimum cannot be greater than Maximum.";
                return false;
            }

            return true;
        }

        private static bool TryValidateReference(GameDBSnapshot snapshot, GameDBFieldSnapshot field,
            object value, out object normalized, out string referencedTable,
            out string code, out string error)
        {
            normalized = null;
            referencedTable = null;
            code = "predicate.incompatible";
            error = null;
            if (field.FieldType == FieldType.tableRef)
            {
                referencedTable = field.TypeArgument;
            }
            else if (field.FieldType == FieldType.dictionary
                && field.DictionaryType != null
                && field.DictionaryType.ValueType == FieldType.tableRef)
            {
                referencedTable = field.DictionaryType.ValueTypeArgument;
            }
            else
            {
                error = $"ReferencesRow is not supported for field {field.Name}.";
                return false;
            }

            if (!(value is string rowKey) || string.IsNullOrWhiteSpace(rowKey))
            {
                code = "predicate.valueInvalid";
                error = "ReferencesRow requires a non-empty row-key string.";
                return false;
            }

            var targetTableName = referencedTable;
            var table = snapshot.Tables.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, targetTableName, StringComparison.Ordinal));
            if (table == null || !table.Rows.Any(row =>
                string.Equals(row.Key, rowKey, StringComparison.Ordinal)))
            {
                code = "reference.rowNotFound";
                error = $"Referenced row does not exist: {referencedTable}[{rowKey}]";
                return false;
            }

            normalized = rowKey;
            return true;
        }

        private static GameDBQueryResult Evaluate(string databasePath, GameDBSnapshot snapshot,
            QueryPlan plan, int limit, QueryCursor cursor)
        {
            var result = new GameDBQueryResult
            {
                Success = true,
                FailureKind = GameDBQueryFailureKind.None,
                DatabasePath = databasePath,
                Revision = snapshot.Revision,
                Message = "Query completed."
            };
            var resultTables = new Dictionary<string, GameDBQueryTableResult>(NameComparer);
            foreach (var tablePlan in plan.Tables)
            {
                var tableResult = new GameDBQueryTableResult
                {
                    Name = tablePlan.Table.Name,
                    KeyType = tablePlan.Table.KeyType,
                    KeyTypeArgument = tablePlan.Table.KeyTypeArgument,
                    Fields = tablePlan.ProjectedFields.Select(CopyField).ToList()
                };
                result.Tables.Add(tableResult);
                resultTables.Add(tableResult.Name, tableResult);
            }

            var matches = new List<QueryMatch>(limit + 1);
            var requiredSkip = cursor?.Offset ?? 0L;
            var skippedMatches = 0L;
            foreach (var tablePlan in plan.Tables)
            {
                foreach (var row in tablePlan.Table.Rows.OrderBy(item => item.Key, NameComparer))
                {
                    if (tablePlan.RowKeys != null && !tablePlan.RowKeys.Contains(row.Key)
                        || !tablePlan.Predicates.All(predicate => Matches(row, predicate)))
                    {
                        continue;
                    }

                    if (skippedMatches < requiredSkip)
                    {
                        skippedMatches++;
                        continue;
                    }

                    matches.Add(new QueryMatch(tablePlan, row));
                    if (matches.Count > limit)
                    {
                        break;
                    }
                }

                if (matches.Count > limit)
                {
                    break;
                }
            }

            if (cursor != null && (skippedMatches != requiredSkip || matches.Count == 0))
            {
                return Failure(databasePath, GameDBQueryFailureKind.InvalidCursor,
                    "cursor.positionInvalid", "The query cursor position is invalid.", snapshot.Revision);
            }

            var page = matches.Take(limit).ToList();
            foreach (var match in page)
            {
                resultTables[match.Plan.Table.Name].Rows.Add(ProjectRow(match.Row, match.Plan.ProjectedFields));
            }

            result.ReturnedRowCount = page.Count;
            result.HasMore = matches.Count > limit;
            if (result.HasMore)
            {
                result.NextCursor = GameDBQueryCursorCodec.Encode(new QueryCursor
                {
                    DatabasePathHash = Sha256(databasePath),
                    Revision = snapshot.Revision,
                    QueryHash = plan.QueryHash,
                    Offset = requiredSkip + page.Count
                });
            }

            return result;
        }

        private static bool Matches(GameDBRowSnapshot row, QueryPredicatePlan predicate)
        {
            object stored;
            if (!row.Values.TryGetValue(predicate.Field.Name, out stored))
            {
                throw new InvalidOperationException($"Row {row.Key} is missing field {predicate.Field.Name}.");
            }

            switch (predicate.Kind)
            {
                case GameDBQueryPredicateKind.Equals:
                    return WireEquals(predicate.Field, stored, predicate.Value);
                case GameDBQueryPredicateKind.Contains:
                    return Contains(predicate.Field, stored, predicate.Value);
                case GameDBQueryPredicateKind.NumericRange:
                    var number = NormalizeScalar(predicate.Field.FieldType, stored);
                    if (predicate.Field.FieldType == FieldType.@int
                        || predicate.Field.FieldType == FieldType.@long)
                    {
                        var integer = (long)number;
                        return (predicate.Minimum == null || integer >= (long)predicate.Minimum)
                            && (predicate.Maximum == null || integer <= (long)predicate.Maximum);
                    }

                    var floatingPoint = (double)number;
                    return (predicate.Minimum == null || floatingPoint >= (double)predicate.Minimum)
                        && (predicate.Maximum == null || floatingPoint <= (double)predicate.Maximum);
                case GameDBQueryPredicateKind.ReferencesRow:
                    return ReferencesRow(predicate.Field, stored, (string)predicate.Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool WireEquals(GameDBFieldSnapshot field, object stored, object expected)
        {
            if (field.FieldType == FieldType.unityObject)
            {
                var actualReference = (UnityObjectReference)stored;
                var expectedReference = (UnityObjectReference)expected;
                return actualReference.IsEmpty || expectedReference.IsEmpty
                    ? actualReference.IsEmpty && expectedReference.IsEmpty
                    : string.Equals(actualReference.Guid, expectedReference.Guid,
                        StringComparison.Ordinal);
            }

            var actual = NormalizeScalar(field.FieldType, stored);
            if (actual == null || expected == null)
            {
                return actual == null && expected == null;
            }

            if (field.FieldType == FieldType.@int || field.FieldType == FieldType.@long)
            {
                return (long)actual == (long)expected;
            }

            if (field.FieldType == FieldType.@float || field.FieldType == FieldType.@double)
            {
                return ((double)actual).Equals((double)expected);
            }

            return Equals(actual, expected);
        }

        private static bool Contains(GameDBFieldSnapshot field, object stored, object expected)
        {
            if (field.FieldType == FieldType.dictionary)
            {
                var dictionary = (IDictionary)stored;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (string.Equals(NormalizeDictionaryKey(field.DictionaryType, entry.Key),
                        (string)expected, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (field.IsArray)
            {
                var scalar = CopyAsScalar(field);
                foreach (var item in (IEnumerable)stored)
                {
                    if (WireEquals(scalar, item, expected))
                    {
                        return true;
                    }
                }
                return false;
            }

            return ((string)NormalizeScalar(field.FieldType, stored))
                .IndexOf((string)expected, StringComparison.Ordinal) >= 0;
        }

        private static bool ReferencesRow(GameDBFieldSnapshot field, object stored, string rowKey)
        {
            if (field.FieldType == FieldType.dictionary)
            {
                foreach (DictionaryEntry entry in (IDictionary)stored)
                {
                    if (string.Equals((string)NormalizeScalar(FieldType.tableRef, entry.Value), rowKey,
                        StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (field.IsArray)
            {
                return ((IEnumerable)stored).Cast<object>().Any(item =>
                    string.Equals((string)NormalizeScalar(FieldType.tableRef, item), rowKey,
                        StringComparison.Ordinal));
            }

            return string.Equals((string)NormalizeScalar(FieldType.tableRef, stored), rowKey,
                StringComparison.Ordinal);
        }

        private static GameDBQueryRowResult ProjectRow(GameDBRowSnapshot row,
            IReadOnlyList<GameDBFieldSnapshot> fields)
        {
            var result = new GameDBQueryRowResult { Key = row.Key };
            foreach (var field in fields)
            {
                object value;
                if (!row.Values.TryGetValue(field.Name, out value))
                {
                    throw new InvalidOperationException($"Row {row.Key} is missing field {field.Name}.");
                }

                result.Values.Add(field.Name, NormalizeValue(field, value));
            }
            return result;
        }

        private static object NormalizeValue(GameDBFieldSnapshot field, object value)
        {
            if (field.FieldType == FieldType.dictionary)
            {
                var entries = new List<KeyValuePair<string, object>>();
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    entries.Add(new KeyValuePair<string, object>(
                        NormalizeDictionaryKey(field.DictionaryType, entry.Key),
                        NormalizeScalar(field.DictionaryType.ValueType, entry.Value)));
                }

                var dictionary = new Dictionary<string, object>();
                foreach (var entry in entries.OrderBy(item => item.Key, NameComparer))
                {
                    dictionary.Add(entry.Key, entry.Value);
                }
                return dictionary;
            }

            if (field.IsArray)
            {
                return ((IEnumerable)value).Cast<object>()
                    .Select(item => NormalizeScalar(field.FieldType, item)).ToList();
            }

            return NormalizeScalar(field.FieldType, value);
        }

        private static object NormalizeScalar(FieldType type, object value)
        {
            if (value == null || type == FieldType.tableRef && value is string reference
                && (reference.Length == 0 || reference == FieldBase.NullRefToken))
            {
                return null;
            }

            switch (type)
            {
                case FieldType.@enum:
                case FieldType.color:
                    return value.ToString();
                case FieldType.vector2:
                    var vector2 = (Vector2)value;
                    return FormatVector(vector2.x, vector2.y);
                case FieldType.vector3:
                    var vector3 = (Vector3)value;
                    return FormatVector(vector3.x, vector3.y, vector3.z);
                case FieldType.vector4:
                    var vector4 = (Vector4)value;
                    return FormatVector(vector4.x, vector4.y, vector4.z, vector4.w);
                case FieldType.unityObject:
                    return UnityObjectReferenceWire.Serialize((UnityObjectReference)value);
                case FieldType.@int:
                    long normalizedInt;
                    if (!TryInt32(value, out normalizedInt))
                    {
                        throw new InvalidOperationException("Stored int value is not an Int32.");
                    }
                    return normalizedInt;
                case FieldType.@long:
                    long normalizedLong;
                    if (!TryInt64(value, out normalizedLong))
                    {
                        throw new InvalidOperationException("Stored long value is not an Int64.");
                    }
                    return normalizedLong;
                case FieldType.@float:
                    double normalizedFloat;
                    if (!TryFiniteSingle(value, out normalizedFloat))
                    {
                        throw new InvalidOperationException("Stored float value is not a finite Single.");
                    }
                    return normalizedFloat;
                case FieldType.@double:
                    double normalizedDouble;
                    if (!TryFiniteDouble(value, out normalizedDouble))
                    {
                        throw new InvalidOperationException("Stored double value is not a finite Double.");
                    }
                    return normalizedDouble;
                default:
                    return value;
            }
        }

        private static string NormalizeDictionaryKey(GameDBDictionaryTypeDefinition type, object value)
        {
            return value?.ToString();
        }

        private static GameDBFieldSnapshot CopyField(GameDBFieldSnapshot field)
        {
            return new GameDBFieldSnapshot
            {
                Name = field.Name,
                FieldType = field.FieldType,
                IsArray = field.IsArray,
                TypeArgument = field.TypeArgument,
                DictionaryType = field.DictionaryType == null ? null : new GameDBDictionaryTypeDefinition
                {
                    KeyType = field.DictionaryType.KeyType,
                    KeyTypeArgument = field.DictionaryType.KeyTypeArgument,
                    ValueType = field.DictionaryType.ValueType,
                    ValueTypeArgument = field.DictionaryType.ValueTypeArgument
                }
            };
        }

        private static GameDBFieldSnapshot CopyAsScalar(GameDBFieldSnapshot field)
        {
            var copy = CopyField(field);
            copy.IsArray = false;
            return copy;
        }

        private static string ComputeQueryHash(IEnumerable<QueryTablePlan> plans)
        {
            var builder = new StringBuilder("gamedb-query-v1");
            foreach (var plan in plans)
            {
                Append(builder, plan.Table.Name);
                AppendSet(builder, plan.RowKeys);
                AppendSet(builder, plan.ProjectedFields.Select(field => field.Name));
                foreach (var predicate in plan.Predicates.Select(CanonicalPredicate)
                    .OrderBy(value => value, NameComparer))
                {
                    Append(builder, predicate);
                }
            }
            return Sha256(builder.ToString());
        }

        private static string CanonicalPredicate(QueryPredicatePlan predicate)
        {
            var builder = new StringBuilder();
            Append(builder, predicate.Kind.ToString());
            Append(builder, predicate.Field.Name);
            Append(builder, CanonicalValue(predicate.Field.FieldType, predicate.Value));
            Append(builder, CanonicalValue(predicate.Field.FieldType, predicate.Minimum));
            Append(builder, CanonicalValue(predicate.Field.FieldType, predicate.Maximum));
            return builder.ToString();
        }

        private static string CanonicalValue(FieldType type, object value)
        {
            if (value == null) return "null";
            if (value is bool boolean) return boolean ? "bool:true" : "bool:false";
            if (value is long integer)
            {
                return (type == FieldType.@long ? "long:" : "int:")
                    + integer.ToString(CultureInfo.InvariantCulture);
            }
            if (value is double number)
            {
                return (type == FieldType.@double ? "double:" : "float:")
                    + number.ToString(type == FieldType.@double ? "G17" : "R",
                        CultureInfo.InvariantCulture);
            }
            if (value is UnityObjectReference reference)
            {
                return "unityObject:" + reference.Guid + ":" + reference.Path;
            }
            return "string:" + value;
        }

        private static void AppendSet(StringBuilder builder, IEnumerable<string> values)
        {
            builder.Append('[');
            if (values != null)
            {
                foreach (var value in values.OrderBy(item => item, NameComparer))
                {
                    Append(builder, value);
                }
            }
            builder.Append(']');
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value);
        }

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
            }
        }

        private static bool TryInt32(object value, out long result)
        {
            result = 0;
            if (!NumericValue.TryNormalizeInt32(value, out var normalized))
            {
                return false;
            }

            result = normalized;
            return true;
        }

        private static bool TryInt64(object value, out long result)
        {
            return NumericValue.TryNormalizeInt64(value, out result);
        }

        private static bool TryFiniteSingle(object value, out double result)
        {
            result = 0;
            if (!NumericValue.TryNormalizeSingle(value, out var normalized))
            {
                return false;
            }

            result = normalized;
            return true;
        }

        private static bool TryFiniteDouble(object value, out double result)
        {
            return NumericValue.TryNormalizeDouble(value, out result);
        }

        private static bool IsEnumName(string typeName, string value)
        {
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(value)) return false;
            AssemblyExplorer.Instance.Load();
            var type = AssemblyExplorer.Instance.GetType(typeName);
            return type != null && type.IsEnum && Enum.GetNames(type).Contains(value);
        }

        private static bool IsColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var hex = value.Replace("0x", string.Empty).Replace("#", string.Empty);
            if (hex.Length != 6 && hex.Length != 8) return false;
            byte parsed;
            return Enumerable.Range(0, hex.Length / 2).All(index =>
                byte.TryParse(hex.Substring(index * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out parsed));
        }

        private static bool TryNormalizeVector(FieldType type, object value, out string normalized)
        {
            normalized = null;
            if (!(value is string text)) return false;

            var componentCount = type == FieldType.vector2 ? 2
                : type == FieldType.vector3 ? 3
                : type == FieldType.vector4 ? 4
                : 0;
            var parts = text.Split(',');
            if (componentCount == 0 || parts.Length != componentCount)
            {
                return false;
            }

            var components = new float[componentCount];
            for (var index = 0; index < parts.Length; index++)
            {
                float component;
                if (!float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out component) || float.IsNaN(component) || float.IsInfinity(component))
                {
                    return false;
                }
                components[index] = component;
            }

            normalized = FormatVector(components);
            return true;
        }

        private static string FormatVector(params float[] components)
        {
            if (components.Any(component => float.IsNaN(component) || float.IsInfinity(component)))
            {
                throw new InvalidOperationException("Stored vector components must be finite.");
            }

            return string.Join(",", components.Select(component =>
                component.ToString("R", CultureInfo.InvariantCulture)).ToArray());
        }

        private static GameDBQueryResult PredicateFailure(string databasePath, string revision,
            string code, string message, int projectionIndex, int predicateIndex,
            string tableName, string fieldName)
        {
            return Failure(databasePath, GameDBQueryFailureKind.InvalidRequest, code, message,
                revision, projectionIndex, predicateIndex, tableName, fieldName);
        }

        internal static GameDBQueryResult Failure(string databasePath, GameDBQueryFailureKind kind,
            string code, string message, string revision = null, int projectionIndex = -1,
            int predicateIndex = -1, string tableName = null, string fieldName = null)
        {
            return new GameDBQueryResult
            {
                Success = false,
                FailureKind = kind,
                DatabasePath = databasePath,
                Message = message,
                Revision = revision,
                Errors = new List<GameDBQueryError>
                {
                    new GameDBQueryError
                    {
                        Code = code,
                        Message = message,
                        ProjectionIndex = projectionIndex,
                        PredicateIndex = predicateIndex,
                        TableName = tableName,
                        FieldName = fieldName
                    }
                }
            };
        }

        private sealed class QueryPlan
        {
            internal IReadOnlyList<QueryTablePlan> Tables { get; }
            internal string QueryHash { get; }

            internal QueryPlan(IReadOnlyList<QueryTablePlan> tables, string queryHash)
            {
                Tables = tables;
                QueryHash = queryHash;
            }
        }

        private sealed class QueryTablePlan
        {
            internal GameDBTableSnapshot Table { get; }
            internal HashSet<string> RowKeys { get; }
            internal IReadOnlyList<GameDBFieldSnapshot> ProjectedFields { get; }
            internal IReadOnlyList<QueryPredicatePlan> Predicates { get; }

            internal QueryTablePlan(GameDBTableSnapshot table, HashSet<string> rowKeys,
                IReadOnlyList<GameDBFieldSnapshot> fields,
                IReadOnlyList<QueryPredicatePlan> predicates)
            {
                Table = table;
                RowKeys = rowKeys;
                ProjectedFields = fields;
                Predicates = predicates;
            }
        }

        private sealed class QueryPredicatePlan
        {
            internal GameDBQueryPredicateKind Kind { get; }
            internal GameDBFieldSnapshot Field { get; }
            internal object Value { get; }
            internal object Minimum { get; }
            internal object Maximum { get; }

            internal QueryPredicatePlan(GameDBQueryPredicateKind kind, GameDBFieldSnapshot field,
                object value, object minimum, object maximum)
            {
                Kind = kind;
                Field = field;
                Value = value;
                Minimum = minimum;
                Maximum = maximum;
            }
        }

        private sealed class QueryMatch
        {
            internal QueryTablePlan Plan { get; }
            internal GameDBRowSnapshot Row { get; }

            internal QueryMatch(QueryTablePlan plan, GameDBRowSnapshot row)
            {
                Plan = plan;
                Row = row;
            }
        }
    }

    internal sealed class QueryCursor
    {
        internal string DatabasePathHash;
        internal string Revision;
        internal string QueryHash;
        internal long Offset;
    }

    internal static class GameDBQueryCursorCodec
    {
        private const int Version = 2;
        private const int AuthenticationTagLength = 32;
        private const int MaximumEncodedLength = 4096;
        private const string AuthenticationKeyName = "GameDB.Query.Cursor.AuthenticationKey.v1";
        private const string Domain = "GameDB.Query.Cursor";
        private static readonly byte[] AuthenticationKey = LoadOrCreateAuthenticationKey();

        internal static string Encode(QueryCursor cursor)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Domain);
                writer.Write(Version);
                writer.Write(cursor.DatabasePathHash);
                writer.Write(cursor.Revision);
                writer.Write(cursor.QueryHash);
                writer.Write(cursor.Offset);
                writer.Flush();
                payload = stream.ToArray();
            }

            byte[] authenticationTag;
            using (var algorithm = new HMACSHA256(AuthenticationKey))
            {
                authenticationTag = algorithm.ComputeHash(payload);
            }

            var complete = new byte[payload.Length + authenticationTag.Length];
            Buffer.BlockCopy(payload, 0, complete, 0, payload.Length);
            Buffer.BlockCopy(authenticationTag, 0, complete, payload.Length, authenticationTag.Length);
            return ToBase64Url(complete);
        }

        internal static bool TryDecode(string value, out QueryCursor cursor, out string errorCode)
        {
            cursor = null;
            errorCode = "cursor.invalid";
            try
            {
                if (string.IsNullOrEmpty(value) || value.Length > MaximumEncodedLength)
                {
                    return false;
                }

                var padded = value.Replace('-', '+').Replace('_', '/');
                switch (padded.Length % 4)
                {
                    case 2: padded += "=="; break;
                    case 3: padded += "="; break;
                    case 1: return false;
                }

                var complete = Convert.FromBase64String(padded);
                if (!string.Equals(ToBase64Url(complete), value, StringComparison.Ordinal)
                    || complete.Length <= AuthenticationTagLength)
                {
                    return false;
                }
                var payloadLength = complete.Length - AuthenticationTagLength;
                var payload = new byte[payloadLength];
                var actualAuthenticationTag = new byte[AuthenticationTagLength];
                Buffer.BlockCopy(complete, 0, payload, 0, payloadLength);
                Buffer.BlockCopy(complete, payloadLength, actualAuthenticationTag, 0,
                    AuthenticationTagLength);
                byte[] expectedAuthenticationTag;
                using (var algorithm = new HMACSHA256(AuthenticationKey))
                {
                    expectedAuthenticationTag = algorithm.ComputeHash(payload);
                }

                if (!FixedTimeEquals(actualAuthenticationTag, expectedAuthenticationTag))
                {
                    errorCode = "cursor.tampered";
                    return false;
                }

                using (var stream = new MemoryStream(payload))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (reader.ReadString() != Domain || reader.ReadInt32() != Version)
                    {
                        return false;
                    }

                    cursor = new QueryCursor
                    {
                        DatabasePathHash = reader.ReadString(),
                        Revision = reader.ReadString(),
                        QueryHash = reader.ReadString(),
                        Offset = reader.ReadInt64()
                    };
                    if (stream.Position != stream.Length
                        || string.IsNullOrWhiteSpace(cursor.DatabasePathHash)
                        || string.IsNullOrWhiteSpace(cursor.Revision)
                        || string.IsNullOrWhiteSpace(cursor.QueryHash)
                        || cursor.Offset < 1)
                    {
                        cursor = null;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                cursor = null;
                return false;
            }
        }

        private static string ToBase64Url(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] LoadOrCreateAuthenticationKey()
        {
            var encoded = SessionState.GetString(AuthenticationKeyName, string.Empty);
            if (!string.IsNullOrEmpty(encoded))
            {
                try
                {
                    var existing = Convert.FromBase64String(encoded);
                    if (existing.Length == AuthenticationTagLength)
                    {
                        return existing;
                    }
                }
                catch (FormatException)
                {
                }
            }

            var created = new byte[AuthenticationTagLength];
            using (var algorithm = RandomNumberGenerator.Create())
            {
                algorithm.GetBytes(created);
            }
            SessionState.SetString(AuthenticationKeyName, Convert.ToBase64String(created));
            return created;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }
    }
}
