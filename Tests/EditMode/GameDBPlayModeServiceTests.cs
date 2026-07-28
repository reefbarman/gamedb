using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBPlayModeServiceTests
    {
        [Test]
        public void LoadRuntimeData_PreservesSchemaAndBaselineAndStartsFreshHistory()
        {
            using (var session = CreateSession())
            {
                Assert.That(session.ApplyTransaction(new GameDBCommand[]
                {
                    new SetValueCommand("Items", "Sword", "Power", 2L)
                }).Success, Is.True);
                Assert.That(session.GetHistoryState().CanUndo, Is.True);
                var baseline = session.GetState().BaselineRevision;
                var registry = new GameDBRuntimeRegistry();
                var runtime = new TestRuntimeDB();
                Assert.That(runtime.Import(Data(17), false), Is.Null);
                var target = registry.Register(runtime).Target;
                var service = new GameDBPlayModeService(registry);

                var result = service.LoadRuntimeData(session, target.TargetId,
                    target.Epoch, session.GetState().CurrentRevision);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Binding.TargetId, Is.EqualTo(target.TargetId));
                Assert.That(result.Binding.Epoch, Is.EqualTo(target.Epoch));
                Assert.That(result.Snapshot.ScopeName, Is.EqualTo("PlayModeTests"));
                var table = result.Snapshot.Tables.Single();
                Assert.That(table.Fields.Single().Name, Is.EqualTo("Power"));
                Assert.That(table.Rows.Single().Values["Power"], Is.EqualTo(17));
                Assert.That(session.GetState().BaselineRevision, Is.EqualTo(baseline));
                Assert.That(session.GetState().IsDirty, Is.True);
                Assert.That(session.GetHistoryState().CanUndo, Is.False,
                    "Runtime import must establish a fresh Play Mode history boundary.");
            }
        }

        [Test]
        public void LoadRuntimeData_IdenticalContentStillResetsHistory()
        {
            using (var session = CreateSession())
            {
                Assert.That(session.ApplyTransaction(new GameDBCommand[]
                {
                    new SetValueCommand("Items", "Sword", "Power", 2L)
                }).Success, Is.True);
                Assert.That(session.GetHistoryState().CanUndo, Is.True);
                var registry = new GameDBRuntimeRegistry();
                var runtime = new TestRuntimeDB();
                Assert.That(runtime.Import(Data(2), false), Is.Null);
                var target = registry.Register(runtime).Target;

                var result = new GameDBPlayModeService(registry).LoadRuntimeData(session,
                    target.TargetId, target.Epoch, session.GetState().CurrentRevision);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Snapshot.Revision,
                    Is.EqualTo(session.CreateSnapshot().Revision));
                Assert.That(session.GetHistoryState().CanUndo, Is.False);
                Assert.That(session.GetHistoryState().CanRedo, Is.False);
            }
        }

        [Test]
        public void LoadRuntimeData_FailurePreservesDocument()
        {
            using (var session = CreateSession())
            {
                var registry = new GameDBRuntimeRegistry();
                var unpublished = registry.Register(new TestRuntimeDB()).Target;
                var service = new GameDBPlayModeService(registry);
                var before = session.CreateSnapshot();

                var result = service.LoadRuntimeData(session, unpublished.TargetId,
                    unpublished.Epoch, before.Revision);

                Assert.That(result.Status,
                    Is.EqualTo(GameDBPlayModeOperationStatus.RuntimeImportFailed));
                Assert.That(result.Message, Does.Contain("no published data"));
                Assert.That(session.CreateSnapshot().Revision, Is.EqualTo(before.Revision));
            }
        }

        [Test]
        public void LoadAndReload_RejectStaleEpochAndPushCurrentData()
        {
            using (var session = CreateSession())
            {
                var registry = new GameDBRuntimeRegistry();
                var runtime = new TestRuntimeDB();
                Assert.That(runtime.Import(Data(5), false), Is.Null);
                var target = registry.Register(runtime).Target;
                var service = new GameDBPlayModeService(registry);
                var loaded = service.LoadRuntimeData(session, target.TargetId,
                    target.Epoch, session.GetState().CurrentRevision);
                Assert.That(loaded.Success, Is.True, loaded.Message);
                var edited = session.ApplyTransaction(new GameDBCommand[]
                {
                    new SetValueCommand("Items", "Sword", "Power", 42L)
                }, new GameDBTransactionOptions
                {
                    ExpectedRevision = loaded.Snapshot.Revision,
                    AllowedOperations = new[] { GameDBCommandKind.SetValue }
                });
                Assert.That(edited.Success, Is.True, edited.Message);

                var reloaded = service.ReloadInGame(session, loaded.Binding,
                    session.GetState().CurrentRevision);

                Assert.That(reloaded.Success, Is.True, reloaded.Message);
                Assert.That(runtime.Power, Is.EqualTo(42));
                registry.BeginPlaySession();
                var stale = service.ReloadInGame(session, loaded.Binding,
                    session.GetState().CurrentRevision);
                Assert.That(stale.Status,
                    Is.EqualTo(GameDBPlayModeOperationStatus.StalePlaySession));
                Assert.That(service.IsCurrent(loaded.Binding), Is.False);
            }
        }

        [Test]
        public void LoadRuntimeData_RejectsMissingRuntimeTableWithoutMutation()
        {
            using (var session = CreateSession())
            {
                var registry = new GameDBRuntimeRegistry();
                var runtime = new MissingTableRuntimeDB();
                Assert.That(runtime.Import("{\"tables\":{}}", false), Is.Null);
                var target = registry.Register(runtime).Target;
                var service = new GameDBPlayModeService(registry);
                var before = session.CreateSnapshot();

                var result = service.LoadRuntimeData(session, target.TargetId,
                    target.Epoch, before.Revision);

                Assert.That(result.Status,
                    Is.EqualTo(GameDBPlayModeOperationStatus.RuntimeImportFailed));
                Assert.That(result.Message, Does.Contain("missing table: Items"));
                Assert.That(session.CreateSnapshot().Revision, Is.EqualTo(before.Revision));
            }
        }

        private static GameDBAssetSession CreateSession()
        {
            var path = $"Assets/GameDBPlayModeServiceTests/{Guid.NewGuid():N}.json";
            var document = GameDBDocument.CreateNew(path, "PlayModeTests", false);
            var created = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Power",
                    new GameDBFieldTypeSpec(FieldType.@int, false, null)),
                new AddRowCommand("Items", "Sword",
                    new Dictionary<string, object> { { "Power", 1L } })
            });
            Assert.That(created.Success, Is.True, created.Message);
            var opened = GameDBAssetSession.TryRestore(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                document.CaptureState());
            Assert.That(opened.Status, Is.EqualTo(GameDBAssetSessionOpenStatus.Opened));
            return opened.Session;
        }

        private static string Data(int power)
        {
            return "{\"tables\":{\"Items\":{\"Sword\":{\"Power\":"
                + power + "}}}}";
        }

        private sealed class TestRuntimeDB : GameDBBase
        {
            internal int Power => Convert.ToInt32(m_internal.Tables["Items"]
                .GetByKeyRaw("Sword").GetValue("Power"));

            internal TestRuntimeDB() : base("Runtime Items", "PlayModeTests")
            {
                var table = new TableBase("Items", KeyType.@string, null,
                    key => new RowBase(key));
                table.Fields.Add("Power", new FieldBase("Power", FieldType.@int, false));
                RegisterTable("Items", table);
            }
        }

        private sealed class MissingTableRuntimeDB : GameDBBase
        {
            internal MissingTableRuntimeDB() : base("Missing Items", "PlayModeTests")
            {
            }
        }
    }
}
