using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class GameDBQueryTests
    {

        private string m_assetFolderName;
        private string m_assetFolderPath;
        private string m_assetFolderAbsolutePath;
        private string m_databasePath;
        private string m_databaseAbsolutePath;
        private string m_schemaAbsolutePath;

        [SetUp]
        public void SetUp()
        {
            m_assetFolderName = $"GameDBQueryTests_{Guid.NewGuid():N}";
            m_assetFolderPath = $"Assets/{m_assetFolderName}";
            m_assetFolderAbsolutePath = Path.Combine(Application.dataPath, m_assetFolderName);
            m_databasePath = $"{m_assetFolderPath}/database.json";
            m_databaseAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.json");
            m_schemaAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.schema.json");
            AssetDatabase.CreateFolder("Assets", m_assetFolderName);
            GameDBEditor.OnGameDBSaved = null;
        }

        [TearDown]
        public void TearDown()
        {
            GameDBEditor.OnGameDBSaved = null;
            AssetDatabase.DeleteAsset(m_assetFolderPath);
            if (Directory.Exists(m_assetFolderAbsolutePath))
            {
                Directory.Delete(m_assetFolderAbsolutePath, true);
            }
        }

        [Test]
        public void Query_ProjectsOrdinalWireValuesWithoutWritingOrNotifying()
        {
            CreateRepresentativeDatabase();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var dataWriteTime = File.GetLastWriteTimeUtc(m_databaseAbsolutePath);
            var schemaWriteTime = File.GetLastWriteTimeUtc(m_schemaAbsolutePath);
            var savedScopes = new List<string>();
            GameDBEditor.OnGameDBSaved = savedScopes.Add;

            var result = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        RowKeys = new List<string> { "Sword" },
                        FieldNames = new List<string>
                        {
                            "Tint", "Tags", "Power", "Offset", "Attributes"
                        }
                    }
                }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBQueryFailureKind.None));
            Assert.That(result.Revision, Is.EqualTo(InspectRevision()));
            Assert.That(result.ReturnedRowCount, Is.EqualTo(1));
            Assert.That(result.HasMore, Is.False);
            Assert.That(result.NextCursor, Is.Null);
            var table = result.Tables.Single();
            Assert.That(table.Fields.Select(field => field.Name), Is.EqualTo(new[]
            {
                "Attributes", "Offset", "Power", "Tags", "Tint"
            }));
            var values = table.Rows.Single().Values;
            Assert.That(values.Keys, Is.EqualTo(new[]
            {
                "Attributes", "Offset", "Power", "Tags", "Tint"
            }));
            Assert.That(values["Power"], Is.EqualTo(12L));

            Assert.That(values["Tint"], Is.EqualTo("#FF8000"));
            Assert.That(values["Offset"], Is.EqualTo("1.5,2.5"));
            Assert.That(values["Tags"], Is.TypeOf<List<object>>());
            Assert.That((List<object>)values["Tags"], Is.EqualTo(new object[] { "melee", "sharp" }));
            Assert.That(values["Attributes"], Is.TypeOf<Dictionary<string, object>>());
            Assert.That(((Dictionary<string, object>)values["Attributes"])["Power"], Is.EqualTo(12L));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(File.GetLastWriteTimeUtc(m_databaseAbsolutePath), Is.EqualTo(dataWriteTime));
            Assert.That(File.GetLastWriteTimeUtc(m_schemaAbsolutePath), Is.EqualTo(schemaWriteTime));
            Assert.That(savedScopes, Is.Empty);
        }

        [Test]
        public void Query_ProjectsUnityObjectAndMatchesGuidIdentity()
        {
            CreateRepresentativeDatabase();
            var icon = GetStoredReference("Sword", "Icon");
            var stalePathOperand = ReferenceWire(icon.Guid,
                $"{m_assetFolderPath}/Resources/Icons/PreviousSword.asset");

            var equals = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Equals, "Icon", stalePathOperand)
            });
            var contains = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Contains, "Icons", stalePathOperand)
            });

            Assert.That(equals.Success, Is.True, equals.Message);
            Assert.That(equals.Tables.Single().Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Sword" }));
            var projected = (IDictionary<string, object>)equals.Tables.Single()
                .Rows.Single().Values["Icon"];
            Assert.That(projected.Keys, Is.EquivalentTo(new[] { "guid", "path" }));
            Assert.That(projected["guid"], Is.EqualTo(icon.Guid));
            Assert.That(projected["path"], Is.EqualTo(icon.Path));
            Assert.That(contains.Success, Is.True, contains.Message);
            Assert.That(contains.Tables.Single().Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Sword" }));

            var pathString = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Equals, "Icon", icon.Path)
            });
            AssertFailure(pathString, GameDBQueryFailureKind.InvalidRequest,
                "predicate.valueInvalid");
        }

        [Test]
        public void Query_NormalizesVectorPredicatesAndResultsInvariantly()
        {
            CreateRepresentativeDatabase();
            var request = new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        RowKeys = new List<string> { "Sword" },
                        FieldNames = new List<string> { "Offset" },
                        Predicates = new List<GameDBQueryPredicate>
                        {
                            Predicate(GameDBQueryPredicateKind.Equals, "Offset", "1.5,2.5")
                        }
                    }
                }
            };
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

                var result = GameDBAutomationService.Query(request);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.ReturnedRowCount, Is.EqualTo(1));
                Assert.That(result.Tables.Single().Rows.Single().Values["Offset"], Is.EqualTo("1.5,2.5"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void Query_EqualsContainsAndNumericRangePredicatesAreAndCombined()
        {
            CreateRepresentativeDatabase();
            AssertSuccess(GameDBAutomationService.SetValue(new GameDBValueRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                FieldName = "Weight",
                Value = 0.1d
            }));
            var result = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Equals, "Enabled", true),

                Predicate(GameDBQueryPredicateKind.Equals, "Tint", "#FF8000"),
                Predicate(GameDBQueryPredicateKind.Equals, "Offset", "1.5,2.5"),
                Predicate(GameDBQueryPredicateKind.Contains, "Name", "wor"),
                Predicate(GameDBQueryPredicateKind.Contains, "Tags", "sharp"),
                Predicate(GameDBQueryPredicateKind.Contains, "Attributes", "Power"),
                Range("Power", 10L, 12L),
                Predicate(GameDBQueryPredicateKind.Equals, "Weight", 0.1d),
                Range("Weight", 0.1d, 0.1d)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Tables.Single().Rows.Select(row => row.Key), Is.EqualTo(new[] { "Sword" }));

            var caseSensitive = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Contains, "Name", "sword")
            });
            Assert.That(caseSensitive.Success, Is.True, caseSensitive.Message);
            Assert.That(caseSensitive.ReturnedRowCount, Is.EqualTo(0));
        }

        [Test]
        public void Query_NumericRangeSupportsOpenBoundsAndRejectsInvalidOrder()
        {
            CreateRepresentativeDatabase();
            var minimumOnly = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Range("Power", 10L, null)
            });
            var maximumOnly = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Range("Power", null, 8L)
            });
            var invalidOrder = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Range("Power", 12L, 8L)
            });
            var fractionalInt = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Range("Power", 8.5d, null)
            });

            Assert.That(minimumOnly.Success, Is.True, minimumOnly.Message);
            Assert.That(minimumOnly.Tables.Single().Rows.Select(row => row.Key), Is.EqualTo(new[] { "Sword" }));
            Assert.That(maximumOnly.Success, Is.True, maximumOnly.Message);
            Assert.That(maximumOnly.Tables.Single().Rows.Select(row => row.Key), Is.EqualTo(new[] { "Axe" }));
            AssertFailure(invalidOrder, GameDBQueryFailureKind.InvalidRequest, "range.orderInvalid");
            AssertFailure(fractionalInt, GameDBQueryFailureKind.InvalidRequest, "predicate.valueInvalid");
        }

        [Test]
        public void RuntimeVectorsUseInvariantFiniteWireStrings()
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

                Assert.That(new GameDBLibrary.Vector2("1.5,2.5").ToString(), Is.EqualTo("1.5,2.5"));
                Assert.That(new GameDBLibrary.Vector3("1.5,2.5,3.5").ToString(), Is.EqualTo("1.5,2.5,3.5"));
                Assert.That(new GameDBLibrary.Vector4("1.5,2.5,3.5,4.5").ToString(),
                    Is.EqualTo("1.5,2.5,3.5,4.5"));

                Assert.Throws<FormatException>(() => new GameDBLibrary.Vector2(float.NaN, 1f));
                Assert.Throws<FormatException>(() => new GameDBLibrary.Vector3(1f, float.PositiveInfinity, 2f));
                Assert.Throws<FormatException>(() => new GameDBLibrary.Vector4(1f, 2f, 3f, float.NegativeInfinity));

                var mutable2 = new GameDBLibrary.Vector2(1f, 2f) { x = float.NaN };
                var mutable3 = new GameDBLibrary.Vector3(1f, 2f, 3f) { y = float.PositiveInfinity };
                var mutable4 = new GameDBLibrary.Vector4(1f, 2f, 3f, 4f) { w = float.NegativeInfinity };
                Assert.Throws<FormatException>(() => mutable2.ToString());
                Assert.Throws<FormatException>(() => mutable3.ToString());
                Assert.Throws<FormatException>(() => mutable4.ToString());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void Query_RejectsNonFiniteStoredWireValues()
        {
            var invalidVector = new GameDBLibrary.Vector2(1f, 2f) { x = float.PositiveInfinity };
            var cases = new List<KeyValuePair<GameDBFieldSnapshot, object>>
            {
                new KeyValuePair<GameDBFieldSnapshot, object>(
                    new GameDBFieldSnapshot { Name = "Scalar", FieldType = FieldType.@float },
                    double.NaN),
                new KeyValuePair<GameDBFieldSnapshot, object>(
                    new GameDBFieldSnapshot { Name = "Array", FieldType = FieldType.@float, IsArray = true },
                    new List<object> { double.PositiveInfinity }),
                new KeyValuePair<GameDBFieldSnapshot, object>(
                    new GameDBFieldSnapshot
                    {
                        Name = "Dictionary",
                        FieldType = FieldType.dictionary,
                        DictionaryType = new GameDBDictionaryTypeDefinition
                        {
                            KeyType = KeyType.@string,
                            ValueType = FieldType.@float
                        }
                    },
                    new Dictionary<object, object> { { "Value", double.NegativeInfinity } }),
                new KeyValuePair<GameDBFieldSnapshot, object>(
                    new GameDBFieldSnapshot { Name = "Vector", FieldType = FieldType.vector2 },
                    invalidVector)
            };

            foreach (var item in cases)
            {
                var snapshot = new GameDBSnapshot
                {
                    Revision = "non-finite",
                    Tables = new List<GameDBTableSnapshot>
                    {
                        new GameDBTableSnapshot
                        {
                            Name = "Items",
                            Fields = new List<GameDBFieldSnapshot> { item.Key },
                            Rows = new List<GameDBRowSnapshot>
                            {
                                new GameDBRowSnapshot
                                {
                                    Key = "Invalid",
                                    Values = new Dictionary<string, object> { { item.Key.Name, item.Value } }
                                }
                            }
                        }
                    }
                };
                var result = GameDBQueryEngine.Execute("Assets/non-finite.json", snapshot,
                    new GameDBQueryRequest
                    {
                        DatabasePath = "Assets/non-finite.json",
                        Tables = OneTable("Items")
                    });

                AssertFailure(result, GameDBQueryFailureKind.EvaluationFailed,
                    "query.evaluationFailed");
            }
        }

        [Test]
        public void Query_ReferencesRowMatchesScalarArrayAndDictionaryValues()
        {
            CreateRepresentativeDatabase();
            var result = QueryTable("Recipes", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.ReferencesRow, "Result", "Sword"),
                Predicate(GameDBQueryPredicateKind.ReferencesRow, "Ingredients", "Sword"),
                Predicate(GameDBQueryPredicateKind.ReferencesRow, "Slots", "Sword")
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Tables.Single().Rows.Select(row => row.Key), Is.EqualTo(new[] { "Forge" }));
            var values = result.Tables.Single().Rows.Single().Values;
            Assert.That(values["Result"], Is.EqualTo("Sword"));
            Assert.That((List<object>)values["Ingredients"], Is.EqualTo(new object[] { "Sword" }));
            Assert.That(((Dictionary<string, object>)values["Slots"])["Primary"], Is.EqualTo("Sword"));
        }

        [Test]
        public void Query_EqualsMatchesUnsetTableReferencesAsNull()
        {
            CreateRepresentativeDatabase();
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                RowKey = "Unset"
            }));

            var result = QueryTable("Recipes", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Equals, "Result", null)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.ReturnedRowCount, Is.EqualTo(1));
            Assert.That(result.Tables.Single().Rows.Single().Key, Is.EqualTo("Unset"));
            Assert.That(result.Tables.Single().Rows.Single().Values["Result"], Is.Null);
        }

        [Test]
        public void Query_PredicateMayUseUnprojectedFieldAndSelectorsAreExact()
        {
            CreateRepresentativeDatabase();
            var result = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        RowKeys = new List<string> { "Sword" },
                        FieldNames = new List<string> { "Name" },
                        Predicates = new List<GameDBQueryPredicate>
                        {
                            Predicate(GameDBQueryPredicateKind.Equals, "Power", 12L)
                        }
                    }
                }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Tables.Single().Fields.Single().Name, Is.EqualTo("Name"));
            Assert.That(result.Tables.Single().Rows.Single().Values.Keys, Is.EqualTo(new[] { "Name" }));

            var unknownField = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        FieldNames = new List<string> { "Missing" }
                    }
                }
            });
            AssertFailure(unknownField, GameDBQueryFailureKind.InvalidRequest, "field.notFound");
        }

        [Test]
        public void Query_RejectsUnsupportedPredicatesAndMalformedRequests()
        {
            CreateRepresentativeDatabase();
            var collectionEquals = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Equals, "Tags", "melee")
            });
            var stringRange = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                Range("Name", 1L, 2L)
            });
            var tableRefContains = QueryTable("Recipes", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.Contains, "Ingredients", "Sword")
            });
            var missingReference = QueryTable("Recipes", new List<GameDBQueryPredicate>
            {
                Predicate(GameDBQueryPredicateKind.ReferencesRow, "Result", "Missing")
            });
            var unspecified = QueryTable("Items", new List<GameDBQueryPredicate>
            {
                new GameDBQueryPredicate { FieldName = "Name", Value = "Sword" }
            });
            var noTables = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath
            });
            var invalidLimit = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Limit = 1001,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection { TableName = "Items" }
                }
            });

            AssertFailure(collectionEquals, GameDBQueryFailureKind.InvalidRequest, "predicate.incompatible");
            AssertFailure(stringRange, GameDBQueryFailureKind.InvalidRequest, "predicate.incompatible");
            AssertFailure(tableRefContains, GameDBQueryFailureKind.InvalidRequest, "predicate.incompatible");
            AssertFailure(missingReference, GameDBQueryFailureKind.InvalidRequest, "reference.rowNotFound");
            AssertFailure(unspecified, GameDBQueryFailureKind.InvalidRequest, "predicate.unspecified");
            AssertFailure(noTables, GameDBQueryFailureKind.InvalidRequest, "projection.required");
            AssertFailure(invalidLimit, GameDBQueryFailureKind.InvalidRequest, "limit.outOfRange");
        }

        [Test]
        public void Query_RejectsDuplicateProjectionAndSelectors()
        {
            CreateRepresentativeDatabase();
            var duplicateTable = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection { TableName = "Items" },
                    new GameDBQueryTableProjection { TableName = "Items" }
                }
            });
            var duplicateRows = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        RowKeys = new List<string> { "Sword", "Sword" }
                    }
                }
            });
            var duplicateFields = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = "Items",
                        FieldNames = new List<string> { "Name", "Name" }
                    }
                }
            });

            AssertFailure(duplicateTable, GameDBQueryFailureKind.InvalidRequest, "projection.duplicateTable");
            AssertFailure(duplicateRows, GameDBQueryFailureKind.InvalidRequest, "row.duplicate");
            AssertFailure(duplicateFields, GameDBQueryFailureKind.InvalidRequest, "field.duplicate");
        }

        [Test]
        public void Query_PaginatesGloballyInOrdinalOrderAndAllowsLimitChanges()
        {
            CreateRepresentativeDatabase();
            var request = PaginationRequest(2);
            var first = GameDBAutomationService.Query(request);
            var repeated = GameDBAutomationService.Query(PaginationRequest(2));

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(first.ReturnedRowCount, Is.EqualTo(2));
            Assert.That(first.HasMore, Is.True);
            Assert.That(first.NextCursor, Is.Not.Empty);
            Assert.That(first.NextCursor, Is.EqualTo(repeated.NextCursor));
            Assert.That(FlattenKeys(first), Is.EqualTo(new[] { "Categories:Food", "Categories:Weapon" }));

            var secondRequest = PaginationRequest(1);
            secondRequest.Cursor = first.NextCursor;
            var second = GameDBAutomationService.Query(secondRequest);
            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(second.HasMore, Is.True);
            Assert.That(FlattenKeys(second), Is.EqualTo(new[] { "Items:Axe" }));

            var thirdRequest = PaginationRequest(5);
            thirdRequest.Cursor = second.NextCursor;
            var third = GameDBAutomationService.Query(thirdRequest);
            Assert.That(third.Success, Is.True, third.Message);
            Assert.That(third.HasMore, Is.False);
            Assert.That(third.NextCursor, Is.Null);
            Assert.That(FlattenKeys(third), Is.EqualTo(new[] { "Items:Sword" }));
        }

        [Test]
        public void Query_CursorSizeDoesNotDependOnIdentifierLength()
        {
            var longPrefix = new string('a', 5000);
            var snapshot = new GameDBSnapshot
            {
                Revision = "long-identifiers",
                Tables = new List<GameDBTableSnapshot>
                {
                    new GameDBTableSnapshot
                    {
                        Name = longPrefix + "Table",
                        Rows = new List<GameDBRowSnapshot>
                        {
                            new GameDBRowSnapshot { Key = longPrefix + "A" },
                            new GameDBRowSnapshot { Key = longPrefix + "B" }
                        }
                    }
                }
            };
            var request = new GameDBQueryRequest
            {
                DatabasePath = "Assets/long-identifiers.json",
                Limit = 1,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection { TableName = snapshot.Tables.Single().Name }
                }
            };

            var first = GameDBQueryEngine.Execute(request.DatabasePath, snapshot, request);
            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(first.NextCursor.Length, Is.LessThan(4096));

            request.Cursor = first.NextCursor;
            var second = GameDBQueryEngine.Execute(request.DatabasePath, snapshot, request);
            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(second.ReturnedRowCount, Is.EqualTo(1));
            Assert.That(second.Tables.Single().Rows.Single().Key, Is.EqualTo(longPrefix + "B"));
        }

        [Test]
        public void Query_RejectsTamperedMismatchedAndStaleCursors()
        {
            CreateRepresentativeDatabase();
            var first = GameDBAutomationService.Query(PaginationRequest(1));
            Assert.That(first.Success, Is.True, first.Message);

            var tamperedRequest = PaginationRequest(1);
            var tamperIndex = first.NextCursor.Length / 2;
            var tamperedCharacter = first.NextCursor[tamperIndex] == 'A' ? 'B' : 'A';
            tamperedRequest.Cursor = first.NextCursor.Substring(0, tamperIndex)
                + tamperedCharacter + first.NextCursor.Substring(tamperIndex + 1);
            var tampered = GameDBAutomationService.Query(tamperedRequest);
            AssertFailure(tampered, GameDBQueryFailureKind.InvalidCursor, "cursor.tampered");

            var malformedRequest = PaginationRequest(1);
            malformedRequest.Cursor = "not-base64!";
            var malformed = GameDBAutomationService.Query(malformedRequest);
            AssertFailure(malformed, GameDBQueryFailureKind.InvalidCursor, "cursor.invalid");

            var mismatchedRequest = new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Cursor = first.NextCursor,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection { TableName = "Items" }
                }
            };
            var mismatched = GameDBAutomationService.Query(mismatchedRequest);
            AssertFailure(mismatched, GameDBQueryFailureKind.InvalidCursor, "cursor.mismatch");

            QueryCursor invalidPosition;
            string cursorError;
            Assert.That(GameDBQueryCursorCodec.TryDecode(first.NextCursor,
                out invalidPosition, out cursorError), Is.True, cursorError);
            invalidPosition.Offset = long.MaxValue;
            var invalidPositionRequest = PaginationRequest(1);
            invalidPositionRequest.Cursor = GameDBQueryCursorCodec.Encode(invalidPosition);
            var invalidPositionResult = GameDBAutomationService.Query(invalidPositionRequest);
            AssertFailure(invalidPositionResult, GameDBQueryFailureKind.InvalidCursor,
                "cursor.positionInvalid");

            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Zzz"
            }));
            var staleRequest = PaginationRequest(1);
            staleRequest.Cursor = first.NextCursor;
            var stale = GameDBAutomationService.Query(staleRequest);
            AssertFailure(stale, GameDBQueryFailureKind.StaleCursor, "cursor.stale");
            Assert.That(stale.Revision, Is.EqualTo(InspectRevision()));
        }

        [Test]
        public void Query_ReturnsStaleCursorBeforeRevalidatingChangedSchema()
        {
            CreateRepresentativeDatabase();
            var first = GameDBAutomationService.Query(PaginationRequest(1));
            Assert.That(first.Success, Is.True, first.Message);

            AssertSuccess(GameDBAutomationService.DeleteField(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                Name = "Name",
                Options = new GameDBOperationOptions { AllowDestructive = true }
            }));

            var request = PaginationRequest(1);
            request.Cursor = first.NextCursor;
            var result = GameDBAutomationService.Query(request);

            AssertFailure(result, GameDBQueryFailureKind.StaleCursor, "cursor.stale");
            Assert.That(result.Revision, Is.EqualTo(InspectRevision()));
        }

        [Test]
        public void Query_ValidatesIntrinsicRequestAndCursorBeforeDatabaseLoad()
        {
            var invalidRequest = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath
            });
            var invalidCursor = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Cursor = "not-base64!",
                Tables = OneTable("Items")
            });

            CreateRepresentativeDatabase();
            var artifactPath = m_databaseAbsolutePath + ".interrupted.tmp";
            File.WriteAllText(artifactPath, "pending");
            var invalidAgainstRecovery = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Limit = 0,
                Tables = OneTable("Items")
            });

            AssertFailure(invalidRequest, GameDBQueryFailureKind.InvalidRequest, "projection.required");
            AssertFailure(invalidCursor, GameDBQueryFailureKind.InvalidCursor, "cursor.invalid");
            AssertFailure(invalidAgainstRecovery, GameDBQueryFailureKind.InvalidRequest,
                "limit.outOfRange");
        }

        [Test]
        public void Query_MapsUnsupportedSchemaFormatToLoadFailed()
        {
            CreateRepresentativeDatabase();
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 2", "\"formatVersion\": 3"));

            var result = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = OneTable("Items")
            });

            AssertFailure(result, GameDBQueryFailureKind.LoadFailed, "database.loadFailed");
            Assert.That(result.Errors.Single().Message,
                Does.Contain("format version 3").And.Contain("supported version 2"));
            Assert.That(result.Tables, Is.Empty);
        }

        [Test]
        public void Query_MapsPathLoadAndRecoveryFailures()
        {
            var invalidPath = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = "Assets/../escaped.json",
                Tables = OneTable("Items")
            });
            var missing = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = OneTable("Items")
            });
            CreateRepresentativeDatabase();
            var artifactPath = m_databaseAbsolutePath + ".interrupted.tmp";
            File.WriteAllText(artifactPath, "pending");
            var recovery = GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = OneTable("Items")
            });

            AssertFailure(invalidPath, GameDBQueryFailureKind.InvalidPath, "path.invalid");
            AssertFailure(missing, GameDBQueryFailureKind.LoadFailed, "database.loadFailed");
            AssertFailure(recovery, GameDBQueryFailureKind.RecoveryRequired, "database.recoveryRequired");
            Assert.That(recovery.RecoveryArtifacts, Is.EqualTo(new[] { artifactPath }));
            Assert.That(recovery.Tables, Is.Empty);
        }

        private void CreateRepresentativeDatabase()
        {
            var icon = CreateUnityObjectReference();
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "QueryTestDatabase"
            }));

            var operations = new List<GameDBBatchOperation>
            {
                AddTable("Recipes"),
                AddTable("Items"),
                AddTable("Categories"),
                Field("Items", "Name", FieldType.@string),
                Field("Items", "Power", FieldType.@int),
                Field("Items", "Weight", FieldType.@float),
                Field("Items", "Enabled", FieldType.@bool),

                Field("Items", "Tint", FieldType.color),
                Field("Items", "Offset", FieldType.vector2),
                Field("Items", "Icon", FieldType.unityObject),
                Field("Items", "Icons", FieldType.unityObject, isArray: true),
                Field("Items", "Tags", FieldType.@string, isArray: true),
                DictionaryField("Items", "Attributes", FieldType.@int),
                Field("Recipes", "Result", FieldType.tableRef, typeArgument: "Items"),
                Field("Recipes", "Ingredients", FieldType.tableRef, isArray: true, typeArgument: "Items"),
                DictionaryField("Recipes", "Slots", FieldType.tableRef, "Items"),
                Row("Categories", "Weapon", null),
                Row("Categories", "Food", null),
                Row("Items", "Sword", new Dictionary<string, object>
                {
                    { "Name", "Sword" },
                    { "Power", 12L },
                    { "Weight", 2.5d },
                    { "Enabled", true },

                    { "Tint", "#FF8000" },
                    { "Offset", "1.5,2.5" },
                    { "Icon", icon },
                    { "Icons", new List<object> { icon } },
                    { "Tags", new List<object> { "melee", "sharp" } },
                    { "Attributes", new Dictionary<string, object> { { "Power", 12L } } }
                }),
                Row("Items", "Axe", new Dictionary<string, object>
                {
                    { "Name", "Axe" },
                    { "Power", 8L },
                    { "Weight", 3d },
                    { "Enabled", false },

                    { "Tags", new List<object> { "melee" } },
                    { "Attributes", new Dictionary<string, object> { { "Power", 8L } } }
                }),
                Row("Recipes", "Forge", new Dictionary<string, object>
                {
                    { "Result", "Sword" },
                    { "Ingredients", new List<object> { "Sword" } },
                    { "Slots", new Dictionary<string, object> { { "Primary", "Sword" } } }
                })
            };
            var batch = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = operations
            });
            Assert.That(batch.Success, Is.True, batch.Message);
            GameDBEditor.OnGameDBSaved = null;
        }

        private Dictionary<string, object> CreateUnityObjectReference()
        {
            var resourcesPath = $"{m_assetFolderPath}/Resources";
            AssetDatabase.CreateFolder(m_assetFolderPath, "Resources");
            var iconsPath = $"{resourcesPath}/Icons";
            AssetDatabase.CreateFolder(resourcesPath, "Icons");
            var assetPath = $"{iconsPath}/Sword.asset";
            AssetDatabase.CreateAsset(
                ScriptableObject.CreateInstance<UnityObjectTestAsset>(), assetPath);
            AssetDatabase.SaveAssets();
            return ReferenceWire(AssetDatabase.AssetPathToGUID(assetPath), assetPath);
        }

        private UnityObjectReference GetStoredReference(string rowKey, string fieldName)
        {
            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(inspected.Success, Is.True, inspected.Message);
            return (UnityObjectReference)inspected.Snapshot.Tables
                .Single(table => table.Name == "Items").Rows
                .Single(row => row.Key == rowKey).Values[fieldName];
        }

        private static Dictionary<string, object> ReferenceWire(string guid, string path)
        {
            return new Dictionary<string, object>
            {
                { "guid", guid },
                { "path", path }
            };
        }

        private GameDBQueryResult QueryTable(string tableName, List<GameDBQueryPredicate> predicates)
        {
            return GameDBAutomationService.Query(new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection
                    {
                        TableName = tableName,
                        Predicates = predicates
                    }
                }
            });
        }

        private GameDBQueryRequest PaginationRequest(int limit)
        {
            return new GameDBQueryRequest
            {
                DatabasePath = m_databasePath,
                Limit = limit,
                Tables = new List<GameDBQueryTableProjection>
                {
                    new GameDBQueryTableProjection { TableName = "Items", FieldNames = new List<string> { "Name" } },
                    new GameDBQueryTableProjection { TableName = "Categories" }
                }
            };
        }

        private string InspectRevision()
        {
            var result = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(result.Success, Is.True, result.Message);
            return result.Snapshot.Revision;
        }

        private static GameDBBatchOperation AddTable(string name)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddTable,
                Table = new GameDBBatchTableOperation { TableName = name }
            };
        }

        private static GameDBBatchOperation Field(string tableName, string fieldName,
            FieldType fieldType, bool isArray = false, string typeArgument = null)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddField,
                Field = new GameDBBatchFieldOperation
                {
                    TableName = tableName,
                    FieldName = fieldName,
                    FieldType = fieldType,
                    IsArray = isArray,
                    TypeArgument = typeArgument
                }
            };
        }

        private static GameDBBatchOperation DictionaryField(string tableName, string fieldName,
            FieldType valueType, string valueTypeArgument = null)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddField,
                Field = new GameDBBatchFieldOperation
                {
                    TableName = tableName,
                    FieldName = fieldName,
                    FieldType = FieldType.dictionary,
                    DictionaryType = new GameDBDictionaryTypeDefinition
                    {
                        KeyType = KeyType.@string,
                        ValueType = valueType,
                        ValueTypeArgument = valueTypeArgument
                    }
                }
            };
        }

        private static GameDBBatchOperation Row(string tableName, string rowKey,
            Dictionary<string, object> values)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddRow,
                Row = new GameDBBatchRowOperation
                {
                    TableName = tableName,
                    RowKey = rowKey,
                    Values = values
                }
            };
        }

        private static GameDBQueryPredicate Predicate(GameDBQueryPredicateKind kind,
            string fieldName, object value)
        {
            return new GameDBQueryPredicate { Kind = kind, FieldName = fieldName, Value = value };
        }

        private static GameDBQueryPredicate Range(string fieldName, object minimum, object maximum)
        {
            return new GameDBQueryPredicate
            {
                Kind = GameDBQueryPredicateKind.NumericRange,
                FieldName = fieldName,
                Minimum = minimum,
                Maximum = maximum
            };
        }

        private static List<GameDBQueryTableProjection> OneTable(string name)
        {
            return new List<GameDBQueryTableProjection>
            {
                new GameDBQueryTableProjection { TableName = name }
            };
        }

        private static string[] FlattenKeys(GameDBQueryResult result)
        {
            return result.Tables.SelectMany(table => table.Rows.Select(row => $"{table.Name}:{row.Key}"))
                .ToArray();
        }

        private static void AssertFailure(GameDBQueryResult result, GameDBQueryFailureKind kind,
            string code)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(kind));
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain(code));
            Assert.That(result.Tables, Is.Empty);
            Assert.That(result.ReturnedRowCount, Is.EqualTo(0));
        }

        private static void AssertSuccess(GameDBAutomationResult result)
        {
            Assert.That(result.Success, Is.True, result.Message);
        }
    }
}
