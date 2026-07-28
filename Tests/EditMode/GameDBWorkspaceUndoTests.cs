using GameDBEditorLibrary;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GameDBLibrary.Tests
{
    public class GameDBWorkspaceUndoTests
    {
        private const string AssetPath = "Assets/GameDBWorkspaceUndoTests/database.json";

        [Test]
        public void Workspace_UndoRedoRoutesToActiveSessionAndRestoredHistoryStartsEmpty()
        {
            var pairStore = new MemoryPairStore();
            var recoveryStore = new MemoryRecoveryStore();
            var source = CreateSavedDocument(pairStore);
            Assert.That(new GameDBWorkspaceRecoveryService(recoveryStore).Save(
                new GameDBWorkspaceRecoverySnapshot(new[]
                {
                    new GameDBWorkspaceRecoveryTab("active", source.CaptureState())
                }, "active")).Success, Is.True);

            using (var workspace = CreateWorkspace(pairStore, recoveryStore))
            {
                Assert.That(workspace.ActiveTab.Session.GetHistoryState().CanUndo, Is.False);
                Assert.That(SetPower(workspace.ActiveTab.Session, 20).Success, Is.True);

                var undo = workspace.UndoActiveDocument();
                Assert.That(undo.Success, Is.True, undo.Message);
                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(12L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.False);

                var redo = workspace.RedoActiveDocument();
                Assert.That(redo.Success, Is.True, redo.Message);
                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(20L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.True);
                Assert.That(workspace.PersistRecovery().Success, Is.True);
            }

            using (var restored = CreateWorkspace(pairStore, recoveryStore))
            {
                Assert.That(Power(restored.ActiveTab.Session), Is.EqualTo(20L));
                Assert.That(restored.ActiveTab.Session.GetState().IsDirty, Is.True);
                Assert.That(restored.ActiveTab.Session.GetHistoryState().CanUndo, Is.False);
                Assert.That(restored.ActiveTab.Session.GetHistoryState().CanRedo, Is.False);
            }
        }

        [Test]
        public void Workspace_ProbeAutoRefreshesCleanDocumentAndResetsHistory()
        {
            var pairStore = new MemoryPairStore();
            var recoveryStore = new MemoryRecoveryStore();
            var source = CreateSavedDocument(pairStore);
            SaveRecovery(recoveryStore, source);

            using (var workspace = CreateWorkspace(pairStore, recoveryStore))
            {
                pairStore.SetPair(CreateExternalState(30));

                workspace.ProbeActiveDocument();

                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(30L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.False);
                Assert.That(workspace.ActiveTab.Session.GetHistoryState().CanUndo, Is.False);
                Assert.That(workspace.LastDiskRefresh.Status,
                    Is.EqualTo(GameDBDiskRefreshStatus.Refreshed));
                Assert.That(workspace.LastDiskState.State, Is.EqualTo(GameDBDiskState.Unchanged));
            }
        }

        [Test]
        public void Workspace_ConfirmedReloadRejectsInterveningDocumentEdit()
        {
            var pairStore = new MemoryPairStore();
            var recoveryStore = new MemoryRecoveryStore();
            var source = CreateSavedDocument(pairStore);
            SaveRecovery(recoveryStore, source);

            using (var workspace = CreateWorkspace(pairStore, recoveryStore))
            {
                var observedRevision = workspace.ActiveTab.Session.GetState().CurrentRevision;
                Assert.That(SetPower(workspace.ActiveTab.Session, 40).Success, Is.True);
                pairStore.SetPair(CreateExternalState(50));

                var reloaded = workspace.ReloadActiveDocument(observedRevision, true);

                Assert.That(reloaded.Success, Is.False);
                Assert.That(reloaded.Status,
                    Is.EqualTo(GameDBDiskRefreshStatus.RevisionConflict));
                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(40L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.True);
            }
        }

        [Test]
        public void Workspace_ProbePreservesDirtyConflictUntilExplicitDiscardReload()
        {
            var pairStore = new MemoryPairStore();
            var recoveryStore = new MemoryRecoveryStore();
            var source = CreateSavedDocument(pairStore);
            SaveRecovery(recoveryStore, source);

            using (var workspace = CreateWorkspace(pairStore, recoveryStore))
            {
                Assert.That(SetPower(workspace.ActiveTab.Session, 40).Success, Is.True);
                pairStore.SetPair(CreateExternalState(50));

                workspace.ProbeActiveDocument();

                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(40L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.True);
                Assert.That(workspace.LastDiskRefresh.Status,
                    Is.EqualTo(GameDBDiskRefreshStatus.Conflict));
                Assert.That(workspace.LastDiskState.State, Is.EqualTo(GameDBDiskState.Modified));

                var reloaded = workspace.ReloadActiveDocument(
                    workspace.ActiveTab.Session.GetState().CurrentRevision, true);
                Assert.That(reloaded.Success, Is.True, reloaded.Message);
                Assert.That(Power(workspace.ActiveTab.Session), Is.EqualTo(50L));
                Assert.That(workspace.ActiveTab.Session.GetState().IsDirty, Is.False);
                Assert.That(workspace.ActiveTab.Session.GetHistoryState().CanUndo, Is.False);
                Assert.That(workspace.LastDiskState.State, Is.EqualTo(GameDBDiskState.Unchanged));
            }
        }

        private static GameDBEditorWorkspace CreateWorkspace(MemoryPairStore pairStore,
            MemoryRecoveryStore recoveryStore)
        {
            return new GameDBEditorWorkspace(new GameDBDocumentLeaseRegistry(pairStore),
                new GameDBWorkspaceRecoveryService(recoveryStore),
                new GameDBActiveWorkspaceHub());
        }

        private static void SaveRecovery(MemoryRecoveryStore store, GameDBDocument document)
        {
            Assert.That(new GameDBWorkspaceRecoveryService(store).Save(
                new GameDBWorkspaceRecoverySnapshot(new[]
                {
                    new GameDBWorkspaceRecoveryTab("active", document.CaptureState())
                }, "active")).Success, Is.True);
        }

        private static GameDBDocument CreateSavedDocument(MemoryPairStore store)
        {
            var document = GameDBDocument.CreateNew(AssetPath, "WorkspaceUndo", false,
                store, new NoOpPostSaveActions());
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Power",
                    new GameDBFieldTypeSpec(FieldType.@int, false, null)),
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>
                {
                    { "Power", 12 }
                })
            }).Success, Is.True);
            Assert.That(document.Save().Success, Is.True);
            return document;
        }

        private static GameDBSerializedState CreateExternalState(int power)
        {
            var model = new GameDB();
            model.CreateInMemory(AssetPath.Substring("Assets/".Length));
            model.ScopeName = "WorkspaceUndo";
            Assert.That(model.AddTable("Items", KeyType.@string), Is.True);
            var table = (TableModel)model.Tables["Items"];
            Assert.That(table.AddField("Power", FieldType.@int, false), Is.True);
            Assert.That(table.AddKey("Sword"), Is.True);
            Assert.That(table.SetValue("Sword", "Power", power), Is.True);
            return GameDBModelCodec.Serialize(model);
        }

        private static GameDBTransactionResult SetPower(GameDBAssetSession session, int value)
        {
            return session.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", value)
            });
        }

        private static long Power(GameDBAssetSession session)
        {
            return (long)session.CreateSnapshot().Tables.Single().Rows.Single().Values["Power"];
        }

        private sealed class MemoryRecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
            public string QuarantinePrimary()
            {
                m_contents = null;
                return "quarantine.json";
            }
            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }
        }

        private sealed class MemoryPairStore : IGameDBPairStore
        {
            private byte[] m_data;
            private byte[] m_schema;
            public StringComparer LockKeyComparer => StringComparer.Ordinal;

            public GameDBResolvedPath Resolve(string assetPath)
            {
                var schema = Path.ChangeExtension(assetPath, ".schema.json").Replace('\\', '/');
                return new GameDBResolvedPath(assetPath, schema,
                    assetPath.Substring("Assets/".Length), assetPath, schema, assetPath);
            }

            public GameDBPairRead Read(string assetPath)
            {
                return new GameDBPairRead(Resolve(assetPath), m_data?.ToArray(),
                    m_schema?.ToArray(), Token(m_data, m_schema));
            }

            public GameDBPairCommitResult Commit(string assetPath,
                GameDBDiskToken expectedToken, byte[] dataBytes, byte[] schemaBytes)
            {
                var before = Token(m_data, m_schema);
                if (before != expectedToken)
                {
                    return new GameDBPairCommitResult
                    {
                        Status = GameDBPairCommitStatus.Conflict,
                        TokenBefore = before,
                        TokenAfter = before
                    };
                }
                m_data = dataBytes.ToArray();
                m_schema = schemaBytes.ToArray();
                return new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.Committed,
                    TokenBefore = before,
                    TokenAfter = Token(m_data, m_schema)
                };
            }

            internal void SetPair(GameDBSerializedState state)
            {
                m_data = GameDBFilePairStore.Encode(state.DataJson);
                m_schema = GameDBFilePairStore.Encode(state.SchemaJson);
            }

            private static GameDBDiskToken Token(byte[] data, byte[] schema)
            {
                return new GameDBDiskToken
                {
                    DataExists = data != null,
                    SchemaExists = schema != null,
                    DataSha256 = Hash(data),
                    SchemaSha256 = Hash(schema)
                };
            }

            private static string Hash(byte[] bytes)
            {
                if (bytes == null)
                {
                    return null;
                }
                using (var sha = SHA256.Create())
                {
                    return string.Concat(sha.ComputeHash(bytes)
                        .Select(value => value.ToString("x2")));
                }
            }
        }

        private sealed class NoOpPostSaveActions : IGameDBPostSaveActions
        {
            public void Import(string assetPath) { }
            public void Notify(string scopeName) { }
        }
    }
}
