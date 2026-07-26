using GameDBEditorLibrary;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    public class GameDBDocumentPersistenceTests
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
            m_assetFolderName = $"GameDBDocumentPersistenceTests_{Guid.NewGuid():N}";
            m_assetFolderPath = $"Assets/{m_assetFolderName}";
            m_assetFolderAbsolutePath = Path.Combine(Application.dataPath, m_assetFolderName);
            m_databasePath = $"{m_assetFolderPath}/database.json";
            m_databaseAbsolutePath = Path.Combine(m_assetFolderAbsolutePath, "database.json");
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
        public void Save_NewDocumentCommitsPairAndReloadsClean()
        {
            var actions = new RecordingPostSaveActions();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, actions);
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null)
            }).Success, Is.True);

            var saved = document.Save();
            var loaded = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(saved.Status, Is.EqualTo(GameDBSaveStatus.Saved));
            Assert.That(saved.FilesCommitted, Is.True);
            Assert.That(saved.ChangedPaths, Is.EqualTo(new[]
            {
                m_databasePath,
                $"{m_assetFolderPath}/database.schema.json"
            }));
            Assert.That(File.Exists(m_databaseAbsolutePath), Is.True);
            Assert.That(File.Exists(m_schemaAbsolutePath), Is.True);
            var schema = (IDictionary<string, object>)JsonSerialization.Deserialize(
                File.ReadAllText(m_schemaAbsolutePath));
            Assert.That(schema["formatVersion"], Is.EqualTo((long)GameDBSchemaFormat.CurrentVersion));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.BaselineRevision, Is.EqualTo(saved.RevisionSaved));
            Assert.That(loaded.IsDirty, Is.False);
            Assert.That(loaded.CurrentRevision, Is.EqualTo(saved.RevisionSaved));
            Assert.That(loaded.CreateSnapshot().Tables.Select(table => table.Name), Does.Contain("Items"));
            Assert.That(actions.Imports, Is.EqualTo(saved.ChangedPaths));
            Assert.That(actions.Notifications, Is.EqualTo(new[] { "PersistenceTests" }));
        }

        [Test]
        public void Load_RejectsNewerSchemaFormatWithoutChangingFiles()
        {
            CreateSavedDocument();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBDocument.Load(m_databasePath,
                    GameDBFilePairStore.Instance, new RecordingPostSaveActions()));

            Assert.That(exception.FoundVersion, Is.EqualTo(2));
            Assert.That(exception.Message, Does.Contain("newer GameDB package"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void LegacyLoad_RejectsNewerSchemaFormatAndCannotOverwriteItThroughStaleState()
        {
            CreateSavedDocument();
            var legacy = new GameDB();
            Assert.That(legacy.Load($"{m_assetFolderName}/database.json"), Is.True);
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            LogAssert.Expect(LogType.Error, new Regex("^failed to load gameDB:"));
            LogAssert.Expect(LogType.Exception, new Regex(
                "Schema format version 2 is newer than the supported version 1"));

            Assert.That(legacy.Load($"{m_assetFolderName}/database.json"), Is.False);

            LogAssert.Expect(LogType.Error, new Regex("^failed to save gameDB:"));
            LogAssert.Expect(LogType.Exception, new Regex(
                "Schema format version 2 is newer than the supported version 1"));
            Assert.That(legacy.Save(), Is.False);
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }

        [Test]
        public void LoadRuntimeDB_RejectsNewerSchemaFormatAndPreservesPreviousModel()
        {
            CreateSavedDocument();
            var legacy = new GameDB();
            Assert.That(legacy.Load($"{m_assetFolderName}/database.json"), Is.True);
            var tablesBefore = legacy.Tables;
            var scopeBefore = legacy.ScopeName;
            var pathBefore = legacy.LoadedPath;
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));
            LogAssert.Expect(LogType.Error, new Regex("^failed to load gameDB:"));
            LogAssert.Expect(LogType.Exception, new Regex(
                "Schema format version 2 is newer than the supported version 1"));

            Assert.That(legacy.LoadRuntimeDB(0, $"{m_assetFolderName}/database.json"), Is.False);
            Assert.That(legacy.Tables, Is.SameAs(tablesBefore));
            Assert.That(legacy.ScopeName, Is.EqualTo(scopeBefore));
            Assert.That(legacy.LoadedPath, Is.EqualTo(pathBefore));
        }

        [Test]
        public void Save_CleanDocumentStillDetectsFormattingOnlyExternalChange()
        {
            var document = CreateSavedDocument();
            var tokenBefore = GameDBFilePairStore.Instance.Read(m_databasePath).Token;
            File.AppendAllText(m_databaseAbsolutePath, "\n");

            var result = document.Save();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.Conflict));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.DiskTokenBefore, Is.EqualTo(tokenBefore));
            Assert.That(result.DiskTokenAfter, Is.Not.EqualTo(tokenBefore));
            Assert.That(document.IsDirty, Is.False);
        }

        [Test]
        public void Save_TwoLoadedDocumentsConflictAfterFirstCommit()
        {
            CreateSavedDocument();
            var first = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            var second = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(first.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("First", KeyType.@string, null)
            }).Success, Is.True);
            Assert.That(second.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Second", KeyType.@string, null)
            }).Success, Is.True);

            var firstSave = first.Save();
            var secondSave = second.Save();

            Assert.That(firstSave.Success, Is.True, firstSave.Message);
            Assert.That(secondSave.Success, Is.False);
            Assert.That(secondSave.Status, Is.EqualTo(GameDBSaveStatus.Conflict));
            Assert.That(second.IsDirty, Is.True);
            var reloaded = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(reloaded.CreateSnapshot().Tables.Select(table => table.Name),
                Does.Contain("First").And.Not.Contain("Second"));
        }

        [Test]
        public void Load_RejectsInterruptedSaveArtifacts()
        {
            CreateSavedDocument();
            var artifactPath = m_databaseAbsolutePath + ".interrupted.tmp";
            File.WriteAllText(artifactPath, "incomplete");

            var exception = Assert.Throws<GameDBRecoveryRequiredException>(() =>
                GameDBDocument.Load(m_databasePath,
                    GameDBFilePairStore.Instance, new RecordingPostSaveActions()));

            Assert.That(exception.Artifacts, Does.Contain(artifactPath));
        }

        [Test]
        public void Save_ForceWritePersistsAndNotifiesCleanDocument()
        {
            var document = CreateSavedDocument();
            var actions = new RecordingPostSaveActions();
            document = GameDBDocument.Load(m_databasePath, GameDBFilePairStore.Instance, actions);
            var revisionBefore = document.CurrentRevision;

            var result = document.Save(new GameDBSaveOptions { ForceWrite = true });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.Saved));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionSaved, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionCurrent, Is.EqualTo(revisionBefore));
            Assert.That(actions.Imports, Has.Count.EqualTo(2));
            Assert.That(actions.Notifications, Is.EqualTo(new[] { "PersistenceTests" }));
        }

        [Test]
        public void Save_PostSaveFailureRetriesAcrossJsonUtilityStateRoundTrip()
        {
            var failingActions = new RecordingPostSaveActions { FailDataImports = 1 };
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, failingActions);

            var first = document.Save();
            var capturedState = document.CaptureState();
            Assert.That(capturedState.DataImportPending, Is.True);
            Assert.That(capturedState.SchemaImportPending, Is.False);
            Assert.That(capturedState.CallbackPending, Is.True);
            var json = JsonUtility.ToJson(capturedState);
            var serializedState = JsonUtility.FromJson<GameDBDocumentState>(json);
            Assert.That(serializedState.DataImportPending, Is.True, json);
            Assert.That(serializedState.CallbackPending, Is.True, json);
            var retryActions = new RecordingPostSaveActions();
            var restored = GameDBDocument.RestoreState(serializedState,
                GameDBFilePairStore.Instance, retryActions);

            Assert.That(first.Success, Is.False);
            Assert.That(first.Status, Is.EqualTo(GameDBSaveStatus.PostSavePending));
            Assert.That(first.FilesCommitted, Is.True);
            Assert.That(document.HasPendingPostSaveWork, Is.True);
            Assert.That(document.IsDirty, Is.False);
            Assert.That(serializedState.DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(restored.DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(restored.HasPendingPostSaveWork, Is.True);

            var retried = restored.Save();

            Assert.That(retried.Success, Is.True, retried.Message);
            Assert.That(retried.Status, Is.EqualTo(GameDBSaveStatus.NoChanges));
            Assert.That(restored.HasPendingPostSaveWork, Is.False);
            Assert.That(retryActions.Imports, Is.EqualTo(new[] { m_databasePath }));
            Assert.That(retryActions.Notifications, Is.EqualTo(new[] { "PersistenceTests" }));
        }

        [Test]
        public void RestoreState_RejectsUnsupportedSchemaFormat()
        {
            var state = CreateSavedDocument().CaptureState();
            state.SchemaJson = state.SchemaJson.Replace(
                "\"formatVersion\": 1", "\"formatVersion\": 2");

            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBDocument.RestoreState(state));

            Assert.That(exception.FoundVersion, Is.EqualTo(2));
        }

        [Test]
        public void RestoreState_RejectsUnknownVersionAndDirtyCorruption()
        {
            var document = CreateSavedDocument();
            var state = document.CaptureState();
            state.Version++;
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));

            state = document.CaptureState();
            state.WasDirty = true;
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));
        }

        [Test]
        public void Save_UnknownPersistenceStateIsCapturedAndBlocksRetry()
        {
            var store = new InMemoryPairStore
            {
                NextCommit = new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.StateUnknown,
                    Message = "ambiguous write",
                    RecoveryArtifacts = new[] { "database.tmp" }
                }
            };
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());

            var first = document.Save();
            var restored = GameDBDocument.RestoreState(document.CaptureState(),
                store, new RecordingPostSaveActions());
            var second = restored.Save();

            Assert.That(first.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(first.RecoveryArtifacts, Is.EqualTo(new[] { "database.tmp" }));
            Assert.That(second.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(store.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateNewReplacement_RejectsAnExistingPair()
        {
            var store = new InMemoryPairStore();
            var existing = GameDBDocument.CreateNew(m_databasePath, "Existing", false,
                store, new RecordingPostSaveActions());
            Assert.That(existing.Save().Success, Is.True);
            var state = existing.SerializeCurrent();

            var exception = Assert.Throws<IOException>(() =>
                GameDBDocument.CreateNewReplacement(m_databasePath,
                    state.DataJson, state.SchemaJson, store, new RecordingPostSaveActions()));

            Assert.That(exception.Message, Does.Contain("already exist"));
            Assert.That(store.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void RestoreState_RejectsImpossiblePostSaveCombinations()
        {
            var document = CreateSavedDocument();
            var state = document.CaptureState();
            state.DataImportPending = true;
            state.CallbackPending = false;
            state.PendingScopeName = "PersistenceTests";
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));

            state = document.CaptureState();
            state.CallbackPending = true;
            state.PendingScopeName = "PersistenceTests";
            state.PersistenceStateUnknown = true;
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));

            state = document.CaptureState();
            state.PendingScopeName = "PersistenceTests";
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));

            var unsaved = GameDBDocument.CreateNew($"{m_assetFolderPath}/unsaved.json",
                "Unsaved", false);
            state = unsaved.CaptureState();
            state.CallbackPending = true;
            state.PendingScopeName = "Unsaved";
            Assert.Throws<FormatException>(() => GameDBDocument.RestoreState(state));
        }

        [Test]
        public void LegacyCreate_UsesSharedPersistenceAndFilenameScope()
        {
            var notifications = new List<string>();
            GameDBEditor.OnGameDBSaved = notifications.Add;
            var legacy = new GameDB();

            legacy.Create($"{m_assetFolderName}/database.json");
            var loaded = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());

            Assert.That(File.Exists(m_databaseAbsolutePath), Is.True);
            Assert.That(File.Exists(m_schemaAbsolutePath), Is.True);
            Assert.That(legacy.ScopeName, Is.EqualTo("database"));
            Assert.That(loaded.CreateSnapshot().ScopeName, Is.EqualTo("database"));
            Assert.That(notifications, Is.EqualTo(new[] { "database" }));
        }

        [Test]
        public void LegacySave_ConflictsWithDocumentCommitAfterLoad()
        {
            var existing = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(existing.Save().Success, Is.True);
            var legacy = new GameDB();
            Assert.That(legacy.Load($"{m_assetFolderName}/database.json"), Is.True);
            Assert.That(legacy.AddTable("Legacy", KeyType.@string), Is.True);

            var external = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(external.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("External", KeyType.@string, null)
            }).Success, Is.True);
            Assert.That(external.Save().Success, Is.True);

            LogAssert.Expect(LogType.Error, new Regex(
                "^failed to save gameDB: .*Database files changed after this document was loaded\\.$"));
            Assert.That(legacy.Save(), Is.False);
            var reloaded = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(reloaded.CreateSnapshot().Tables.Select(table => table.Name),
                Does.Contain("External").And.Not.Contain("Legacy"));
        }

        [Test]
        public void Save_BindsBaselineToWrittenRevisionWhenDocumentAdvancesDuringCommit()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            var revisionWritten = document.CurrentRevision;
            store.OnCommit = () =>
            {
                var result = document.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("Later", KeyType.@string, null)
                });
                Assert.That(result.Success, Is.True, result.Message);
            };

            var saved = document.Save();

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(saved.RevisionSaved, Is.EqualTo(revisionWritten));
            Assert.That(saved.RevisionCurrent, Is.EqualTo(document.CurrentRevision));
            Assert.That(saved.RevisionCurrent, Is.Not.EqualTo(revisionWritten));
            Assert.That(document.BaselineRevision, Is.EqualTo(revisionWritten));
            Assert.That(document.IsDirty, Is.True);
        }

        private GameDBDocument CreateSavedDocument()
        {
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            return document;
        }

        private sealed class RecordingPostSaveActions : IGameDBPostSaveActions
        {
            internal List<string> Imports { get; } = new List<string>();
            internal List<string> Notifications { get; } = new List<string>();
            internal int FailDataImports { get; set; }

            public void Import(string assetPath)
            {
                if (assetPath.EndsWith("database.json", StringComparison.Ordinal)
                    && !assetPath.EndsWith("database.schema.json", StringComparison.Ordinal)
                    && FailDataImports-- > 0)
                {
                    throw new IOException("data import failed");
                }

                Imports.Add(assetPath);
            }

            public void Notify(string scopeName)
            {
                Notifications.Add(scopeName);
            }
        }

        private sealed class InMemoryPairStore : IGameDBPairStore
        {
            private byte[] m_dataBytes;
            private byte[] m_schemaBytes;

            internal GameDBPairCommitResult NextCommit { get; set; }
            internal Action OnCommit { get; set; }
            internal int CommitCount { get; private set; }

            public GameDBResolvedPath Resolve(string assetPath)
            {
                var schemaAssetPath = Path.ChangeExtension(assetPath, ".schema.json").Replace('\\', '/');
                return new GameDBResolvedPath(assetPath, schemaAssetPath,
                    assetPath.Substring("Assets/".Length), assetPath,
                    schemaAssetPath, assetPath);
            }

            public GameDBPairRead Read(string assetPath)
            {
                return new GameDBPairRead(Resolve(assetPath), m_dataBytes, m_schemaBytes,
                    Token(m_dataBytes, m_schemaBytes));
            }

            public GameDBPairCommitResult Commit(string assetPath, GameDBDiskToken expectedToken,
                byte[] dataBytes, byte[] schemaBytes)
            {
                CommitCount++;
                var before = Token(m_dataBytes, m_schemaBytes);
                if (NextCommit != null)
                {
                    var result = NextCommit;
                    NextCommit = null;
                    result.TokenBefore = before;
                    result.TokenAfter = before;
                    return result;
                }

                Assert.That(before, Is.EqualTo(expectedToken));
                OnCommit?.Invoke();
                m_dataBytes = dataBytes.ToArray();
                m_schemaBytes = schemaBytes.ToArray();
                var after = Token(m_dataBytes, m_schemaBytes);
                return new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.Committed,
                    TokenBefore = before,
                    TokenAfter = after
                };
            }

            private static GameDBDiskToken Token(byte[] dataBytes, byte[] schemaBytes)
            {
                return new GameDBDiskToken
                {
                    DataExists = dataBytes != null,
                    SchemaExists = schemaBytes != null,
                    DataSha256 = dataBytes == null ? null : Hash(dataBytes),
                    SchemaSha256 = schemaBytes == null ? null : Hash(schemaBytes)
                };
            }

            private static string Hash(byte[] bytes)
            {
                using (var algorithm = System.Security.Cryptography.SHA256.Create())
                {
                    return string.Concat(algorithm.ComputeHash(bytes)
                        .Select(value => value.ToString("x2")));
                }
            }
        }
    }
}
