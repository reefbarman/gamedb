using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Workspace;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBTableViewProjection
    {
        internal const string KeyFieldId = "$gamedb.row-key";
        private readonly GameDBTableSnapshot[] m_tables;
        private readonly GameDBRowSnapshot[] m_rows;

        internal IReadOnlyList<GameDBTableSnapshot> Tables { get; }
        internal GameDBTableSnapshot SelectedTable { get; }
        internal IReadOnlyList<GameDBRowSnapshot> Rows { get; }
        internal IReadOnlyList<GameDBWorkspaceSortState> Sorts { get; }

        internal GameDBTableViewProjection(GameDBSnapshot snapshot,
            string selectedTableId, string searchText = null,
            IEnumerable<GameDBWorkspaceSortState> sorts = null)
        {
            m_tables = (snapshot?.Tables ?? new List<GameDBTableSnapshot>()).ToArray();
            Tables = new ReadOnlyCollection<GameDBTableSnapshot>(m_tables);
            SelectedTable = m_tables.FirstOrDefault(table => table.Name == selectedTableId)
                ?? m_tables.FirstOrDefault();
            var sanitizedSorts = SanitizeSorts(SelectedTable, sorts);
            var fieldsById = (SelectedTable?.Fields ?? new List<GameDBFieldSnapshot>())
                .ToDictionary(field => field.Name, StringComparer.Ordinal);
            Sorts = new ReadOnlyCollection<GameDBWorkspaceSortState>(sanitizedSorts);
            var normalizedSearch = searchText?.Trim() ?? string.Empty;
            var rows = (SelectedTable?.Rows ?? new List<GameDBRowSnapshot>())
                .Where(row => MatchesSearch(row, SelectedTable, normalizedSearch));
            m_rows = rows.OrderBy(row => row,
                    Comparer<GameDBRowSnapshot>.Create((first, second) =>
                        CompareRows(fieldsById, first, second, sanitizedSorts)))
                .ToArray();
            Rows = new ReadOnlyCollection<GameDBRowSnapshot>(m_rows);
        }

        internal bool ContainsSourceRow(string rowKey)
        {
            return SelectedTable != null && !string.IsNullOrWhiteSpace(rowKey)
                && SelectedTable.Rows.Any(row => row.Key == rowKey);
        }

        internal int IndexOfRow(string rowKey)
        {
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                return -1;
            }
            for (var index = 0; index < m_rows.Length; index++)
            {
                if (m_rows[index].Key == rowKey)
                {
                    return index;
                }
            }
            return -1;
        }

        private static GameDBWorkspaceSortState[] SanitizeSorts(
            GameDBTableSnapshot table, IEnumerable<GameDBWorkspaceSortState> sorts)
        {
            if (table == null)
            {
                return Array.Empty<GameDBWorkspaceSortState>();
            }
            var valid = new HashSet<string>(table.Fields.Select(field => field.Name),
                StringComparer.Ordinal) { KeyFieldId };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return (sorts ?? Array.Empty<GameDBWorkspaceSortState>())
                .Where(sort => sort != null && valid.Contains(sort.FieldId)
                    && seen.Add(sort.FieldId))
                .Select(sort => new GameDBWorkspaceSortState(
                    sort.FieldId, sort.Descending))
                .ToArray();
        }

        private static bool MatchesSearch(GameDBRowSnapshot row,
            GameDBTableSnapshot table, string searchText)
        {
            if (searchText.Length == 0
                || Contains(row.Key, searchText))
            {
                return true;
            }
            foreach (var field in table.Fields)
            {
                if (row.Values.TryGetValue(field.Name, out var value)
                    && Contains(FormatValue(value), searchText))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Contains(string value, string searchText)
        {
            return value?.IndexOf(searchText,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareRows(
            IReadOnlyDictionary<string, GameDBFieldSnapshot> fieldsById,
            GameDBRowSnapshot first, GameDBRowSnapshot second,
            IReadOnlyList<GameDBWorkspaceSortState> sorts)
        {
            foreach (var sort in sorts)
            {
                var comparison = sort.FieldId == KeyFieldId
                    ? (sort.Descending
                        ? CompareRowKeys(second.Key, first.Key)
                        : CompareRowKeys(first.Key, second.Key))
                    : CompareValues(
                        fieldsById.TryGetValue(sort.FieldId, out var field) ? field : null,
                        Value(first, sort.FieldId), Value(second, sort.FieldId),
                        sort.Descending);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            return CompareRowKeys(first.Key, second.Key);
        }


        private static object Value(GameDBRowSnapshot row, string fieldId)
        {
            return row.Values.TryGetValue(fieldId, out var value) ? value : null;
        }

        private static int CompareValues(GameDBFieldSnapshot field,
            object first, object second, bool descending)
        {
            if (ReferenceEquals(first, second))
            {
                return 0;
            }
            if (first == null)
            {
                return 1;
            }
            if (second == null)
            {
                return -1;
            }
            return descending
                ? CompareNonNullValues(field, second, first)
                : CompareNonNullValues(field, first, second);
        }

        private static int CompareNonNullValues(GameDBFieldSnapshot field,
            object first, object second)
        {
            if (field?.IsArray == true || field?.FieldType == GameDBLibrary.FieldType.dictionary)
            {
                return CollectionCount(first).CompareTo(CollectionCount(second));
            }
            switch (field?.FieldType)
            {
                case GameDBLibrary.FieldType.@bool:
                    return CompareTyped(first, second,
                        value => value is bool,
                        (firstValue, secondValue) => ((bool)firstValue)
                            .CompareTo((bool)secondValue));
                case GameDBLibrary.FieldType.@int:
                case GameDBLibrary.FieldType.@long:
                case GameDBLibrary.FieldType.@float:
                case GameDBLibrary.FieldType.@double:
                    return CompareTyped(first, second, IsNumber, CompareNumbers);
                case GameDBLibrary.FieldType.@enum:
                    return CompareTyped(first, second,
                        value => value is Enum,
                        (firstValue, secondValue) => Convert.ToInt64(firstValue,
                            CultureInfo.InvariantCulture).CompareTo(Convert.ToInt64(
                                secondValue, CultureInfo.InvariantCulture)));
                case GameDBLibrary.FieldType.tableRef:
                    return CompareRowKeys(first.ToString(), second.ToString());
                case GameDBLibrary.FieldType.unityObject:
                    return CompareTyped(first, second,
                        value => value is GameDBLibrary.UnityObjectReference,
                        (firstValue, secondValue) => CompareObjectReferences(
                            (GameDBLibrary.UnityObjectReference)firstValue,
                            (GameDBLibrary.UnityObjectReference)secondValue));
                case GameDBLibrary.FieldType.color:
                    return CompareTyped(first, second,
                        value => value is GameDBLibrary.Color,
                        (firstValue, secondValue) => CompareColors(
                            (GameDBLibrary.Color)firstValue,
                            (GameDBLibrary.Color)secondValue));
                case GameDBLibrary.FieldType.vector2:
                    return CompareTyped(first, second,
                        value => value is GameDBLibrary.Vector2,
                        (firstValue, secondValue) => CompareVectors(
                            (GameDBLibrary.Vector2)firstValue,
                            (GameDBLibrary.Vector2)secondValue));
                case GameDBLibrary.FieldType.vector3:
                    return CompareTyped(first, second,
                        value => value is GameDBLibrary.Vector3,
                        (firstValue, secondValue) => CompareVectors(
                            (GameDBLibrary.Vector3)firstValue,
                            (GameDBLibrary.Vector3)secondValue));
                case GameDBLibrary.FieldType.vector4:
                    return CompareTyped(first, second,
                        value => value is GameDBLibrary.Vector4,
                        (firstValue, secondValue) => CompareVectors(
                            (GameDBLibrary.Vector4)firstValue,
                            (GameDBLibrary.Vector4)secondValue));
            }
            return CompareText(FormatValue(first), FormatValue(second));
        }

        private static int CompareTyped(object first, object second,
            Func<object, bool> isExpectedType, Func<object, object, int> compare)
        {
            var firstExpected = isExpectedType(first);
            var secondExpected = isExpectedType(second);
            if (firstExpected != secondExpected)
            {
                return firstExpected ? -1 : 1;
            }
            return firstExpected
                ? compare(first, second)
                : CompareText(FormatValue(first), FormatValue(second));
        }

        private static int CompareObjectReferences(
            GameDBLibrary.UnityObjectReference first,
            GameDBLibrary.UnityObjectReference second)
        {
            var path = CompareText(first.Path, second.Path);
            return path != 0
                ? path
                : StringComparer.Ordinal.Compare(first.Guid, second.Guid);
        }

        private static int CompareNumbers(object first, object second)
        {
            try
            {
                if (IsNumber(first) && IsNumber(second))
                {
                    if (IsFloatingPoint(first) || IsFloatingPoint(second))
                    {
                        return Convert.ToDouble(first, CultureInfo.InvariantCulture)
                            .CompareTo(Convert.ToDouble(second, CultureInfo.InvariantCulture));
                    }
                    return Convert.ToDecimal(first, CultureInfo.InvariantCulture)
                        .CompareTo(Convert.ToDecimal(second, CultureInfo.InvariantCulture));
                }
            }
            catch (Exception exception) when (exception is FormatException
                || exception is InvalidCastException || exception is OverflowException)
            {
            }
            return CompareText(FormatValue(first), FormatValue(second));
        }

        private static int CollectionCount(object value)
        {
            if (value is ICollection collection)
            {
                return collection.Count;
            }
            if (!(value is IEnumerable enumerable))
            {
                return 0;
            }
            var count = 0;
            foreach (var ignored in enumerable)
            {
                count++;
            }
            return count;
        }

        private static int CompareColors(GameDBLibrary.Color first,
            GameDBLibrary.Color second)
        {
            return CompareComponents(first.r, second.r, first.g, second.g,
                first.b, second.b, first.a, second.a);
        }

        private static int CompareVectors(GameDBLibrary.Vector2 first,
            GameDBLibrary.Vector2 second)
        {
            return CompareComponents(first.x, second.x, first.y, second.y);
        }

        private static int CompareVectors(GameDBLibrary.Vector3 first,
            GameDBLibrary.Vector3 second)
        {
            return CompareComponents(first.x, second.x, first.y, second.y,
                first.z, second.z);
        }

        private static int CompareVectors(GameDBLibrary.Vector4 first,
            GameDBLibrary.Vector4 second)
        {
            return CompareComponents(first.x, second.x, first.y, second.y,
                first.z, second.z, first.w, second.w);
        }

        private static int CompareComponents(float firstX, float secondX,
            float firstY, float secondY)
        {
            var comparison = firstX.CompareTo(secondX);
            return comparison != 0 ? comparison : firstY.CompareTo(secondY);
        }

        private static int CompareComponents(float firstX, float secondX,
            float firstY, float secondY, float firstZ, float secondZ)
        {
            var comparison = CompareComponents(firstX, secondX, firstY, secondY);
            return comparison != 0 ? comparison : firstZ.CompareTo(secondZ);
        }

        private static int CompareComponents(float firstX, float secondX,
            float firstY, float secondY, float firstZ, float secondZ,
            float firstW, float secondW)
        {
            var comparison = CompareComponents(firstX, secondX,
                firstY, secondY, firstZ, secondZ);
            return comparison != 0 ? comparison : firstW.CompareTo(secondW);
        }

        private static int CompareText(string first, string second)
        {
            var insensitive = StringComparer.OrdinalIgnoreCase.Compare(first, second);
            return insensitive != 0
                ? insensitive
                : StringComparer.Ordinal.Compare(first, second);
        }

        private static bool IsFloatingPoint(object value)
        {
            var code = Type.GetTypeCode(value.GetType());
            return code == TypeCode.Double || code == TypeCode.Single;
        }

        private static bool IsNumber(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        private static int CompareRowKeys(string first, string second)
        {
            first = first ?? string.Empty;
            second = second ?? string.Empty;
            var firstIndex = 0;
            var secondIndex = 0;
            var caseTieBreak = 0;
            while (firstIndex < first.Length && secondIndex < second.Length)
            {
                var firstDigit = IsAsciiDigit(first[firstIndex]);
                var secondDigit = IsAsciiDigit(second[secondIndex]);
                if (firstDigit && secondDigit)
                {
                    var firstEnd = DigitRunEnd(first, firstIndex);
                    var secondEnd = DigitRunEnd(second, secondIndex);
                    var numeric = CompareDigitRuns(first, firstIndex, firstEnd,
                        second, secondIndex, secondEnd);
                    if (numeric != 0)
                    {
                        return numeric;
                    }
                    firstIndex = firstEnd;
                    secondIndex = secondEnd;
                    continue;
                }

                var firstCharacter = char.ToUpperInvariant(first[firstIndex]);
                var secondCharacter = char.ToUpperInvariant(second[secondIndex]);
                var character = firstCharacter.CompareTo(secondCharacter);
                if (character != 0)
                {
                    return character;
                }
                if (caseTieBreak == 0)
                {
                    caseTieBreak = first[firstIndex].CompareTo(second[secondIndex]);
                }
                firstIndex++;
                secondIndex++;
            }
            var remaining = first.Length - firstIndex - (second.Length - secondIndex);
            return remaining != 0 ? remaining : caseTieBreak;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static int DigitRunEnd(string value, int start)
        {
            var end = start;
            while (end < value.Length && IsAsciiDigit(value[end]))
            {
                end++;
            }
            return end;
        }

        private static int CompareDigitRuns(string first, int firstStart, int firstEnd,
            string second, int secondStart, int secondEnd)
        {
            var firstSignificant = firstStart;
            while (firstSignificant < firstEnd && first[firstSignificant] == '0')
            {
                firstSignificant++;
            }
            var secondSignificant = secondStart;
            while (secondSignificant < secondEnd && second[secondSignificant] == '0')
            {
                secondSignificant++;
            }
            var firstLength = firstEnd - firstSignificant;
            var secondLength = secondEnd - secondSignificant;
            if (firstLength != secondLength)
            {
                return firstLength.CompareTo(secondLength);
            }
            for (var offset = 0; offset < firstLength; offset++)
            {
                var digit = first[firstSignificant + offset]
                    .CompareTo(second[secondSignificant + offset]);
                if (digit != 0)
                {
                    return digit;
                }
            }
            return (firstEnd - firstStart).CompareTo(secondEnd - secondStart);
        }

        internal static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is string text)
            {
                return text;
            }
            if (value is bool boolean)
            {
                return boolean ? "true" : "false";
            }
            if (value is IDictionary dictionary)
            {
                return $"{dictionary.Count} entr{(dictionary.Count == 1 ? "y" : "ies")}";
            }
            if (value is IEnumerable enumerable)
            {
                var count = 0;
                foreach (var ignored in enumerable)
                {
                    count++;
                }
                return $"{count} item{(count == 1 ? string.Empty : "s")}";
            }
            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }
    }
}
