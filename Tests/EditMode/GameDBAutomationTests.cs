using GameDBEditorLibrary.Automation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class GameDBAutomationTests
    {
        private string m_assetFolderName;
        private string m_assetFolderPath;
        private string m_assetFolderAbsolutePath;
        private string m_databasePath;

        [SetUp]
        public void SetUp()
        {
            m_assetFolderName = $"GameDBAutomationTests_{Guid.NewGuid():N}";
            m_assetFolderPath = $"Assets/{m_assetFolderName}";
            m_assetFolderAbsolutePath = Path.Combine(Application.dataPath, m_assetFolderName);
            m_databasePath = $"{m_assetFolderPath}/database.json";
            Directory.CreateDirectory(m_assetFolderAbsolutePath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(m_assetFolderPath);
            if (Directory.Exists(m_assetFolderAbsolutePath))
            {
                Directory.Delete(m_assetFolderAbsolutePath, true);
            }
        }

        [Test]
        public void Create_DryRunReturnsSnapshotWithoutWritingFiles()
        {
            var result = GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "DryRunDatabase",
                Options = new GameDBOperationOptions { DryRun = true }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.DryRun, Is.True);
            Assert.That(result.Snapshot.ScopeName, Is.EqualTo("DryRunDatabase"));
            Assert.That(File.Exists(Path.Combine(m_assetFolderAbsolutePath, "database.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(m_assetFolderAbsolutePath, "database.schema.json")), Is.False);
        }

        [Test]
        public void Create_RejectsPathOutsideAssets()
        {
            var result = GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = "Assets/../escaped.json",
                ScopeName = "Escaped"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("outside"));
        }

        [Test]
        public void TableFieldAndRowMutationsPersistAndInspect()
        {
            CreateDatabase();

            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Power",
                FieldType = FieldType.@int
            }));
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                Values = new Dictionary<string, object> { { "Power", 12L } }
            }));
            AssertSuccess(GameDBAutomationService.SetValue(new GameDBValueRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                FieldName = "Power",
                Value = 15L
            }));

            var result = GameDBAutomationService.Inspect(m_databasePath);
            var table = result.Snapshot.Tables.Single(item => item.Name == "Items");
            var row = table.Rows.Single(item => item.Key == "Sword");

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(row.Values["Power"], Is.EqualTo(15L));
            Assert.That(GameDBAutomationService.ListDatabases(m_assetFolderPath).DatabasePaths,
                Does.Contain(m_databasePath));
        }

        [Test]
        public void MutationRejectsStaleExpectedRevision()
        {
            CreateDatabase();
            var revision = GameDBAutomationService.Inspect(m_databasePath).Snapshot.Revision;
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));

            var stale = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                Options = new GameDBOperationOptions { ExpectedRevision = revision }
            });

            Assert.That(stale.Success, Is.False);
            Assert.That(stale.Message, Does.Contain("Revision conflict"));
        }

        [Test]
        public void ArrayMutationValidatesEveryElement()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Values",
                FieldType = FieldType.@int,
                IsArray = true
            }));
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword"
            }));

            var result = GameDBAutomationService.SetValue(new GameDBValueRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                FieldName = "Values",
                Value = new List<object> { 1L, "invalid" }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("invalid"));
        }

        [Test]
        public void AddRow_RoundTripsComplexWireValues()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Values",
                FieldType = FieldType.@int,
                IsArray = true
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Attributes",
                FieldType = FieldType.dictionary,
                DictionaryType = new GameDBDictionaryTypeDefinition
                {
                    KeyType = KeyType.@string,
                    ValueType = FieldType.@int
                }
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Tint",
                FieldType = FieldType.color
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                FieldName = "Offset",
                FieldType = FieldType.vector2
            }));

            var added = GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                Values = new Dictionary<string, object>
                {
                    { "Values", new List<object> { 1L, 2L } },
                    { "Attributes", new Dictionary<string, object> { { "Power", 12L } } },
                    { "Tint", "#FF8000" },
                    { "Offset", "1.5,2.5" }
                }
            });

            Assert.That(added.Success, Is.True, added.Message);
            var values = added.Snapshot.Tables.Single().Rows.Single().Values;
            Assert.That(((IList<object>)values["Values"]).Cast<object>(), Is.EqualTo(new object[] { 1L, 2L }));
            Assert.That(((Dictionary<object, object>)values["Attributes"])["Power"], Is.EqualTo(12L));
            Assert.That(((Color)values["Tint"]).Hex, Is.EqualTo("#FF8000"));
            Assert.That(((Vector2)values["Offset"]).x, Is.EqualTo(1.5f));
            Assert.That(((Vector2)values["Offset"]).y, Is.EqualTo(2.5f));
        }

        [Test]
        public void DestructiveMutationRequiresExplicitAuthorization()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));

            var blocked = GameDBAutomationService.DeleteTable(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                Name = "Items"
            });
            var allowed = GameDBAutomationService.DeleteTable(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                Name = "Items",
                Options = DestructiveOptions()
            });

            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.Message, Does.Contain("AllowDestructive"));
            Assert.That(allowed.Success, Is.True, allowed.Message);
        }

        [Test]
        public void ReferenceSensitiveRenamesAndDeletesHandleScalarArrayAndDictionaryReferences()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                FieldName = "Result",
                FieldType = FieldType.tableRef,
                TypeArgument = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                FieldName = "Ingredients",
                FieldType = FieldType.tableRef,
                IsArray = true,
                TypeArgument = "Items"
            }));
            AssertSuccess(GameDBAutomationService.AddField(new GameDBFieldRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                FieldName = "Slots",
                FieldType = FieldType.dictionary,
                DictionaryType = new GameDBDictionaryTypeDefinition
                {
                    KeyType = KeyType.@string,
                    ValueType = FieldType.tableRef,
                    ValueTypeArgument = "Items"
                }
            }));
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "OldSword"
            }));
            AssertSuccess(GameDBAutomationService.AddRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Recipes",
                RowKey = "ForgeSword",
                Values = new Dictionary<string, object>
                {
                    { "Result", "OldSword" },
                    { "Ingredients", new List<object> { "OldSword" } },
                    { "Slots", new Dictionary<string, object> { { "Primary", "OldSword" } } }
                }
            }));

            var blockedRowDelete = GameDBAutomationService.DeleteRow(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                Name = "OldSword",
                Options = DestructiveOptions()
            });
            var renamedRow = GameDBAutomationService.RenameRow(new GameDBRenameRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                CurrentName = "OldSword",
                NewName = "Sword",
                Options = DestructiveOptions()
            });
            var blockedTableDelete = GameDBAutomationService.DeleteTable(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                Name = "Items",
                Options = DestructiveOptions()
            });
            var renamedTable = GameDBAutomationService.RenameTable(new GameDBRenameRequest
            {
                DatabasePath = m_databasePath,
                CurrentName = "Items",
                NewName = "Catalog",
                Options = DestructiveOptions()
            });

            Assert.That(blockedRowDelete.Success, Is.False);
            Assert.That(blockedRowDelete.Message, Does.Contain("Recipes[ForgeSword].Result"));
            Assert.That(blockedRowDelete.Message, Does.Contain("Recipes[ForgeSword].Ingredients"));
            Assert.That(blockedRowDelete.Message, Does.Contain("Recipes[ForgeSword].Slots"));
            Assert.That(renamedRow.Success, Is.True, renamedRow.Message);
            var recipe = renamedRow.Snapshot.Tables.Single(table => table.Name == "Recipes").Rows.Single();
            Assert.That(recipe.Values["Result"], Is.EqualTo("Sword"));
            Assert.That(((IList<object>)recipe.Values["Ingredients"]).Single(), Is.EqualTo("Sword"));
            Assert.That(((Dictionary<object, object>)recipe.Values["Slots"])["Primary"], Is.EqualTo("Sword"));

            Assert.That(blockedTableDelete.Success, Is.False);
            Assert.That(blockedTableDelete.Message, Does.Contain("Recipes.Result"));
            Assert.That(blockedTableDelete.Message, Does.Contain("Recipes.Ingredients"));
            Assert.That(blockedTableDelete.Message, Does.Contain("Recipes.Slots"));
            Assert.That(renamedTable.Success, Is.True, renamedTable.Message);
            var recipeFields = renamedTable.Snapshot.Tables.Single(table => table.Name == "Recipes").Fields;
            Assert.That(recipeFields.Single(field => field.Name == "Result").TypeArgument, Is.EqualTo("Catalog"));
            Assert.That(recipeFields.Single(field => field.Name == "Ingredients").TypeArgument, Is.EqualTo("Catalog"));
            Assert.That(recipeFields.Single(field => field.Name == "Slots").DictionaryType.ValueTypeArgument,
                Is.EqualTo("Catalog"));
        }

        [Test]
        public void ExportAndRawSaveRoundTrip()
        {
            CreateDatabase();
            var exported = GameDBAutomationService.ExportJson(m_databasePath);
            var replacementData = exported.DataJson.Replace("\"tables\": {}", "\"tables\": {}");
            var saved = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = replacementData,
                SchemaJson = exported.SchemaJson,
                Options = DestructiveOptions()
            });

            Assert.That(exported.Success, Is.True, exported.Message);
            Assert.That(exported.DataJson, Does.Contain("\"tables\""));
            Assert.That(exported.SchemaJson, Does.Contain("AutomationTestDatabase"));
            Assert.That(saved.Success, Is.True, saved.Message);
        }

        [Test]
        public void GenerateCSharp_DryRunValidatesWithoutCreatingOutput()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));

            var outputPath = $"{m_assetFolderPath}/Generated";
            var result = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
            {
                DatabasePath = m_databasePath,
                OutputDirectory = outputPath,
                Options = new GameDBOperationOptions { DryRun = true }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.ChangedPaths, Does.Contain(outputPath + "/AutomationTestDatabase"));
            Assert.That(Directory.Exists(Path.Combine(m_assetFolderAbsolutePath, "Generated")), Is.False);
        }

        private void CreateDatabase()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "AutomationTestDatabase"
            }));
        }

        private static GameDBOperationOptions DestructiveOptions()
        {
            return new GameDBOperationOptions { AllowDestructive = true };
        }

        private static void AssertSuccess(GameDBAutomationResult result)
        {
            Assert.That(result.Success, Is.True, result.Message);
        }
    }
}
