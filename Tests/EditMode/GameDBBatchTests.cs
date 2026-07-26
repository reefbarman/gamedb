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
    public class GameDBBatchTests
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
            m_assetFolderName = $"GameDBBatchTests_{Guid.NewGuid():N}";
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
        public void ApplyBatch_PersistsOrderedOperationsAndMapsEveryKind()
        {
            CreateDatabase();
            var revisionBefore = InspectRevision();
            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Temporary"),
                    Rename(GameDBBatchOperationKind.RenameTable, null, "Temporary", "Items"),
                    AddTable("Obsolete"),
                    Delete(GameDBBatchOperationKind.DeleteTable, null, "Obsolete"),
                    Field(GameDBBatchOperationKind.AddField, "Items", "Power", FieldType.@int),
                    Field(GameDBBatchOperationKind.ReplaceField, "Items", "Power", FieldType.@int),
                    Field(GameDBBatchOperationKind.AddField, "Items", "OldLabel", FieldType.@string),
                    Rename(GameDBBatchOperationKind.RenameField, "Items", "OldLabel", "Label"),
                    Field(GameDBBatchOperationKind.AddField, "Items", "Trash", FieldType.@string),
                    Delete(GameDBBatchOperationKind.DeleteField, "Items", "Trash"),
                    Row(GameDBBatchOperationKind.AddRow, "Items", "Sword",
                        new Dictionary<string, object> { { "Power", 10L }, { "Label", "Sword" } }),
                    Row(GameDBBatchOperationKind.UpdateRow, "Items", "Sword",
                        new Dictionary<string, object> { { "Power", 11L } }),
                    Value("Items", "Sword", "Label", "Sharp Sword"),
                    Rename(GameDBBatchOperationKind.RenameRow, "Items", "Sword", "Blade"),
                    Row(GameDBBatchOperationKind.AddRow, "Items", "DeleteMe", null),
                    Delete(GameDBBatchOperationKind.DeleteRow, "Items", "DeleteMe")
                },
                Options = new GameDBBatchOptions
                {
                    ExpectedRevision = revisionBefore,
                    AllowedDestructiveOperations = AllDestructiveKinds()
                }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo("applyBatch"));
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.None));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.Saved));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(-1));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.FilesCommitted, Is.True);
            Assert.That(result.PostSavePending, Is.False);
            Assert.That(result.ChangedPaths, Is.EquivalentTo(new[] { m_databasePath, m_schemaPath }));

            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            var table = inspected.Snapshot.Tables.Single();
            var row = table.Rows.Single();
            Assert.That(table.Name, Is.EqualTo("Items"));
            Assert.That(table.Fields.Select(item => item.Name), Is.EquivalentTo(new[] { "Power", "Label" }));
            Assert.That(row.Key, Is.EqualTo("Blade"));
            Assert.That(row.Values["Power"], Is.EqualTo(11L));
            Assert.That(row.Values["Label"], Is.EqualTo("Sharp Sword"));
            Assert.That(inspected.Snapshot.Revision, Is.EqualTo(result.RevisionAfter));
        }

        [Test]
        public void ApplyBatch_DryRunReturnsProspectiveSnapshotWithoutWritingOrNotifying()
        {
            CreateDatabase();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var revisionBefore = InspectRevision();
            var savedScopes = new List<string>();
            GameDBEditor.OnGameDBSaved = savedScopes.Add;

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Items"),
                    Field(GameDBBatchOperationKind.AddField, "Items", "Power", FieldType.@int),
                    Row(GameDBBatchOperationKind.AddRow, "Items", "Sword",
                        new Dictionary<string, object> { { "Power", 12L } })
                },
                Options = new GameDBBatchOptions
                {
                    DryRun = true,
                    ExpectedRevision = revisionBefore
                }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.DryRun, Is.True);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.DryRun));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.Snapshot.Tables.Single().Rows.Single().Values["Power"], Is.EqualTo(12L));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(savedScopes, Is.Empty);
            Assert.That(GameDBAutomationService.Inspect(m_databasePath).Snapshot.Tables, Is.Empty);
        }

        [Test]
        public void ApplyBatch_CommandFailureReportsIndexAndRollsBackEntireBatch()
        {
            CreateDatabase();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var revisionBefore = InspectRevision();

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    AddTable("Items"),
                    AddTable("Items"),
                    AddTable("NeverApplied")
                }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.CommandFailed));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.NotAttempted));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.Tables.Select(table => table.Name), Is.EquivalentTo(new[] { "Items" }));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(inspected.Snapshot.Revision, Is.EqualTo(revisionBefore));
            Assert.That(inspected.Snapshot.Tables, Is.Empty);
        }

        [Test]
        public void ApplyBatch_RequiresAnExplicitAllowlistBeforeLoading()
        {
            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    Rename(GameDBBatchOperationKind.RenameTable, null, "Items", "Catalog")
                },
                Options = new GameDBBatchOptions
                {
                    AllowedDestructiveOperations = new List<GameDBBatchOperationKind>
                    {
                        GameDBBatchOperationKind.DeleteTable
                    }
                }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.AuthorizationDenied));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(0));
            Assert.That(result.DeniedOperationKind, Is.EqualTo(GameDBBatchOperationKind.RenameTable));
            Assert.That(result.Message, Does.Contain("RenameTable"));
            Assert.That(result.Message, Does.Not.Contain("does not exist"));
        }

        [Test]
        public void ApplyBatch_RejectsStaleRevisionWithoutApplyingAnyOperation()
        {
            CreateDatabase();
            var staleRevision = InspectRevision();
            AssertSuccess(GameDBAutomationService.AddTable(new GameDBTableRequest
            {
                DatabasePath = m_databasePath,
                TableName = "Existing"
            }));

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Rejected") },
                Options = new GameDBBatchOptions { ExpectedRevision = staleRevision }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.RevisionConflict));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(-1));
            var inspected = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(inspected.Snapshot.Tables.Select(table => table.Name), Is.EquivalentTo(new[] { "Existing" }));
        }

        [Test]
        public void ApplyBatch_RejectsMalformedUnionAndUndefinedAllowlistKinds()
        {
            CreateDatabase();
            var missingKind = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    new GameDBBatchOperation
                    {
                        Table = new GameDBBatchTableOperation { TableName = "Items" }
                    }
                }
            });
            var wrongPayload = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    new GameDBBatchOperation
                    {
                        Kind = GameDBBatchOperationKind.AddTable,
                        Field = new GameDBBatchFieldOperation()
                    }
                }
            });
            var multiplePayloads = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    new GameDBBatchOperation
                    {
                        Kind = GameDBBatchOperationKind.AddTable,
                        Table = new GameDBBatchTableOperation { TableName = "Items" },
                        Row = new GameDBBatchRowOperation { TableName = "Items", RowKey = "Sword" }
                    }
                }
            });
            var invalidAllowlist = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Items") },
                Options = new GameDBBatchOptions
                {
                    AllowedDestructiveOperations = new List<GameDBBatchOperationKind>
                    {
                        (GameDBBatchOperationKind)999
                    }
                }
            });

            Assert.That(missingKind.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(missingKind.FailedOperationIndex, Is.EqualTo(0));
            Assert.That(missingKind.Message, Does.Contain("Unspecified"));
            Assert.That(wrongPayload.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(wrongPayload.FailedOperationIndex, Is.EqualTo(0));
            Assert.That(multiplePayloads.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(multiplePayloads.FailedOperationIndex, Is.EqualTo(0));
            Assert.That(invalidAllowlist.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(invalidAllowlist.Message, Does.Contain("999"));
            Assert.That(GameDBAutomationService.Inspect(m_databasePath).Snapshot.Tables, Is.Empty);
        }

        [Test]
        public void ApplyBatch_RejectsNullEmptyAndMissingDatabaseRequests()
        {
            var nullRequest = GameDBAutomationService.ApplyBatch(null);
            var empty = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath
            });
            var missing = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Items") }
            });

            Assert.That(nullRequest.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(empty.FailureKind, Is.EqualTo(GameDBBatchFailureKind.InvalidRequest));
            Assert.That(missing.FailureKind, Is.EqualTo(GameDBBatchFailureKind.LoadFailed));
        }

        [Test]
        public void ApplyBatch_MapsUnsupportedSchemaFormatToLoadFailedWithoutWriting()
        {
            CreateDatabase();
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Items") }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.LoadFailed));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.NotAttempted));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Message, Does.Contain("format version 2").And.Contain("supported version 1"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void ApplyBatch_RecoveryRequiredReturnsArtifactPathsWithoutLoading()
        {
            CreateDatabase();
            var artifactPath = m_databaseAbsolutePath + ".interrupted.tmp";
            File.WriteAllText(artifactPath, "pending");

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Items") }
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.RecoveryRequired));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.NotAttempted));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(-1));
            Assert.That(result.RecoveryArtifacts, Is.EqualTo(new[] { artifactPath }));
            Assert.That(result.Snapshot, Is.Null);
        }

        [Test]
        public void ApplyBatch_CanonicalNoOpDoesNotRewriteFiles()
        {
            CreateDatabaseWithIntValue(12L);
            var dataWriteTimeBefore = File.GetLastWriteTimeUtc(m_databaseAbsolutePath);
            var schemaWriteTimeBefore = File.GetLastWriteTimeUtc(m_schemaAbsolutePath);
            var revisionBefore = InspectRevision();
            var savedScopes = new List<string>();
            GameDBEditor.OnGameDBSaved = savedScopes.Add;

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation>
                {
                    Value("Items", "Sword", "Power", 12L)
                },
                Options = new GameDBBatchOptions { ExpectedRevision = revisionBefore }
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.NoChanges));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.ChangedPaths, Is.Empty);
            Assert.That(result.RevisionAfter, Is.EqualTo(revisionBefore));
            Assert.That(File.GetLastWriteTimeUtc(m_databaseAbsolutePath), Is.EqualTo(dataWriteTimeBefore));
            Assert.That(File.GetLastWriteTimeUtc(m_schemaAbsolutePath), Is.EqualTo(schemaWriteTimeBefore));
            Assert.That(savedScopes, Is.Empty);
        }

        [Test]
        public void ApplyBatch_PostSaveFailureReportsCommittedFilesAndPendingWork()
        {
            CreateDatabase();
            GameDBEditor.OnGameDBSaved = _ => throw new InvalidOperationException("callback failed");

            var result = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
            {
                DatabasePath = m_databasePath,
                Operations = new List<GameDBBatchOperation> { AddTable("Items") }
            });
            GameDBEditor.OnGameDBSaved = null;

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBBatchFailureKind.CommitFailed));
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBBatchCommitStatus.PostSavePending));
            Assert.That(result.FilesCommitted, Is.True);
            Assert.That(result.PostSavePending, Is.True);
            Assert.That(result.PostSaveErrors, Has.Some.Contains("callback failed"));
            Assert.That(GameDBAutomationService.Inspect(m_databasePath).Snapshot.Tables.Single().Name,
                Is.EqualTo("Items"));
        }

        private void CreateDatabase()
        {
            AssertSuccess(GameDBAutomationService.Create(new GameDBCreateRequest
            {
                DatabasePath = m_databasePath,
                ScopeName = "BatchTestDatabase"
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

        private string InspectRevision()
        {
            var result = GameDBAutomationService.Inspect(m_databasePath);
            Assert.That(result.Success, Is.True, result.Message);
            return result.Snapshot.Revision;
        }

        private static GameDBBatchOperation AddTable(string tableName)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.AddTable,
                Table = new GameDBBatchTableOperation { TableName = tableName }
            };
        }

        private static GameDBBatchOperation Rename(GameDBBatchOperationKind kind, string tableName,
            string currentName, string newName)
        {
            return new GameDBBatchOperation
            {
                Kind = kind,
                Rename = new GameDBBatchRenameOperation
                {
                    TableName = tableName,
                    CurrentName = currentName,
                    NewName = newName
                }
            };
        }

        private static GameDBBatchOperation Delete(GameDBBatchOperationKind kind, string tableName,
            string name)
        {
            return new GameDBBatchOperation
            {
                Kind = kind,
                Delete = new GameDBBatchDeleteOperation { TableName = tableName, Name = name }
            };
        }

        private static GameDBBatchOperation Field(GameDBBatchOperationKind kind, string tableName,
            string fieldName, FieldType fieldType)
        {
            return new GameDBBatchOperation
            {
                Kind = kind,
                Field = new GameDBBatchFieldOperation
                {
                    TableName = tableName,
                    FieldName = fieldName,
                    FieldType = fieldType
                }
            };
        }

        private static GameDBBatchOperation Row(GameDBBatchOperationKind kind, string tableName,
            string rowKey, Dictionary<string, object> values)
        {
            return new GameDBBatchOperation
            {
                Kind = kind,
                Row = new GameDBBatchRowOperation
                {
                    TableName = tableName,
                    RowKey = rowKey,
                    Values = values
                }
            };
        }

        private static GameDBBatchOperation Value(string tableName, string rowKey,
            string fieldName, object value)
        {
            return new GameDBBatchOperation
            {
                Kind = GameDBBatchOperationKind.SetValue,
                Value = new GameDBBatchValueOperation
                {
                    TableName = tableName,
                    RowKey = rowKey,
                    FieldName = fieldName,
                    Value = value
                }
            };
        }

        private static List<GameDBBatchOperationKind> AllDestructiveKinds()
        {
            return new List<GameDBBatchOperationKind>
            {
                GameDBBatchOperationKind.RenameTable,
                GameDBBatchOperationKind.DeleteTable,
                GameDBBatchOperationKind.ReplaceField,
                GameDBBatchOperationKind.RenameField,
                GameDBBatchOperationKind.DeleteField,
                GameDBBatchOperationKind.RenameRow,
                GameDBBatchOperationKind.DeleteRow
            };
        }

        private static void AssertSuccess(GameDBAutomationResult result)
        {
            Assert.That(result.Success, Is.True, result.Message);
        }
    }
}
