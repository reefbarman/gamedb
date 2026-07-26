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
    public class GameDBAutomationCharacterizationTests
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
            m_assetFolderName = $"GameDBAutomationCharacterizationTests_{Guid.NewGuid():N}";
            m_assetFolderPath = $"Assets/{m_assetFolderName}";
            m_assetFolderAbsolutePath = Path.Combine(Application.dataPath, m_assetFolderName);
            m_databasePath = $"{m_assetFolderPath}/database.json";
            m_databaseAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.json");
            m_schemaPath = $"{m_assetFolderPath}/database.schema.json";
            m_schemaAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.schema.json");
            Directory.CreateDirectory(m_assetFolderAbsolutePath);
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
        public void Inspect_ReturnsCanonicalSnapshotWithoutMutationMetadata()
        {
            CreateDatabaseWithIntValue(12L);
            var exported = GameDBAutomationService.ExportJson(m_databasePath);

            var result = GameDBAutomationService.Inspect(m_databasePath);
            var loaded = GameDBAutomationService.Load(m_databasePath);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("inspect"));
            Assert.That(result.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.DryRun, Is.False);
            Assert.That(result.RevisionBefore, Is.Null);
            Assert.That(result.RevisionAfter, Is.Null);
            Assert.That(result.ChangedPaths, Is.Empty);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Snapshot.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.Snapshot.SchemaPath, Is.EqualTo(m_schemaPath));
            Assert.That(result.Snapshot.Revision, Is.EqualTo(ComputeRevision(exported.SchemaJson, exported.DataJson)));
            Assert.That(loaded.Operation, Is.EqualTo("inspect"));
            Assert.That(loaded.Snapshot.Revision, Is.EqualTo(result.Snapshot.Revision));
        }

        [Test]
        public void UnsupportedSchemaFormatFailsAutomationLoadsWithoutWritingFiles()
        {
            CreateDatabase();
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 4", "\"formatVersion\": 5"));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            var validated = GameDBAutomationService.Validate(m_databasePath);
            var mutated = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });

            foreach (var result in new[] { inspected, validated, mutated })
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message,
                    Does.Contain("format version 5").And.Contain("supported version 4")
                        .And.Contain("newer GameDB package"));
                AssertGenericFailure(result);
            }
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void Save_RequiresSchemaFormatVersionAndPreservesOtherGenericImportFailures()
        {
            const string dataJson = "{\"tables\":{}}";
            var missingVersion = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = dataJson,
                SchemaJson = "{\"tables\":{},\"scope\":\"Replacement\",\"localizationDB\":false}"
            });
            var malformedSchema = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = dataJson,
                SchemaJson = "{\"formatVersion\":4,\"tables\":\"invalid\"}"
            });

            Assert.That(missingVersion.Success, Is.False);
            Assert.That(missingVersion.Message, Does.Contain("missing required 'formatVersion'"));
            AssertGenericFailure(missingVersion);
            Assert.That(malformedSchema.Success, Is.False);
            Assert.That(malformedSchema.Message,
                Is.EqualTo("DataJson or SchemaJson could not be imported."));
            AssertGenericFailure(malformedSchema);
            Assert.That(File.Exists(m_databaseAbsolutePath), Is.False);
            Assert.That(File.Exists(m_schemaAbsolutePath), Is.False);
        }

        [Test]
        public void Create_ReturnsProspectiveMetadataAndChangedPaths()
        {
            var result = GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "CharacterizationDatabase",
                LocalizationDatabase = true
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("create"));
            Assert.That(result.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.RevisionBefore, Is.Null);
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(result.Snapshot.ScopeName, Is.EqualTo("CharacterizationDatabase"));
            Assert.That(result.Snapshot.LocalizationDatabase, Is.True);
            AssertChangedDatabasePaths(result);
            Assert.That(InspectRevision(), Is.EqualTo(result.RevisionAfter));
        }

        [Test]
        public void SuccessfulMutation_ReturnsProspectiveMetadataAndChangedPaths()
        {
            CreateDatabase();
            var revisionBefore = InspectRevision();

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("addTable"));
            Assert.That(result.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.DryRun, Is.False);
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.Snapshot.Tables.Select(table => table.Name), Does.Contain("Items"));
            Assert.That(result.Issues, Is.Empty);
            AssertChangedDatabasePaths(result);
        }

        [Test]
        public void MutationDryRun_ReturnsProspectiveMetadataWithoutChangingFiles()
        {
            CreateDatabase();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var revisionBefore = InspectRevision();

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                Options = new GameDBOperationOptions { DryRun = true }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("addTable"));
            Assert.That(result.DryRun, Is.True);
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.Snapshot.Tables.Select(table => table.Name), Does.Contain("Items"));
            AssertChangedDatabasePaths(result);
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(InspectRevision(), Is.EqualTo(revisionBefore));
        }

        [Test]
        public void CanonicalNoOpMutation_StillReportsChangedPathsAndSaves()
        {
            CreateDatabaseWithIntValue(12L);
            var revisionBefore = InspectRevision();
            var savedScopes = new List<string>();
            GameDBEditor.OnGameDBSaved = savedScopes.Add;

            var result = GameDBAutomationService.SetValue(new GameDBValueRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                FieldName = "Power",
                Value = 12L
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("setValue"));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(revisionBefore));
            Assert.That(result.Snapshot.Revision, Is.EqualTo(revisionBefore));
            AssertChangedDatabasePaths(result);
            Assert.That(savedScopes, Is.EqualTo(new[] { "CharacterizationDatabase" }));
        }

        [Test]
        public void DestructiveMutationWithoutAuthorization_IsRejectedBeforeMissingDatabaseLoad()
        {
            var result = GameDBAutomationService.DeleteTable(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                Name = null
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Operation, Is.EqualTo("deleteTable"));
            Assert.That(result.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.Message, Does.Contain("AllowDestructive"));
            AssertGenericFailure(result);
        }

        [Test]
        public void AuthorizedDestructiveMutation_ReachesMissingDatabaseLoadBeforeArguments()
        {
            var result = GameDBAutomationService.DeleteTable(new GameDBDeleteRequest
            {
                DatabasePath = m_databasePath,
                Name = null,
                Options = DestructiveOptions()
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Operation, Is.EqualTo("deleteTable"));
            Assert.That(result.Message, Does.Contain("Database file does not exist"));
            Assert.That(result.Message, Does.Not.Contain("non-empty"));
            AssertGenericFailure(result);
        }

        [Test]
        public void ExpectedRevision_IsCheckedBeforeCommandArguments()
        {
            CreateDatabase();
            var actualRevision = InspectRevision();

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = null,
                Options = new GameDBOperationOptions { ExpectedRevision = "stale-revision" }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Operation, Is.EqualTo("addTable"));
            Assert.That(result.Message, Is.EqualTo(
                $"Revision conflict. Expected stale-revision, but the database is {actualRevision}. Inspect it again before writing."));
            Assert.That(result.Message, Does.Not.Contain("non-empty"));
            AssertGenericFailure(result);
        }

        [Test]
        public void ExpectedRevision_IsComparedCaseInsensitively()
        {
            CreateDatabase();
            var expectedRevision = InspectRevision().ToUpperInvariant();

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                Options = new GameDBOperationOptions { ExpectedRevision = expectedRevision }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.RevisionBefore, Is.EqualTo(expectedRevision).IgnoreCase);
        }

        [Test]
        public void UpdateRow_ReturnsItsOperationAndProspectiveSnapshot()
        {
            CreateDatabaseWithIntValue(12L);
            var revisionBefore = InspectRevision();

            var result = GameDBAutomationService.UpdateRow(new GameDBRowRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items",
                RowKey = "Sword",
                Values = new Dictionary<string, object> { { "Power", 15L } }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("updateRow"));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.Snapshot.Tables.Single().Rows.Single().Values["Power"], Is.EqualTo(15L));
            AssertChangedDatabasePaths(result);
        }

        [Test]
        public void ValidationBlockedMutation_ReturnsAttemptedMetadataWithoutWritingFiles()
        {
            WriteDatabasePair(
                "{\n  \"tables\": {}\n}",
                "{\n  \"formatVersion\": 4,\n  \"tables\": {},\n  \"scope\": \"\",\n  \"localizationDB\": false\n}");
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var revisionBefore = InspectRevision();

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Operation, Is.EqualTo("addTable"));
            Assert.That(result.Message, Does.Contain("validation issue"));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.Snapshot.Tables.Select(table => table.Name), Does.Contain("Items"));
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("scope.empty"));
            AssertChangedDatabasePaths(result);
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void PostSaveCallbackFailure_ReturnsGenericFailureAfterFilesCommit()
        {
            CreateDatabase();
            GameDBEditor.OnGameDBSaved = _ => throw new InvalidOperationException("callback failed");

            var result = GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            });

            GameDBEditor.OnGameDBSaved = null;
            var persisted = GameDBAutomationService.Inspect(m_databasePath);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Operation, Is.EqualTo("addTable"));
            Assert.That(result.DatabasePath, Is.EqualTo(m_databasePath));
            Assert.That(result.Message, Is.EqualTo("Database could not be saved."));
            AssertGenericFailure(result);
            Assert.That(persisted.Success, Is.True, persisted.Message);
            Assert.That(persisted.Snapshot.Tables.Select(table => table.Name), Does.Contain("Items"));
        }

        [Test]
        public void SaveAndCreateOverwrite_RefuseUnsupportedExistingSchemaWithoutWriting()
        {
            CreateDatabase();
            var exported = GameDBAutomationService.ExportJson(m_databasePath);
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 4", "\"formatVersion\": 5"));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var saved = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = exported.DataJson,
                SchemaJson = exported.SchemaJson,
                Options = new GameDBOperationOptions { AllowDestructive = true }
            });
            var created = GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "Replacement",
                Overwrite = true,
                Options = new GameDBOperationOptions { AllowDestructive = true }
            });

            foreach (var result in new[] { saved, created })
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message,
                    Does.Contain("format version 5").And.Contain("supported version 4"));
                AssertGenericFailure(result);
            }
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void Save_PartialExistingPairIsRejectedBeforeDestructiveAuthorization()
        {
            File.WriteAllText(m_databaseAbsolutePath, "{\"tables\":{}}");

            var result = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = "{\"tables\":{}}",
                SchemaJson = "{\"tables\":{},\"scope\":\"Replacement\",\"localizationDB\":false}"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("must both exist before replacement"));
            Assert.That(result.Message, Does.Not.Contain("AllowDestructive"));
            AssertGenericFailure(result);
        }

        [Test]
        public void Save_StaleRevisionIsRejectedBeforeDestructiveAuthorization()
        {
            CreateDatabase();
            var exported = GameDBAutomationService.ExportJson(m_databasePath);

            var result = GameDBAutomationService.Save(new GameDBSaveRequest
            {
                DatabasePath = m_databasePath,
                DataJson = exported.DataJson,
                SchemaJson = exported.SchemaJson,
                Options = new GameDBOperationOptions { ExpectedRevision = "stale-revision" }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Revision conflict"));
            Assert.That(result.Message, Does.Not.Contain("AllowDestructive"));
            AssertGenericFailure(result);
        }

        [Test]
        public void CreateOverwrite_StaleRevisionIsRejectedBeforeDestructiveAuthorization()
        {
            CreateDatabase();

            var result = GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "Replacement",
                Overwrite = true,
                Options = new GameDBOperationOptions { ExpectedRevision = "stale-revision" }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Revision conflict"));
            Assert.That(result.Message, Does.Not.Contain("AllowDestructive"));
            AssertGenericFailure(result);
        }

        [Test]
        public void GenerateCSharp_StaleRevisionIsRejectedBeforeOutputAuthorization()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            var outputPath = $"{m_assetFolderPath}/Generated";
            var scopeOutputAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "Generated", "CharacterizationDatabase");
            Directory.CreateDirectory(scopeOutputAbsolutePath);
            var markerPath = Path.Combine(scopeOutputAbsolutePath, "keep.txt");
            File.WriteAllText(markerPath, "keep");

            var result = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
            {
                DatabasePath = m_databasePath,
                OutputDirectory = outputPath,
                Options = new GameDBOperationOptions { ExpectedRevision = "stale-revision" }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Revision conflict"));
            Assert.That(result.Message, Does.Not.Contain("AllowDestructive"));
            AssertGenericFailure(result);
            Assert.That(File.ReadAllText(markerPath), Is.EqualTo("keep"));
        }

        [Test]
        public void GenerateCSharp_NonEmptyOutputRequiresAuthorizationOnlyWhenWriting()
        {
            CreateDatabase();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Items"
            }));
            var outputPath = $"{m_assetFolderPath}/Generated";
            var scopeOutputAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "Generated", "CharacterizationDatabase");
            Directory.CreateDirectory(scopeOutputAbsolutePath);
            var markerPath = Path.Combine(scopeOutputAbsolutePath, "keep.txt");
            File.WriteAllText(markerPath, "keep");

            var blocked = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
            {
                DatabasePath = m_databasePath,
                OutputDirectory = outputPath
            });
            var dryRun = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
            {
                DatabasePath = m_databasePath,
                OutputDirectory = outputPath,
                Options = new GameDBOperationOptions { DryRun = true }
            });

            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.Operation, Is.EqualTo("generateCSharp"));
            Assert.That(blocked.Message, Does.Contain("AllowDestructive"));
            AssertGenericFailure(blocked);
            Assert.That(File.ReadAllText(markerPath), Is.EqualTo("keep"));

            Assert.That(dryRun.Success, Is.True, dryRun.Message);
            Assert.That(dryRun.Operation, Is.EqualTo("generateCSharp"));
            Assert.That(dryRun.DryRun, Is.True);
            Assert.That(dryRun.RevisionBefore, Is.EqualTo(dryRun.RevisionAfter));
            Assert.That(dryRun.ChangedPaths, Is.EqualTo(new[] { outputPath + "/CharacterizationDatabase" }));
            Assert.That(File.ReadAllText(markerPath), Is.EqualTo("keep"));
        }

        private void CreateDatabase()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "CharacterizationDatabase"
            }));
        }

        private void CreateDatabaseWithIntValue(long value)
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
                Values = new Dictionary<string, object> { { "Power", value } }
            }));
        }

        private void WriteDatabasePair(string dataJson, string schemaJson)
        {
            File.WriteAllText(m_databaseAbsolutePath, dataJson);
            File.WriteAllText(m_schemaAbsolutePath, schemaJson);
            AssetDatabase.ImportAsset(m_databasePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(m_schemaPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ComputeRevision(string schemaJson, string dataJson)
        {
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(schemaJson + "\n" + dataJson);
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private string InspectRevision()
        {
            var result = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(result.Success, Is.True, result.Message);
            return result.Snapshot.Revision;
        }

        private void AssertChangedDatabasePaths(GameDBAutomationResult result)
        {
            Assert.That(result.ChangedPaths, Is.EqualTo(new[] { m_databasePath, m_schemaPath }));
        }

        private static void AssertGenericFailure(GameDBAutomationResult result)
        {
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.RevisionBefore, Is.Null);
            Assert.That(result.RevisionAfter, Is.Null);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.ChangedPaths, Is.Empty);
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
