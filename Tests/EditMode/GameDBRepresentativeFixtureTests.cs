using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public static class GameDBRepresentativeFixture
    {
        public const int DefaultTableCount = 3;
        public const int DefaultRowsPerTable = 300;
        public const int DefaultFieldsPerTable = 24;

        private const string AssetPath = "Assets/GameDBRepresentativeFixture/database.json";
        private const string FixtureGuid = "0123456789abcdef0123456789abcdef";
        private const string FixtureObjectPath = "Assets/GameDBRepresentativeFixture/Icon.asset";
        private static GameDBSerializedState s_defaultState;

        internal static GameDBDocument CreateDocument(
            int tableCount = DefaultTableCount,
            int rowsPerTable = DefaultRowsPerTable,
            int fieldsPerTable = DefaultFieldsPerTable,
            string documentId = null)
        {
            if (tableCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tableCount));
            }

            if (rowsPerTable <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowsPerTable));
            }

            if (fieldsPerTable <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldsPerTable));
            }

            var state = tableCount == DefaultTableCount
                && rowsPerTable == DefaultRowsPerTable
                && fieldsPerTable == DefaultFieldsPerTable
                    ? s_defaultState ?? (s_defaultState = CreateState(
                        tableCount, rowsPerTable, fieldsPerTable))
                    : CreateState(tableCount, rowsPerTable, fieldsPerTable);

            return GameDBDocument.RestoreState(new GameDBDocumentState
            {
                DocumentId = documentId ?? Guid.NewGuid().ToString("N"),
                AssetPath = AssetPath,
                DataJson = state.DataJson,
                SchemaJson = state.SchemaJson,
                BaselineRevision = state.Revision,
                BaselineDiskToken = GameDBDiskToken.Absent,
                WasDirty = false
            });
        }

        public static object CreateDocumentForEditorUiTests(
            int tableCount = DefaultTableCount,
            int rowsPerTable = DefaultRowsPerTable,
            int fieldsPerTable = DefaultFieldsPerTable,
            string documentId = null)
        {
            return CreateDocument(tableCount, rowsPerTable, fieldsPerTable, documentId);
        }

        internal static string CreateUncachedRevision(
            int tableCount, int rowsPerTable, int fieldsPerTable)
        {
            return CreateState(tableCount, rowsPerTable, fieldsPerTable).Revision;
        }

        private static GameDBSerializedState CreateState(
            int tableCount, int rowsPerTable, int fieldsPerTable)
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory("GameDBRepresentativeFixture/database.json");
            gameDB.ScopeName = "RepresentativeFixture";

            for (var tableIndex = 0; tableIndex < tableCount; tableIndex++)
            {
                var tableName = TableName(tableIndex);
                if (!gameDB.AddTable(tableName, KeyType.@string))
                {
                    throw new InvalidOperationException($"Could not add table {tableName}.");
                }

                var table = (TableModel)gameDB.Tables[tableName];
                for (var fieldIndex = 0; fieldIndex < fieldsPerTable; fieldIndex++)
                {
                    var fieldName = FieldName(fieldIndex);
                    var definition = GetFieldDefinition(fieldIndex, tableName);
                    if (!table.AddField(fieldName, definition.Type,
                        definition.IsArray, definition.TypeArgument))
                    {
                        throw new InvalidOperationException(
                            $"Could not add field {tableName}.{fieldName}.");
                    }
                }

                for (var rowIndex = 0; rowIndex < rowsPerTable; rowIndex++)
                {
                    var rowKey = RowKey(rowIndex);
                    if (!table.AddKey(rowKey))
                    {
                        throw new InvalidOperationException(
                            $"Could not add row {tableName}.{rowKey}.");
                    }
                }
            }

            for (var tableIndex = 0; tableIndex < tableCount; tableIndex++)
            {
                var tableName = TableName(tableIndex);
                var table = (TableModel)gameDB.Tables[tableName];
                for (var rowIndex = 0; rowIndex < rowsPerTable; rowIndex++)
                {
                    var rowKey = RowKey(rowIndex);
                    for (var fieldIndex = 0; fieldIndex < fieldsPerTable; fieldIndex++)
                    {
                        var fieldName = FieldName(fieldIndex);
                        if (!table.SetValue(rowKey, fieldName,
                            ValueFor(fieldIndex, tableIndex, rowIndex)))
                        {
                            throw new InvalidOperationException(
                                $"Could not set {tableName}.{rowKey}.{fieldName}.");
                        }
                    }
                }
            }

            return GameDBModelCodec.Serialize(gameDB);
        }

        private static FieldDefinition GetFieldDefinition(int fieldIndex, string tableName)
        {
            switch (fieldIndex % 24)
            {
                case 0: return new FieldDefinition(FieldType.@string);
                case 1: return new FieldDefinition(FieldType.@int);
                case 2: return new FieldDefinition(FieldType.@bool);
                case 3: return new FieldDefinition(FieldType.@float);
                case 4: return new FieldDefinition(FieldType.@long);
                case 5: return new FieldDefinition(FieldType.@double);
                case 6: return new FieldDefinition(FieldType.color);
                case 7: return new FieldDefinition(FieldType.vector2);
                case 8: return new FieldDefinition(FieldType.vector3);
                case 9: return new FieldDefinition(FieldType.vector4);
                case 10: return new FieldDefinition(FieldType.@string);
                case 11: return new FieldDefinition(FieldType.tableRef, false, tableName);
                case 12: return new FieldDefinition(FieldType.unityObject);
                case 13: return new FieldDefinition(FieldType.@string, true);
                case 14: return new FieldDefinition(FieldType.@long, true);
                case 15: return new FieldDefinition(FieldType.tableRef, true, tableName);
                case 16:
                    return new FieldDefinition(FieldType.dictionary, false,
                        new DictionaryType(KeyType.@string, null, FieldType.@int, null));
                case 17:
                    return new FieldDefinition(FieldType.dictionary, false,
                        new DictionaryType(KeyType.@string, null, FieldType.tableRef, tableName));
                case 18: return new FieldDefinition(FieldType.unityObject, true);
                case 19:
                    return new FieldDefinition(FieldType.dictionary, false,
                        new DictionaryType(KeyType.@string, null, FieldType.unityObject, null));
                case 20: return new FieldDefinition(FieldType.color, true);
                case 21: return new FieldDefinition(FieldType.vector2, true);
                case 22:
                    return new FieldDefinition(FieldType.dictionary, false,
                        new DictionaryType(KeyType.@string, null, FieldType.@double, null));
                default: return new FieldDefinition(FieldType.@double, true);
            }
        }

        private static object ValueFor(int fieldIndex, int tableIndex, int rowIndex)
        {
            switch (fieldIndex % 24)
            {
                case 0: return $"Value {tableIndex:D2}-{rowIndex:D4}-{fieldIndex:D2}";
                case 1: return tableIndex * 100000 + rowIndex * 100 + fieldIndex;
                case 2: return (tableIndex + rowIndex + fieldIndex) % 2 == 0;
                case 3: return (float)(rowIndex / 10.0 + fieldIndex / 100.0);
                case 4: return 9007199254740991L - rowIndex - tableIndex;
                case 5: return tableIndex + rowIndex / 10.0 + fieldIndex / 100.0;
                case 6: return "#10203040";
                case 7: return $"{rowIndex},{fieldIndex}";
                case 8: return $"{tableIndex},{rowIndex},{fieldIndex}";
                case 9: return $"{tableIndex},{rowIndex},{fieldIndex},1";
                case 10: return $"Label {tableIndex:D2}-{rowIndex:D4}";
                case 11: return rowIndex % 5 == 0 ? null : RowKey(0);
                case 12: return UnityObjectWire(rowIndex);
                case 13: return new List<object> { "alpha", $"row-{rowIndex:D4}" };
                case 14: return new List<object> { (long)rowIndex, long.MaxValue - rowIndex };
                case 15:
                    return new List<object> { null, RowKey(rowIndex) };
                case 16:
                    return new Dictionary<string, object> { { "Power", rowIndex } };
                case 17:
                    return new Dictionary<string, object>
                    {
                        { "Primary", rowIndex % 5 == 0 ? null : RowKey(0) }
                    };
                case 18:
                    return new List<object> { UnityObjectWire(rowIndex) };
                case 19:
                    return new Dictionary<string, object>
                    {
                        { "Primary", UnityObjectWire(rowIndex) }
                    };
                case 20: return new List<object> { "#10203040", "#50607080" };
                case 21: return new List<object> { "1,2", $"{rowIndex},{tableIndex}" };
                case 22:
                    return new Dictionary<string, object> { { "Weight", rowIndex / 10.0 } };
                default: return new List<object> { rowIndex / 10.0, tableIndex + 0.5 };
            }
        }

        private static Dictionary<string, object> UnityObjectWire(int rowIndex)
        {
            var populated = rowIndex % 2 != 0;
            return new Dictionary<string, object>
            {
                { "guid", populated ? FixtureGuid : string.Empty },
                { "path", populated ? FixtureObjectPath : string.Empty }
            };
        }

        private static string TableName(int tableIndex) => $"Table{tableIndex:D2}";
        private static string FieldName(int fieldIndex) => $"Field{fieldIndex:D2}";
        private static string RowKey(int rowIndex) => $"Row{rowIndex:D4}";

        private sealed class FieldDefinition
        {
            internal FieldType Type { get; }
            internal bool IsArray { get; }
            internal object TypeArgument { get; }

            internal FieldDefinition(FieldType type, bool isArray = false,
                object typeArgument = null)
            {
                Type = type;
                IsArray = isArray;
                TypeArgument = typeArgument;
            }
        }
    }

    public class GameDBRepresentativeFixtureTests
    {
        [Test]
        public void DefaultFixture_ProvidesLargeMixedTypeDatabase()
        {
            var document = GameDBRepresentativeFixture.CreateDocument(documentId: "fixture-a");
            var snapshot = document.CreateSnapshot();
            var firstUncachedRevision = GameDBRepresentativeFixture.CreateUncachedRevision(1, 3, 24);
            var secondUncachedRevision = GameDBRepresentativeFixture.CreateUncachedRevision(1, 3, 24);

            Assert.That(document.CurrentRevision, Is.EqualTo(document.BaselineRevision));
            Assert.That(secondUncachedRevision, Is.EqualTo(firstUncachedRevision));
            Assert.That(snapshot.ScopeName, Is.EqualTo("RepresentativeFixture"));
            Assert.That(snapshot.Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "Table00", "Table01", "Table02" }));
            Assert.That(snapshot.Tables, Has.Count.EqualTo(
                GameDBRepresentativeFixture.DefaultTableCount));
            Assert.That(snapshot.Tables.All(table => table.Fields.Count
                == GameDBRepresentativeFixture.DefaultFieldsPerTable), Is.True);
            Assert.That(snapshot.Tables.All(table => table.Rows.Count
                == GameDBRepresentativeFixture.DefaultRowsPerTable), Is.True);
            var expectedTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                .Where(type => type != FieldType.@enum);
            Assert.That(snapshot.Tables[0].Fields.Select(field => field.FieldType).Distinct(),
                Is.EquivalentTo(expectedTypes));

            var row = snapshot.Tables[2].Rows[123];
            Assert.That(row.Key, Is.EqualTo("Row0123"));
            Assert.That(row.Values["Field00"], Is.EqualTo("Value 02-0123-00"));
            Assert.That(row.Values["Field01"], Is.EqualTo(212301));
            Assert.That(row.Values["Field04"], Is.EqualTo(9007199254740866L));
            Assert.That(row.Values["Field11"], Is.EqualTo("Row0000"));
            var objectReference = (UnityObjectReference)row.Values["Field12"];
            Assert.That(objectReference.Guid, Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(objectReference.Path,
                Is.EqualTo("Assets/GameDBRepresentativeFixture/Icon.asset"));
            Assert.That(((IEnumerable<object>)row.Values["Field14"]).Last(),
                Is.EqualTo(long.MaxValue - 123));
            Assert.That(((Dictionary<object, object>)row.Values["Field17"])["Primary"],
                Is.EqualTo("Row0000"));
        }
    }
}
