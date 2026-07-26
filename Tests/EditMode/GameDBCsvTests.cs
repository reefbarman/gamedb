using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class GameDBCsvTests
    {
        private string m_assetFolderName;
        private string m_assetFolderPath;
        private string m_assetFolderAbsolutePath;
        private string m_databasePath;
        private string m_databaseAbsolutePath;
        private string m_schemaPath;
        private string m_schemaAbsolutePath;

        [SetUp]
        public void SetUp()
        {
            m_assetFolderName = $"GameDBCsvTests_{Guid.NewGuid():N}";
            m_assetFolderPath = $"Assets/{m_assetFolderName}";
            m_assetFolderAbsolutePath = Path.Combine(Application.dataPath, m_assetFolderName);
            m_databasePath = $"{m_assetFolderPath}/database.json";
            m_databaseAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.json");
            m_schemaPath = $"{m_assetFolderPath}/database.schema.json";
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
        public void ExportCsv_IsDeterministicRfc4180AndFormulaSafe()
        {
            CreateScalarDatabase();
            var icon = CreateUnityObjectReference();
            AddRow("Items", "=FormulaKey", new Dictionary<string, object>
            {
                { "Name", "line 1, \"quoted\"\nline 2" },
                { "Power", -12L },
                { "Weight", 1.5d },
                { "Enabled", true },
                { "Tint", "#FF8000" },
                { "Offset", "1.5,2.5" },
                { "Icon", icon }
            });
            AddRow("Items", "'LiteralKey", new Dictionary<string, object>
            {
                { "Name", "'=literal" },
                { "Power", 0L },
                { "Weight", 0d },
                { "Enabled", false },
                { "Tint", "#000000" },
                { "Offset", "0,0" },
                { "Icon", ReferenceWire(string.Empty, string.Empty) }
            });

            var result = GameDBAutomationService.ExportCsv(new GameDBCsvExportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBCsvFailureKind.None));
            Assert.That(result.RowCount, Is.EqualTo(2));
            Assert.That(result.CsvText, Does.StartWith("__key,Enabled,Icon,Name,Offset,Power,Tint,Weight\r\n"));
            var parsed = GameDBCsvCodec.Parse(result.CsvText);
            Assert.That(parsed.Success, Is.True, parsed.Error?.Message);
            var header = parsed.Records[0].Cells.Select(cell => cell.Text).ToList();
            var iconColumn = header.IndexOf("Icon");
            var assigned = parsed.Records.Single(record => record.Cells[0].Text == "'=FormulaKey");
            var empty = parsed.Records.Single(record => record.Cells[0].Text == "''LiteralKey");
            AssertReferenceJson(assigned.Cells[iconColumn].Text,
                (string)icon["guid"], (string)icon["path"]);
            AssertReferenceJson(empty.Cells[iconColumn].Text, string.Empty, string.Empty);
            Assert.That(result.CsvText, Does.Contain("\"line 1, \"\"quoted\"\"\nline 2\""));
            Assert.That(result.CsvText, Does.Contain("'-12"));
            Assert.That(result.CsvText.Replace("\r\n", string.Empty), Does.Not.Contain("\r"));
        }

        [Test]
        public void Csv_MapsUnsupportedSchemaFormatToLoadFailedBeforeCsvValidation()
        {
            CreateScalarDatabase();
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 2", "\"formatVersion\": 3"));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var exported = GameDBAutomationService.ExportCsv(new GameDBCsvExportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });
            var imported = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nSword,Sword",
                Mode = GameDBCsvImportMode.Upsert
            });

            Assert.That(exported.Success, Is.False);
            Assert.That(exported.FailureKind, Is.EqualTo(GameDBCsvFailureKind.LoadFailed));
            Assert.That(exported.Errors.Select(error => error.Code), Does.Contain("csv.loadFailed"));
            Assert.That(exported.Message, Does.Contain("format version 3"));
            AssertCsvFailure(imported, GameDBCsvFailureKind.LoadFailed, "csv.loadFailed");
            Assert.That(imported.Message, Does.Contain("format version 3"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void ImportCsv_UpsertAcceptsQuotedMultilineValuesAndPreservesOmittedData()
        {
            CreateScalarDatabase();
            AddRow("Items", "Sword", new Dictionary<string, object>
            {
                { "Name", "Old" },
                { "Power", 12L }
            });
            AddRow("Items", "Axe", new Dictionary<string, object>
            {
                { "Name", "Axe" },
                { "Power", 8L }
            });
            var csv = "\uFEFF__key,Name\nSword,\"line 1, \"\"quoted\"\"\nline 2\"\nSpear,'=Formula";

            var result = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = csv,
                Mode = GameDBCsvImportMode.Upsert
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCsvCommitStatus.Saved));
            Assert.That(result.ImportedRowCount, Is.EqualTo(2));
            Assert.That(result.FilesCommitted, Is.True);
            var table = InspectTable("Items");
            Assert.That(table.Rows.Select(row => row.Key), Is.EquivalentTo(new[] { "Sword", "Axe", "Spear" }));
            Assert.That(Row(table, "Sword").Values["Name"], Is.EqualTo("line 1, \"quoted\"\nline 2"));
            Assert.That(Row(table, "Sword").Values["Power"], Is.EqualTo(12L));
            Assert.That(Row(table, "Axe").Values["Name"], Is.EqualTo("Axe"));
            Assert.That(Row(table, "Spear").Values["Name"], Is.EqualTo("=Formula"));
            Assert.That(Row(table, "Spear").Values["Power"], Is.EqualTo(0L));
        }

        [Test]
        public void ImportCsv_UnityObjectRequiresCanonicalJsonCell()
        {
            CreateScalarDatabase();
            var icon = CreateUnityObjectReference();
            var iconJson = JsonSerialization.Serialize(icon);
            var validCsv = GameDBCsvCodec.Write(new IReadOnlyList<string>[]
            {
                new[] { "__key", "Icon" },
                new[] { "Sword", iconJson }
            });

            var imported = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = validCsv,
                Mode = GameDBCsvImportMode.Upsert
            });
            var stored = (UnityObjectReference)Row(InspectTable("Items"), "Sword").Values["Icon"];
            var invalid = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Icon\r\nAxe,Assets/Game/Resources/Icons/Axe.asset",
                Mode = GameDBCsvImportMode.Upsert
            });

            Assert.That(imported.Success, Is.True, imported.Message);
            Assert.That(stored.Guid, Is.EqualTo(icon["guid"]));
            Assert.That(stored.Path, Is.EqualTo(icon["path"]));
            AssertCsvFailure(invalid, GameDBCsvFailureKind.InvalidCsv, "csv.valueInvalid");
            Assert.That(invalid.Errors.Single().Message,
                Is.EqualTo("Cell value is invalid for unityObject."));
            Assert.That(InspectTable("Items").Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Sword" }));
        }

        [Test]
        public void ImportCsv_ReplaceRequiresAuthorizationAndEveryScalarColumn()
        {
            CreateScalarDatabase();
            AddRow("Items", "Old", new Dictionary<string, object> { { "Name", "Old" } });
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var partial = "__key,Name\r\nNew,New";

            var denied = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = partial,
                Mode = GameDBCsvImportMode.Replace
            });
            var incomplete = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = partial,
                Mode = GameDBCsvImportMode.Replace,
                Options = new GameDBOperationOptions { AllowDestructive = true }
            });
            var exported = GameDBAutomationService.ExportCsv(new GameDBCsvExportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });
            var replacement = exported.CsvText.Replace("Old,", "New,").Replace(",Old,", ",New,");
            var replaced = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = replacement,
                Mode = GameDBCsvImportMode.Replace,
                Options = new GameDBOperationOptions { AllowDestructive = true }
            });

            AssertCsvFailure(denied, GameDBCsvFailureKind.AuthorizationDenied, "csv.destructiveDenied");
            AssertCsvFailure(incomplete, GameDBCsvFailureKind.InvalidCsv, "csv.replaceFieldMissing");
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(replaced.Success, Is.True, replaced.Message);
            Assert.That(InspectTable("Items").Rows.Select(row => row.Key), Is.EqualTo(new[] { "New" }));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.Not.EqualTo(dataBefore));
        }

        [Test]
        public void ImportCsv_DryRunAndRevisionConflictDoNotWriteOrNotify()
        {
            CreateScalarDatabase();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var savedScopes = new List<string>();
            GameDBEditor.OnGameDBSaved = savedScopes.Add;

            var dryRun = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nSword,Sword",
                Mode = GameDBCsvImportMode.Upsert,
                Options = new GameDBOperationOptions { DryRun = true }
            });
            var conflict = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nSword,Sword",
                Mode = GameDBCsvImportMode.Upsert,
                Options = new GameDBOperationOptions { ExpectedRevision = "stale" }
            });

            Assert.That(dryRun.Success, Is.True, dryRun.Message);
            Assert.That(dryRun.CommitStatus, Is.EqualTo(GameDBCsvCommitStatus.DryRun));
            Assert.That(dryRun.Snapshot.Tables.Single(table => table.Name == "Items").Rows.Single().Key,
                Is.EqualTo("Sword"));
            AssertCsvFailure(conflict, GameDBCsvFailureKind.RevisionConflict);
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(savedScopes, Is.Empty);
            Assert.That(InspectTable("Items").Rows, Is.Empty);
        }

        [Test]
        public void ImportCsv_ReportsMalformedAndTypedCellCoordinates()
        {
            CreateScalarDatabase();
            var malformed = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nSword,\"unterminated",
                Mode = GameDBCsvImportMode.Upsert
            });
            var invalid = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Power\r\nSword,1.5",
                Mode = GameDBCsvImportMode.Upsert
            });
            var whitespaceKey = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\n   ,Invalid",
                Mode = GameDBCsvImportMode.Upsert
            });

            AssertCsvFailure(malformed, GameDBCsvFailureKind.InvalidCsv, "csv.unterminatedQuote");
            Assert.That(malformed.Errors.Single().RecordNumber, Is.EqualTo(2));
            Assert.That(malformed.Errors.Single().LineNumber, Is.EqualTo(2));
            Assert.That(malformed.Errors.Single().ColumnNumber, Is.EqualTo(2));
            AssertCsvFailure(invalid, GameDBCsvFailureKind.InvalidCsv, "csv.valueInvalid");
            Assert.That(invalid.Errors.Single().RecordNumber, Is.EqualTo(2));
            Assert.That(invalid.Errors.Single().LineNumber, Is.EqualTo(2));
            Assert.That(invalid.Errors.Single().ColumnNumber, Is.EqualTo(2));
            Assert.That(invalid.Errors.Single().ColumnName, Is.EqualTo("Power"));
            Assert.That(invalid.Errors.Single().RowKey, Is.EqualTo("Sword"));
            Assert.That(invalid.Errors.Single().FieldName, Is.EqualTo("Power"));
            AssertCsvFailure(whitespaceKey, GameDBCsvFailureKind.InvalidCsv, "csv.rowKeyInvalid");
            Assert.That(whitespaceKey.Errors.Single().RecordNumber, Is.EqualTo(2));
            Assert.That(whitespaceKey.Errors.Single().LineNumber, Is.EqualTo(2));
            Assert.That(whitespaceKey.Errors.Single().ColumnNumber, Is.EqualTo(1));
        }

        [Test]
        public void Csv_RejectsCollectionTablesWithoutMutation()
        {
            CreateScalarDatabase();
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Tags",
                FieldType = FieldType.@string,
                IsArray = true
            }));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var exported = GameDBAutomationService.ExportCsv(new GameDBCsvExportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });
            var imported = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nSword,Sword",
                Mode = GameDBCsvImportMode.Upsert
            });

            Assert.That(exported.Success, Is.False);
            Assert.That(exported.FailureKind, Is.EqualTo(GameDBCsvFailureKind.UnsupportedSchema));
            Assert.That(exported.Errors.Single().Code, Is.EqualTo("csv.collectionUnsupported"));
            AssertCsvFailure(imported, GameDBCsvFailureKind.UnsupportedSchema,
                "csv.collectionUnsupported");
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void ImportCsv_MapsImportedReferenceValidationToCellAndRollsBack()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "CsvReferenceCoordinates"
            }));
            AssertSuccess(GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Items"),
                    AddTable("Recipes"),
                    AddField("Recipes", "Result", FieldType.tableRef, "Items")
                }
            }));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var result = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                CsvText = "__key,Result\r\nRecipe,Missing",
                Mode = GameDBCsvImportMode.Upsert
            });

            AssertCsvFailure(result, GameDBCsvFailureKind.ValidationFailed, "tableRef.row.missing");
            var error = result.Errors.Single();
            Assert.That(error.RecordNumber, Is.EqualTo(2));
            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.ColumnNumber, Is.EqualTo(2));
            Assert.That(error.ColumnName, Is.EqualTo("Result"));
            Assert.That(error.RowKey, Is.EqualTo("Recipe"));
            Assert.That(error.FieldName, Is.EqualTo("Result"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(InspectTable("Recipes").Rows, Is.Empty);
        }

        [Test]
        public void ImportCsv_ReferenceValidationRollsBackReplace()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "CsvReferenceTests"
            }));
            AssertSuccess(GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Items"),
                    AddTable("Recipes"),
                    AddField("Items", "Name", FieldType.@string),
                    AddField("Recipes", "Result", FieldType.tableRef, "Items"),
                    AddRowOperation("Items", "Sword", new Dictionary<string, object> { { "Name", "Sword" } }),
                    AddRowOperation("Recipes", "Recipe", new Dictionary<string, object> { { "Result", "Sword" } })
                }
            }));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var result = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CsvText = "__key,Name\r\nAxe,Axe",
                Mode = GameDBCsvImportMode.Replace,
                Options = new GameDBOperationOptions { AllowDestructive = true }
            });

            AssertCsvFailure(result, GameDBCsvFailureKind.ValidationFailed);
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("tableRef.row.missing"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(InspectTable("Items").Rows.Single().Key, Is.EqualTo("Sword"));
        }

        private void CreateScalarDatabase()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "CsvTests"
            }));
            AssertSuccess(GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Items"),
                    AddField("Items", "Name", FieldType.@string),
                    AddField("Items", "Power", FieldType.@int),
                    AddField("Items", "Weight", FieldType.@float),
                    AddField("Items", "Enabled", FieldType.@bool),
                    AddField("Items", "Tint", FieldType.color),
                    AddField("Items", "Offset", FieldType.vector2),
                    AddField("Items", "Icon", FieldType.unityObject)
                }
            }));
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

        private static Dictionary<string, object> ReferenceWire(string guid, string path)
        {
            return new Dictionary<string, object>
            {
                { "guid", guid },
                { "path", path }
            };
        }

        private static void AssertReferenceJson(string json, string guid, string path)
        {
            var reference = (IDictionary<string, object>)JsonSerialization.Deserialize(json);
            Assert.That(reference.Keys, Is.EquivalentTo(new[] { "guid", "path" }));
            Assert.That(reference["guid"], Is.EqualTo(guid));
            Assert.That(reference["path"], Is.EqualTo(path));
        }

        private void AddRow(string tableName, string rowKey, Dictionary<string, object> values)
        {
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = tableName,
                RowKey = rowKey,
                Values = values
            }));
        }

        private GameDBTableSnapshot InspectTable(string tableName)
        {
            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(inspected.Success, Is.True, inspected.Message);
            return inspected.Snapshot.Tables.Single(table => table.Name == tableName);
        }

        private static GameDBRowSnapshot Row(GameDBTableSnapshot table, string key)
        {
            return table.Rows.Single(row => row.Key == key);
        }

        private static GameDBBatchOperation AddTable(string tableName)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddTable,
                Table = new GameDBBatchTableOperation { TableName = tableName }
            };
        }

        private static GameDBBatchOperation AddField(string tableName, string fieldName,
            FieldType type, string typeArgument = null)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddField,
                Field = new GameDBBatchFieldOperation
                {
                    TableName = tableName,
                    FieldName = fieldName,
                    FieldType = type,
                    TypeArgument = typeArgument
                }
            };
        }

        private static GameDBBatchOperation AddRowOperation(string tableName, string rowKey,
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

        private static void AssertSuccess(GameDBAutomationResult result)
        {
            Assert.That(result.Success, Is.True, result.Message);
        }

        private static void AssertSuccess(GameDBBatchResult result)
        {
            Assert.That(result.Success, Is.True, result.Message);
        }

        private static void AssertCsvFailure(GameDBCsvImportResult result,
            GameDBCsvFailureKind kind, string code = null)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(kind));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCsvCommitStatus.NotAttempted));
            if (code != null)
            {
                Assert.That(result.Errors.Select(error => error.Code), Does.Contain(code));
            }
        }
    }
}
