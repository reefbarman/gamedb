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
        public void Save_CommandAuthoredScalarReferenceSchemaRoundTripsOnReload()
        {
            var document = GameDBDocument.CreateNew(m_databasePath,
                "PersistenceTests", false, GameDBFilePairStore.Instance,
                new RecordingPostSaveActions());
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("AuthoredScope", true),
                new AddTableCommand("Targets", KeyType.@string, null),
                new AddRowCommand("Targets", "Target1",
                    new Dictionary<string, object>()),
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Name",
                    new GameDBFieldTypeSpec(FieldType.@string, false, null)),
                new AddFieldCommand("Items", "Target",
                    new GameDBFieldTypeSpec(FieldType.tableRef, false, "Targets")),
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>
                {
                    { "Name", "Steel" },
                    { "Target", "Target1" }
                })
            }).Success, Is.True);

            var saved = document.Save();
            var loaded = GameDBDocument.Load(m_databasePath,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            var snapshot = loaded.CreateSnapshot();
            var items = snapshot.Tables.Single(table => table.Name == "Items");
            var row = items.Rows.Single(candidate => candidate.Key == "Sword");

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(snapshot.ScopeName, Is.EqualTo("AuthoredScope"));
            Assert.That(snapshot.LocalizationDatabase, Is.True);
            Assert.That(items.Fields.Select(field => new
            {
                field.Name,
                field.FieldType,
                field.TypeArgument
            }), Is.EqualTo(new[]
            {
                new { Name = "Name", FieldType = FieldType.@string, TypeArgument = (string)null },
                new { Name = "Target", FieldType = FieldType.tableRef, TypeArgument = "Targets" }
            }));
            Assert.That(row.Values["Name"], Is.EqualTo("Steel"));
            Assert.That(row.Values["Target"], Is.EqualTo("Target1"));
            Assert.That(loaded.IsDirty, Is.False);
        }

        [Test]
        public void Load_RejectsNewerSchemaFormatWithoutChangingFiles()
        {
            CreateSavedDocument();
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            File.WriteAllText(m_schemaAbsolutePath,
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 4", "\"formatVersion\": 5"));
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);

            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBDocument.Load(m_databasePath,
                    GameDBFilePairStore.Instance, new RecordingPostSaveActions()));

            Assert.That(exception.FoundVersion, Is.EqualTo(5));
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
                File.ReadAllText(m_schemaAbsolutePath).Replace("\"formatVersion\": 4", "\"formatVersion\": 5"));
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            LogAssert.Expect(LogType.Error, new Regex("^failed to load gameDB:"));
            LogAssert.Expect(LogType.Exception, new Regex(
                "Schema format version 5 is newer than the supported version 4"));

            Assert.That(legacy.Load($"{m_assetFolderName}/database.json"), Is.False);

            LogAssert.Expect(LogType.Error, new Regex("^failed to save gameDB:"));
            LogAssert.Expect(LogType.Exception, new Regex(
                "Schema format version 5 is newer than the supported version 4"));
            Assert.That(legacy.Save(), Is.False);
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
        }


        [Test]
        public void Save_NormalizesUnityObjectPathsAcrossAllShapesAndConverges()
        {
            var originalPath = CreateUnityObjectAsset("Sword");
            var guid = AssetDatabase.AssetPathToGUID(originalPath);
            var document = CreateUnityObjectDocument(guid, originalPath);
            var firstSave = document.Save();
            Assert.That(firstSave.Success, Is.True, firstSave.Message);

            var addressablesPath = $"{m_assetFolderPath}/Addressables";
            AssetDatabase.CreateFolder(m_assetFolderPath, "Addressables");
            AssetDatabase.CreateFolder(addressablesPath, "Items");
            var movedPath = $"{addressablesPath}/Items/RenamedSword.asset";
            Assert.That(AssetDatabase.MoveAsset(originalPath, movedPath), Is.Empty);
            var normalizedSave = document.Save();
            var secondSave = document.Save();
            var snapshot = document.CreateSnapshot();
            var row = snapshot.Tables.Single(table => table.Name == "Items")
                .Rows.Single(item => item.Key == "Sword");

            Assert.That(normalizedSave.Success, Is.True, normalizedSave.Message);
            Assert.That(normalizedSave.Status, Is.EqualTo(GameDBSaveStatus.Saved));
            Assert.That(normalizedSave.RevisionSaved, Is.Not.EqualTo(firstSave.RevisionSaved));
            Assert.That(secondSave.Success, Is.True, secondSave.Message);
            Assert.That(secondSave.Status, Is.EqualTo(GameDBSaveStatus.NoChanges));
            Assert.That(document.CurrentRevision, Is.EqualTo(normalizedSave.RevisionSaved));
            Assert.That(document.IsDirty, Is.False);
            AssertReferencePath(row.Values["Icon"], movedPath);
            Assert.That(((IEnumerable<object>)row.Values["Icons"])
                .Cast<UnityObjectReference>().Single().Path, Is.EqualTo(movedPath));
            Assert.That(((Dictionary<object, object>)row.Values["IconsBySlot"])["primary"],
                Is.TypeOf<UnityObjectReference>());
            Assert.That(((UnityObjectReference)((Dictionary<object, object>)
                row.Values["IconsBySlot"])["primary"]).Path, Is.EqualTo(movedPath));

            var persisted = (IDictionary<string, object>)JsonSerialization.Deserialize(
                File.ReadAllText(m_databaseAbsolutePath));
            var tables = (IDictionary<string, object>)persisted["tables"];
            var items = (IDictionary<string, object>)tables["Items"];
            var persistedRow = (IDictionary<string, object>)items["Sword"];
            AssertReferencePath(persistedRow["Icon"], movedPath);
        }

        [Test]
        public void Save_MissingUnityObjectGuidLeavesFilesAndLiveModelUnchanged()
        {
            var assetPath = CreateUnityObjectAsset("Sword");
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var document = CreateUnityObjectDocument(guid, assetPath);
            Assert.That(document.Save().Success, Is.True);
            var dataBefore = File.ReadAllBytes(m_databaseAbsolutePath);
            var schemaBefore = File.ReadAllBytes(m_schemaAbsolutePath);
            var revisionBefore = document.CurrentRevision;
            var pathBefore = GetSnapshotReference(document, "Icon").Path;
            Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True);

            var result = document.Save(new GameDBSaveOptions { ForceWrite = true });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.SerializationFailed));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.Message, Does.Contain("Items[Sword].Icon")
                .And.Contain(guid).And.Contain("missing"));
            Assert.That(File.ReadAllBytes(m_databaseAbsolutePath), Is.EqualTo(dataBefore));
            Assert.That(File.ReadAllBytes(m_schemaAbsolutePath), Is.EqualTo(schemaBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(GetSnapshotReference(document, "Icon").Path, Is.EqualTo(pathBefore));
        }

        [Test]
        public void Save_UnityObjectFolderReferenceLeavesFilesAndLiveModelUnchanged()
        {
            var folderPath = $"{m_assetFolderPath}/Folder.asset";
            Assert.That(AssetDatabase.CreateFolder(m_assetFolderPath, "Folder.asset"), Is.Not.Empty);
            var guid = AssetDatabase.AssetPathToGUID(folderPath);
            var document = CreateUnityObjectDocument(guid, folderPath);
            var revisionBefore = document.CurrentRevision;
            var pathBefore = GetSnapshotReference(document, "Icon").Path;

            var result = document.Save();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.SerializationFailed));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.Message, Does.Contain("Items[Sword].Icon")
                .And.Contain(guid).And.Contain("folder"));
            Assert.That(File.Exists(m_databaseAbsolutePath), Is.False);
            Assert.That(File.Exists(m_schemaAbsolutePath), Is.False);
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(GetSnapshotReference(document, "Icon").Path, Is.EqualTo(pathBefore));
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
            var stateBefore = document.GetSessionState();
            var changes = new List<GameDBDocumentStateChange>();
            document.StateChanged += changes.Add;

            var result = document.Save(new GameDBSaveOptions { ForceWrite = true });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.Saved));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionSaved, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionCurrent, Is.EqualTo(revisionBefore));
            Assert.That(actions.Imports, Has.Count.EqualTo(2));
            Assert.That(actions.Notifications, Is.EqualTo(new[] { "PersistenceTests" }));
            AssertSaveStateChange(changes, result, stateBefore, document.GetSessionState());
        }

        [Test]
        public void Save_NoChangesAndConflictEachReportExactOutcomeWithoutStateMutation()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            var baseline = document.GetSessionState();
            var changes = new List<GameDBDocumentStateChange>();
            document.StateChanged += changes.Add;

            var noChanges = document.Save();
            store.SetPair(new byte[] { 1 }, new byte[] { 2 });
            var conflict = document.Save();

            Assert.That(noChanges.Status, Is.EqualTo(GameDBSaveStatus.NoChanges));
            Assert.That(conflict.Status, Is.EqualTo(GameDBSaveStatus.Conflict));
            Assert.That(changes, Has.Count.EqualTo(2));
            AssertSaveStateChange(new[] { changes[0] }, noChanges, baseline, baseline);
            AssertSaveStateChange(new[] { changes[1] }, conflict, baseline, baseline);
        }

        [Test]
        public void Save_PostSaveFailureRetriesAcrossJsonUtilityStateRoundTrip()
        {
            var failingActions = new RecordingPostSaveActions { FailDataImports = 1 };
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, failingActions);

            var firstChanges = new List<GameDBDocumentStateChange>();
            document.StateChanged += firstChanges.Add;
            var firstState = document.GetSessionState();
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
            var restoredInitial = restored.GetSessionState();
            var retryChanges = new List<GameDBDocumentStateChange>();
            restored.StateChanged += retryChanges.Add;

            Assert.That(first.Success, Is.False);
            Assert.That(first.Status, Is.EqualTo(GameDBSaveStatus.PostSavePending));
            Assert.That(first.FilesCommitted, Is.True);
            Assert.That(document.HasPendingPostSaveWork, Is.True);
            Assert.That(document.IsDirty, Is.False);
            Assert.That(serializedState.DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(restored.DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(restored.HasPendingPostSaveWork, Is.True);
            Assert.That(firstChanges, Has.Count.EqualTo(1));
            Assert.That(firstChanges[0].Current.HasPendingPostSaveWork, Is.True);
            Assert.That(firstChanges[0].Current.IsDirty, Is.False);
            AssertSaveStateChange(firstChanges, first, firstState,
                document.GetSessionState());
            Assert.That(restoredInitial.HasPendingPostSaveWork, Is.True);

            var retried = restored.Save();

            Assert.That(retried.Success, Is.True, retried.Message);
            Assert.That(retried.Status, Is.EqualTo(GameDBSaveStatus.NoChanges));
            Assert.That(restored.HasPendingPostSaveWork, Is.False);
            Assert.That(retryActions.Imports, Is.EqualTo(new[] { m_databasePath }));
            Assert.That(retryActions.Notifications, Is.EqualTo(new[] { "PersistenceTests" }));
            AssertSaveStateChange(retryChanges, retried, restoredInitial,
                restored.GetSessionState());
            Assert.That(retryChanges[0].Current.HasPendingPostSaveWork, Is.False);
        }

        [Test]
        public void RestoreState_RejectsUnsupportedSchemaFormat()
        {
            var state = CreateSavedDocument().CaptureState();
            state.SchemaJson = state.SchemaJson.Replace(
                "\"formatVersion\": 4", "\"formatVersion\": 5");

            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBDocument.RestoreState(state));

            Assert.That(exception.FoundVersion, Is.EqualTo(5));
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
        public void Save_PendingRetryRecoveryLatchesUnknownAndClearsPendingState()
        {
            var store = new InMemoryPairStore();
            var actions = new RecordingPostSaveActions { FailDataImports = 1 };
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, actions);
            Assert.That(document.Save().Status, Is.EqualTo(GameDBSaveStatus.PostSavePending));
            var stateBefore = document.GetSessionState();
            var artifacts = new[] { "database.interrupted.tmp" };
            store.NextReadException = new GameDBRecoveryRequiredException(artifacts);
            var changes = new List<GameDBDocumentStateChange>();
            document.StateChanged += changes.Add;

            var result = document.Save();

            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(result.PostSavePending, Is.False);
            Assert.That(result.RecoveryArtifacts, Is.EqualTo(artifacts));
            Assert.That(document.HasPendingPostSaveWork, Is.False);
            Assert.That(document.GetSessionState().PersistenceStateUnknown, Is.True);
            AssertSaveStateChange(changes, result, stateBefore,
                document.GetSessionState());
        }

        [Test]
        public void Save_RecoveryArtifactsLatchUnknownStateAndRoundTrip()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            var artifacts = new[] { "database.interrupted.tmp" };
            store.NextReadException = new GameDBRecoveryRequiredException(artifacts);
            var changes = new List<GameDBDocumentStateChange>();
            document.StateChanged += changes.Add;
            var stateBefore = document.GetSessionState();

            var result = document.Save();
            var restored = GameDBDocument.RestoreState(document.CaptureState(),
                store, new RecordingPostSaveActions());
            var readsBeforeRetry = store.ReadCount;
            var retry = restored.Save();

            Assert.That(result.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(result.RecoveryArtifacts, Is.EqualTo(artifacts));
            AssertSaveStateChange(changes, result, stateBefore,
                document.GetSessionState());
            Assert.That(document.GetSessionState().PersistenceStateUnknown, Is.True);
            Assert.That(restored.GetSessionState().PersistenceStateUnknown, Is.True);
            Assert.That(retry.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(store.ReadCount, Is.EqualTo(readsBeforeRetry));
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

            var firstChanges = new List<GameDBDocumentStateChange>();
            document.StateChanged += firstChanges.Add;
            var firstState = document.GetSessionState();
            var first = document.Save();
            var restored = GameDBDocument.RestoreState(document.CaptureState(),
                store, new RecordingPostSaveActions());
            var secondChanges = new List<GameDBDocumentStateChange>();
            restored.StateChanged += secondChanges.Add;
            var secondState = restored.GetSessionState();
            var second = restored.Save();

            Assert.That(first.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(first.RecoveryArtifacts, Is.EqualTo(new[] { "database.tmp" }));
            Assert.That(second.Status, Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(store.CommitCount, Is.EqualTo(1));
            AssertSaveStateChange(firstChanges, first, firstState,
                document.GetSessionState());
            Assert.That(firstChanges[0].Current.PersistenceStateUnknown, Is.True);
            AssertSaveStateChange(secondChanges, second, secondState, secondState);
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
        public void ProbeDiskState_Unchanged_WhenPairMatchesBaselineAndWorkingCopyIsDirty()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("DirtyWorkingCopy", false)
            }).Success, Is.True);

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.Unchanged));
            Assert.That(result.ObservedToken, Is.EqualTo(result.BaselineToken));
            Assert.That(document.IsDirty, Is.True);
        }

        [Test]
        public void ProbeDiskState_Unchanged_ForNeverSavedAbsentPair()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "Unsaved", false,
                store, new RecordingPostSaveActions());

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.Unchanged));
            Assert.That(result.ObservedToken.HasValue, Is.True);
            Assert.That(result.ObservedToken.Value.DataExists, Is.False);
            Assert.That(result.ObservedToken.Value.SchemaExists, Is.False);
        }

        [Test]
        public void ProbeDiskState_Modified_WhenCompletePairDiffersFromBaseline()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            store.SetPair(new byte[] { 1 }, new byte[] { 2 });

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.Modified));
            Assert.That(result.ObservedToken.HasValue, Is.True);
            Assert.That(result.ObservedToken.Value, Is.Not.EqualTo(result.BaselineToken));
            Assert.That(result.ObservedToken.Value.DataExists, Is.True);
            Assert.That(result.ObservedToken.Value.SchemaExists, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ProbeDiskState_MissingOrIncomplete_WhenPairIsNotComplete(
            bool dataExists, bool schemaExists)
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            store.SetPair(dataExists ? new byte[] { 1 } : null,
                schemaExists ? new byte[] { 2 } : null);

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.MissingOrIncomplete));
            Assert.That(result.ObservedToken.HasValue, Is.True);
            Assert.That(result.ObservedToken.Value.DataExists, Is.EqualTo(dataExists));
            Assert.That(result.ObservedToken.Value.SchemaExists, Is.EqualTo(schemaExists));
        }

        [Test]
        public void ProbeDiskState_MissingOrIncomplete_WhenRestoredPartialBaselineMatchesDisk()
        {
            var store = new InMemoryPairStore();
            var original = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            var state = original.CaptureState();
            store.SetPair(new byte[] { 1 }, null);
            var partialToken = store.Read(m_databasePath).Token;
            state.BaselineDiskToken = partialToken;
            var document = GameDBDocument.RestoreState(state,
                store, new RecordingPostSaveActions());

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.MissingOrIncomplete));
            Assert.That(result.BaselineToken, Is.EqualTo(partialToken));
            Assert.That(result.ObservedToken, Is.EqualTo(partialToken));
        }

        [Test]
        public void ProbeDiskState_RecoveryRequired_WhenReadFindsArtifacts()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            store.NextReadException = new GameDBRecoveryRequiredException(
                new[] { "database.interrupted.tmp" });

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.RecoveryRequired));
            Assert.That(result.ObservedToken.HasValue, Is.False);
            Assert.That(result.RecoveryArtifacts,
                Is.EqualTo(new[] { "database.interrupted.tmp" }));
        }

        [Test]
        public void ProbeDiskState_ReadFailed_WhenStoreReadThrows()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            store.NextReadException = new IOException("probe read failed");

            var result = ProbeDiskStateReadOnly(document, store, 1);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.ReadFailed));
            Assert.That(result.Message, Is.EqualTo("probe read failed"));
            Assert.That(result.ObservedToken.HasValue, Is.False);
            Assert.That(result.RecoveryArtifacts, Is.Empty);
        }

        [Test]
        public void ProbeDiskState_RecoveryRequired_WhenPersistenceStateIsUnknownWithoutReading()
        {
            var store = new InMemoryPairStore
            {
                NextCommit = new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.StateUnknown,
                    Message = "ambiguous write"
                }
            };
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Status,
                Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));

            var result = ProbeDiskStateReadOnly(document, store, 0);

            Assert.That(result.State, Is.EqualTo(GameDBDiskState.RecoveryRequired));
            Assert.That(result.ObservedToken.HasValue, Is.False);
            Assert.That(result.Message, Does.Contain("unknown"));
        }

        [Test]
        public void ReplaceWorkingState_PreservesPendingPostSaveStateAndBaseline()
        {
            var store = new InMemoryPairStore();
            var actions = new RecordingPostSaveActions { FailDataImports = 1 };
            var document = GameDBDocument.CreateNew(m_databasePath, "PendingState", false,
                store, actions);
            var save = document.Save();
            Assert.That(save.Status, Is.EqualTo(GameDBSaveStatus.PostSavePending));
            var savedState = document.SerializeCurrent();
            var changed = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("ChangedPendingState", false)
            });
            Assert.That(changed.Success, Is.True, changed.Message);
            var before = document.CaptureState();

            var result = document.ReplaceWorkingState(savedState.DataJson, savedState.SchemaJson,
                document.CurrentRevision, GameDBDocumentChangeOrigin.Undo);
            var after = document.CaptureState();

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(after.DocumentId, Is.EqualTo(before.DocumentId));
            Assert.That(after.AssetPath, Is.EqualTo(before.AssetPath));
            Assert.That(after.BaselineRevision, Is.EqualTo(before.BaselineRevision));
            Assert.That(after.BaselineDiskToken, Is.EqualTo(before.BaselineDiskToken));
            Assert.That(after.DataImportPending, Is.EqualTo(before.DataImportPending));
            Assert.That(after.SchemaImportPending, Is.EqualTo(before.SchemaImportPending));
            Assert.That(after.CallbackPending, Is.EqualTo(before.CallbackPending));
            Assert.That(after.PendingScopeName, Is.EqualTo(before.PendingScopeName));
            Assert.That(after.PersistenceStateUnknown,
                Is.EqualTo(before.PersistenceStateUnknown));
            Assert.That(document.HasPendingPostSaveWork, Is.True);
            Assert.That(after.WasDirty, Is.False);
        }

        [Test]
        public void ReplaceWorkingState_PreservesUnknownPersistenceStateAndBaseline()
        {
            var store = new InMemoryPairStore
            {
                NextCommit = new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.StateUnknown,
                    Message = "ambiguous write"
                }
            };
            var document = GameDBDocument.CreateNew(m_databasePath, "UnknownState", false,
                store, new RecordingPostSaveActions());
            var savedState = document.SerializeCurrent();
            Assert.That(document.Save().Status,
                Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            var changed = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("ChangedUnknownState", false)
            });
            Assert.That(changed.Success, Is.True, changed.Message);
            var before = document.CaptureState();

            var result = document.ReplaceWorkingState(savedState.DataJson, savedState.SchemaJson,
                document.CurrentRevision, GameDBDocumentChangeOrigin.Recovery);
            var after = document.CaptureState();

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(after.DocumentId, Is.EqualTo(before.DocumentId));
            Assert.That(after.AssetPath, Is.EqualTo(before.AssetPath));
            Assert.That(after.BaselineRevision, Is.EqualTo(before.BaselineRevision));
            Assert.That(after.BaselineDiskToken, Is.EqualTo(before.BaselineDiskToken));
            Assert.That(after.PersistenceStateUnknown, Is.True);
            Assert.That(after.DataImportPending, Is.EqualTo(before.DataImportPending));
            Assert.That(after.SchemaImportPending, Is.EqualTo(before.SchemaImportPending));
            Assert.That(after.CallbackPending, Is.EqualTo(before.CallbackPending));
            Assert.That(after.PendingScopeName, Is.EqualTo(before.PendingScopeName));
            Assert.That(after.WasDirty, Is.EqualTo(before.WasDirty));
            Assert.That(document.Save().Status,
                Is.EqualTo(GameDBSaveStatus.PersistenceStateUnknown));
            Assert.That(store.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ReplaceWorkingState_AfterSavePreservesNewBaselineAndMakesOldContentDirty()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "First", false,
                store, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            var oldContent = document.SerializeCurrent();
            var changed = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("Second", false)
            });
            Assert.That(changed.Success, Is.True, changed.Message);
            Assert.That(document.Save().Success, Is.True);
            var savedBaseline = document.CaptureState();

            var result = document.ReplaceWorkingState(oldContent.DataJson, oldContent.SchemaJson,
                document.CurrentRevision, GameDBDocumentChangeOrigin.Undo);
            var restored = document.CaptureState();

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.AttemptedSnapshot.ScopeName, Is.EqualTo("First"));
            Assert.That(restored.BaselineRevision, Is.EqualTo(savedBaseline.BaselineRevision));
            Assert.That(restored.BaselineDiskToken,
                Is.EqualTo(savedBaseline.BaselineDiskToken));
            Assert.That(restored.WasDirty, Is.True);
            Assert.That(document.IsDirty, Is.True);
            Assert.That(document.CurrentRevision, Is.EqualTo(oldContent.Revision));
            Assert.That(document.BaselineRevision, Is.Not.EqualTo(oldContent.Revision));
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
        public void Save_ReentrantAttemptsFromCommitAndStateSubscriberAreRejected()
        {
            var store = new InMemoryPairStore();
            var actions = new RecordingPostSaveActions();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, actions);
            GameDBSaveOutcome commitNested = null;
            GameDBSaveOutcome subscriberNested = null;
            var stateNotifications = 0;
            store.OnCommit = () => commitNested = document.Save();
            document.StateChanged += change =>
            {
                stateNotifications++;
                subscriberNested = document.Save();
            };

            var saved = document.Save();

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(commitNested.Status, Is.EqualTo(GameDBSaveStatus.SaveInProgress));
            Assert.That(subscriberNested.Status, Is.EqualTo(GameDBSaveStatus.SaveInProgress));
            Assert.That(store.CommitCount, Is.EqualTo(1));
            Assert.That(actions.Imports, Has.Count.EqualTo(2));
            Assert.That(actions.Notifications, Has.Count.EqualTo(1));
            Assert.That(stateNotifications, Is.EqualTo(1));
        }

        [Test]
        public void Save_ReentrantAttemptsFromPostSaveCallbacksAreRejected()
        {
            var store = new InMemoryPairStore();
            var actions = new RecordingPostSaveActions();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, actions);
            var nested = new List<GameDBSaveOutcome>();
            actions.OnImport = _ => nested.Add(document.Save());
            actions.OnNotify = _ => nested.Add(document.Save());

            var saved = document.Save();

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(nested, Has.Count.EqualTo(3));
            Assert.That(nested.Select(result => result.Status),
                Is.All.EqualTo(GameDBSaveStatus.SaveInProgress));
            Assert.That(store.CommitCount, Is.EqualTo(1));
            Assert.That(actions.Imports, Has.Count.EqualTo(2));
            Assert.That(actions.Notifications, Has.Count.EqualTo(1));
        }

        [Test]
        public void Save_BindsBaselineToWrittenRevisionWhenDocumentAdvancesDuringCommit()
        {
            var store = new InMemoryPairStore();
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                store, new RecordingPostSaveActions());
            var revisionWritten = document.CurrentRevision;
            var observed = new List<GameDBDocumentStateChange>();
            var saveGateAvailable = false;
            GameDBTransactionResult transaction = null;
            document.StateChanged += change =>
            {
                observed.Add(change);
                if (change.Origin == GameDBDocumentStateChangeOrigin.Save)
                {
                    saveGateAvailable = document.ProbeDiskState().State
                        == GameDBDiskState.Unchanged;
                }
            };
            store.OnCommit = () =>
            {
                transaction = document.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("Later", KeyType.@string, null)
                });
                Assert.That(transaction.Success, Is.True, transaction.Message);
            };

            var saved = document.Save();

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(saved.RevisionSaved, Is.EqualTo(revisionWritten));
            Assert.That(saved.RevisionCurrent, Is.EqualTo(document.CurrentRevision));
            Assert.That(saved.RevisionCurrent, Is.Not.EqualTo(revisionWritten));
            Assert.That(document.BaselineRevision, Is.EqualTo(revisionWritten));
            Assert.That(document.IsDirty, Is.True);
            Assert.That(observed.Select(change => change.Origin), Is.EqualTo(new[]
            {
                GameDBDocumentStateChangeOrigin.Transaction,
                GameDBDocumentStateChangeOrigin.Save
            }));
            Assert.That(observed[1].Previous, Is.EqualTo(observed[0].Current));
            Assert.That(observed[1].Current.CurrentRevision,
                Is.EqualTo(transaction.AttemptedRevision));
            Assert.That(observed[1].Current.BaselineRevision, Is.EqualTo(revisionWritten));
            Assert.That(observed[1].Current.IsDirty, Is.True);
            Assert.That(observed[1].Current.HasPendingPostSaveWork, Is.False);
            Assert.That(transaction.NotificationErrorsDeferred, Is.True);
            Assert.That(transaction.NotificationErrors, Is.Empty);
            Assert.That(saved.NotificationErrorsDeferred, Is.False);
            Assert.That(saveGateAvailable, Is.True);
        }

        private GameDBDocument CreateUnityObjectDocument(string guid, string path)
        {
            var reference = new Dictionary<string, object>
            {
                { "guid", guid },
                { "path", path }
            };
            var document = GameDBDocument.CreateNew(m_databasePath,
                "UnityObjectPersistenceTests", false,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Icon",
                    new GameDBFieldTypeSpec(FieldType.unityObject, false, null)),
                new AddFieldCommand("Items", "Icons",
                    new GameDBFieldTypeSpec(FieldType.unityObject, true, null)),
                new AddFieldCommand("Items", "IconsBySlot",
                    new GameDBFieldTypeSpec(FieldType.dictionary, false, null,
                        new GameDBDictionaryTypeSpec(KeyType.@string, null,
                            FieldType.unityObject, null))),
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>
                {
                    { "Icon", reference },
                    { "Icons", new List<object> { reference } },
                    { "IconsBySlot", new Dictionary<string, object>
                        {
                            { "primary", reference }
                        }
                    }
                })
            });
            Assert.That(result.Success, Is.True, result.Message);
            return document;
        }

        private string CreateUnityObjectAsset(string name)
        {
            var resourcesPath = $"{m_assetFolderPath}/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder(m_assetFolderPath, "Resources");
            }

            var itemsPath = $"{resourcesPath}/Items";
            if (!AssetDatabase.IsValidFolder(itemsPath))
            {
                AssetDatabase.CreateFolder(resourcesPath, "Items");
            }

            var path = $"{itemsPath}/{name}.asset";
            var asset = ScriptableObject.CreateInstance<UnityObjectTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static UnityObjectReference GetSnapshotReference(
            GameDBDocument document, string fieldName)
        {
            var row = document.CreateSnapshot().Tables
                .Single(table => table.Name == "Items").Rows
                .Single(item => item.Key == "Sword");
            return (UnityObjectReference)row.Values[fieldName];
        }

        private static void AssertReferencePath(object value, string expectedPath)
        {
            if (value is UnityObjectReference reference)
            {
                Assert.That(reference.Path, Is.EqualTo(expectedPath));
                return;
            }

            var wire = (IDictionary<string, object>)value;
            Assert.That(wire["path"], Is.EqualTo(expectedPath));
        }

        private static void AssertSaveStateChange(
            IReadOnlyCollection<GameDBDocumentStateChange> changes,
            GameDBSaveOutcome outcome, GameDBDocumentSessionState expectedPrevious,
            GameDBDocumentSessionState expectedCurrent)
        {
            Assert.That(changes.Count, Is.EqualTo(1));
            var change = changes.Single();
            Assert.That(change.Origin, Is.EqualTo(GameDBDocumentStateChangeOrigin.Save));
            Assert.That(change.SaveStatus, Is.EqualTo(outcome.Status));
            Assert.That(change.Message, Is.EqualTo(outcome.Message));
            Assert.That(change.FilesCommitted, Is.EqualTo(outcome.FilesCommitted));
            Assert.That(change.RecoveryArtifacts, Is.EqualTo(outcome.RecoveryArtifacts));
            Assert.That(change.Previous, Is.EqualTo(expectedPrevious));
            Assert.That(change.Current, Is.EqualTo(expectedCurrent));
        }

        private GameDBDocument CreateSavedDocument()
        {
            var document = GameDBDocument.CreateNew(m_databasePath, "PersistenceTests", false,
                GameDBFilePairStore.Instance, new RecordingPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
            return document;
        }

        private static GameDBDiskStateResult ProbeDiskStateReadOnly(
            GameDBDocument document, InMemoryPairStore store, int expectedReads)
        {
            var stateBefore = JsonUtility.ToJson(document.CaptureState());
            var currentRevisionBefore = document.CurrentRevision;
            var baselineRevisionBefore = document.BaselineRevision;
            var dirtyBefore = document.IsDirty;
            var pendingBefore = document.HasPendingPostSaveWork;
            var commitsBefore = store.CommitCount;
            var readsBefore = store.ReadCount;
            var notifications = 0;
            Action<GameDBDocumentChange> onChanged = _ => notifications++;
            document.Changed += onChanged;

            GameDBDiskStateResult result;
            try
            {
                result = document.ProbeDiskState();
            }
            finally
            {
                document.Changed -= onChanged;
            }

            Assert.That(JsonUtility.ToJson(document.CaptureState()), Is.EqualTo(stateBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(currentRevisionBefore));
            Assert.That(document.BaselineRevision, Is.EqualTo(baselineRevisionBefore));
            Assert.That(document.IsDirty, Is.EqualTo(dirtyBefore));
            Assert.That(document.HasPendingPostSaveWork, Is.EqualTo(pendingBefore));
            Assert.That(store.CommitCount, Is.EqualTo(commitsBefore));
            Assert.That(store.ReadCount, Is.EqualTo(readsBefore + expectedReads));
            Assert.That(notifications, Is.Zero);
            return result;
        }

        private sealed class RecordingPostSaveActions : IGameDBPostSaveActions
        {
            internal List<string> Imports { get; } = new List<string>();
            internal List<string> Notifications { get; } = new List<string>();
            internal int FailDataImports { get; set; }
            internal Action<string> OnImport { get; set; }
            internal Action<string> OnNotify { get; set; }

            public void Import(string assetPath)
            {
                if (assetPath.EndsWith("database.json", StringComparison.Ordinal)
                    && !assetPath.EndsWith("database.schema.json", StringComparison.Ordinal)
                    && FailDataImports-- > 0)
                {
                    throw new IOException("data import failed");
                }

                Imports.Add(assetPath);
                OnImport?.Invoke(assetPath);
            }

            public void Notify(string scopeName)
            {
                Notifications.Add(scopeName);
                OnNotify?.Invoke(scopeName);
            }
        }

        private sealed class InMemoryPairStore : IGameDBPairStore
        {
            private byte[] m_dataBytes;
            private byte[] m_schemaBytes;

            internal GameDBPairCommitResult NextCommit { get; set; }
            internal Exception NextReadException { get; set; }
            internal Action OnCommit { get; set; }
            internal int CommitCount { get; private set; }
            internal int ReadCount { get; private set; }

            public StringComparer LockKeyComparer => StringComparer.Ordinal;

            public GameDBResolvedPath Resolve(string assetPath)
            {
                var schemaAssetPath = Path.ChangeExtension(assetPath, ".schema.json").Replace('\\', '/');
                return new GameDBResolvedPath(assetPath, schemaAssetPath,
                    assetPath.Substring("Assets/".Length), assetPath,
                    schemaAssetPath, assetPath);
            }

            public GameDBPairRead Read(string assetPath)
            {
                ReadCount++;
                if (NextReadException != null)
                {
                    var exception = NextReadException;
                    NextReadException = null;
                    throw exception;
                }

                return new GameDBPairRead(Resolve(assetPath),
                    m_dataBytes?.ToArray(), m_schemaBytes?.ToArray(),
                    Token(m_dataBytes, m_schemaBytes));
            }

            internal void SetPair(byte[] dataBytes, byte[] schemaBytes)
            {
                m_dataBytes = dataBytes?.ToArray();
                m_schemaBytes = schemaBytes?.ToArray();
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
