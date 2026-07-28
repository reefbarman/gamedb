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
    public class GameDBDocumentHistoryTests
    {
        private const string AssetPath = "Assets/GameDBDocumentHistoryTests/database.json";

        [Test]
        public void History_RecordsTransactionsAndMovesUndoRedoWithBaselineDirtyState()
        {
            var store = new MemoryPairStore();
            var document = CreateSavedItemsDocument(store);
            document.EnableHistory();
            var baseline = document.CurrentRevision;
            var origins = new List<GameDBDocumentChangeOrigin>();
            document.Changed += change => origins.Add(change.Origin);

            var edited = SetPower(document, 20);
            var undo = document.Undo();

            Assert.That(edited.Success, Is.True, edited.Message);
            Assert.That(undo.Success, Is.True, undo.Message);
            Assert.That(Power(document), Is.EqualTo(12L));
            Assert.That(document.IsDirty, Is.False);

            var redo = document.Redo();
            Assert.That(redo.Success, Is.True, redo.Message);
            Assert.That(Power(document), Is.EqualTo(20L));
            Assert.That(document.IsDirty, Is.True);
            Assert.That(document.BaselineRevision, Is.EqualTo(baseline));
            Assert.That(origins, Is.EqualTo(new[]
            {
                GameDBDocumentChangeOrigin.Transaction,
                GameDBDocumentChangeOrigin.Undo,
                GameDBDocumentChangeOrigin.Redo
            }));
            Assert.That(document.GetHistoryState().CanUndo, Is.True);
            Assert.That(document.GetHistoryState().CanRedo, Is.False);
            Assert.That(document.GetHistoryState().UndoLabel, Is.EqualTo("Set Value"));
        }

        [Test]
        public void History_NewTransactionAfterUndoTruncatesRedoAndFailuresAddNothing()
        {
            var document = CreateSavedItemsDocument(new MemoryPairStore());
            document.EnableHistory();
            Assert.That(SetPower(document, 20).Success, Is.True);
            Assert.That(SetPower(document, 30).Success, Is.True);
            Assert.That(document.Undo().Success, Is.True);
            Assert.That(Power(document), Is.EqualTo(20L));

            var invalid = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", "invalid")
            });
            var empty = document.ApplyTransaction(Array.Empty<GameDBCommand>());
            Assert.That(invalid.Success, Is.False);
            Assert.That(empty.Success, Is.True);
            Assert.That(document.GetHistoryState().CanRedo, Is.True);

            Assert.That(SetPower(document, 25).Success, Is.True);
            Assert.That(document.GetHistoryState().CanRedo, Is.False);
            Assert.That(document.Redo().Success, Is.False);
            Assert.That(Power(document), Is.EqualTo(25L));
        }

        [Test]
        public void History_MultiCommandTransactionIsOneUndoEntryAndCapDropsOldestStates()
        {
            var document = CreateSavedItemsDocument(new MemoryPairStore());
            document.EnableHistory(3);
            var initial = document.CurrentRevision;
            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20),
                new AddRowCommand("Items", "Shield", new Dictionary<string, object>
                {
                    { "Power", 5 }
                })
            });
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(document.Undo().Success, Is.True);
            Assert.That(document.CurrentRevision, Is.EqualTo(initial));
            Assert.That(document.CreateSnapshot().Tables.Single().Rows, Has.Count.EqualTo(1));

            Assert.That(document.Redo().Success, Is.True);
            Assert.That(SetPower(document, 30).Success, Is.True);
            Assert.That(SetPower(document, 40).Success, Is.True);
            Assert.That(document.Undo().Success, Is.True);
            Assert.That(document.Undo().Success, Is.True);
            Assert.That(document.Undo().Success, Is.False,
                "The oldest state should have been removed by the cap.");
        }

        [Test]
        public void History_SaveKeepsStackAndUndoUsesCurrentSavedBaseline()
        {
            var store = new MemoryPairStore();
            var document = CreateSavedItemsDocument(store);
            document.EnableHistory();
            Assert.That(SetPower(document, 20).Success, Is.True);
            var saved = document.Save();
            var savedRevision = document.BaselineRevision;

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.GetHistoryState().CanUndo, Is.True);
            Assert.That(document.Undo().Success, Is.True);
            Assert.That(Power(document), Is.EqualTo(12L));
            Assert.That(document.IsDirty, Is.True);
            Assert.That(document.BaselineRevision, Is.EqualTo(savedRevision));
            Assert.That(document.Redo().Success, Is.True);
            Assert.That(document.IsDirty, Is.False);
        }

        [Test]
        public void AssetSession_RestoredDraftStartsWithFreshHistoryAfterDomainReload()
        {
            var store = new MemoryPairStore();
            var document = CreateSavedItemsDocument(store);
            var state = document.CaptureState();
            var registry = new GameDBDocumentLeaseRegistry(store);
            var first = GameDBAssetSession.TryRestore(registry, state, "first").Session;
            Assert.That(first.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20)
            }).Success, Is.True);
            Assert.That(first.GetHistoryState().CanUndo, Is.True);
            var recovered = first.CaptureState();
            first.Dispose();

            var restored = GameDBAssetSession.TryRestore(registry, recovered, "restored").Session;

            Assert.That(Power(restored.CreateSnapshot()), Is.EqualTo(20L));
            Assert.That(restored.GetState().IsDirty, Is.True);
            Assert.That(restored.GetHistoryState().CanUndo, Is.False);
            Assert.That(restored.GetHistoryState().CanRedo, Is.False);
            restored.Dispose();
        }

        [Test]
        public void RefreshFromDisk_AutoRefreshesCleanAndConflictsDirtyUntilExplicitDiscard()
        {
            var store = new MemoryPairStore();
            var document = CreateSavedItemsDocument(store);
            document.EnableHistory();
            var external = CreateExternalState(30);
            store.SetPair(external);

            var clean = document.RefreshFromDisk(document.CurrentRevision, false);

            Assert.That(clean.Success, Is.True, clean.Message);
            Assert.That(clean.Status, Is.EqualTo(GameDBDiskRefreshStatus.Refreshed));
            Assert.That(Power(document), Is.EqualTo(30L));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.GetHistoryState().CanUndo, Is.False);
            Assert.That(document.ProbeDiskState().State, Is.EqualTo(GameDBDiskState.Unchanged));

            Assert.That(SetPower(document, 40).Success, Is.True);
            store.SetPair(CreateExternalState(50));
            var conflict = document.RefreshFromDisk(document.CurrentRevision, false);

            Assert.That(conflict.Success, Is.False);
            Assert.That(conflict.Status, Is.EqualTo(GameDBDiskRefreshStatus.Conflict));
            Assert.That(Power(document), Is.EqualTo(40L));
            Assert.That(document.IsDirty, Is.True);

            var discard = document.RefreshFromDisk(document.CurrentRevision, true);
            Assert.That(discard.Success, Is.True, discard.Message);
            Assert.That(Power(document), Is.EqualTo(50L));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.GetHistoryState().CanUndo, Is.False);
        }

        [Test]
        public void RefreshFromDisk_FormattingOnlyChangeRebasesTokenAndNotifies()
        {
            var store = new MemoryPairStore();
            var document = CreateSavedItemsDocument(store);
            document.EnableHistory();
            Assert.That(SetPower(document, 20).Success, Is.True);
            Assert.That(document.Save().Success, Is.True);
            Assert.That(document.GetHistoryState().CanUndo, Is.True);
            var before = document.CurrentRevision;
            var pair = store.Read(AssetPath);
            store.SetPair(GameDBFilePairStore.Decode(pair.DataBytes) + "\n",
                GameDBFilePairStore.Decode(pair.SchemaBytes));
            var changes = new List<GameDBDocumentChange>();
            document.Changed += changes.Add;

            var result = document.RefreshFromDisk(before, false);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo(GameDBDiskRefreshStatus.Refreshed));
            Assert.That(document.CurrentRevision, Is.EqualTo(before));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.GetHistoryState().CanUndo, Is.True,
                "Formatting-only disk rebases must preserve canonical content history.");
            Assert.That(document.ProbeDiskState().State, Is.EqualTo(GameDBDiskState.Unchanged));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Origin, Is.EqualTo(GameDBDocumentChangeOrigin.Recovery));
        }

        private static GameDBDocument CreateSavedItemsDocument(MemoryPairStore store)
        {
            var document = GameDBDocument.CreateNew(AssetPath, "HistoryTests", false,
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
            model.ScopeName = "HistoryTests";
            Assert.That(model.AddTable("Items", KeyType.@string), Is.True);
            var table = (TableModel)model.Tables["Items"];
            Assert.That(table.AddField("Power", FieldType.@int, false), Is.True);
            Assert.That(table.AddKey("Sword"), Is.True);
            Assert.That(table.SetValue("Sword", "Power", power), Is.True);
            return GameDBModelCodec.Serialize(model);
        }

        private static GameDBTransactionResult SetPower(GameDBDocument document, int value)
        {
            return document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", value)
            });
        }

        private static long Power(GameDBDocument document)
        {
            return Power(document.CreateSnapshot());
        }

        private static long Power(GameDBEditorLibrary.Automation.GameDBSnapshot snapshot)
        {
            return (long)snapshot.Tables.Single().Rows.Single(row => row.Key == "Sword")
                .Values["Power"];
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
                SetPair(state.DataJson, state.SchemaJson);
            }

            internal void SetPair(string dataJson, string schemaJson)
            {
                m_data = GameDBFilePairStore.Encode(dataJson);
                m_schema = GameDBFilePairStore.Encode(schemaJson);
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
