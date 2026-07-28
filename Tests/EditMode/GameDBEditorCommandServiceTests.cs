using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorCommandServiceTests
    {
        [Test]
        public void Execute_UsesExpectedRevisionAndReturnsCanonicalSnapshotOnFailure()
        {
            using (var session = CreateSession())
            {
                var service = new GameDBEditorCommandService();
                var initial = session.CreateSnapshot();
                var added = service.Execute(session,
                    new AddTableCommand("Items", KeyType.@string, null),
                    initial.Revision);

                var stale = service.Execute(session,
                    new AddTableCommand("Recipes", KeyType.@string, null),
                    initial.Revision);

                Assert.That(added.Success, Is.True, added.Message);
                Assert.That(added.CommandKind, Is.EqualTo(GameDBCommandKind.AddTable));
                Assert.That(added.RevisionBefore, Is.EqualTo(initial.Revision));
                Assert.That(added.RevisionAfter, Is.EqualTo(added.Snapshot.Revision));
                Assert.That(added.Snapshot.Tables.Select(table => table.Name),
                    Is.EqualTo(new[] { "Items" }));
                Assert.That(stale.Success, Is.False);
                Assert.That(stale.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.RevisionConflict));
                Assert.That(stale.Snapshot.Revision, Is.EqualTo(added.Snapshot.Revision));
                Assert.That(stale.Snapshot.Tables.Select(table => table.Name),
                    Is.EqualTo(new[] { "Items" }));
            }
        }

        [Test]
        public void Execute_RequiresExactDestructiveConfirmation()
        {
            using (var session = CreateSession())
            {
                var service = new GameDBEditorCommandService();
                var added = service.Execute(session,
                    new AddTableCommand("Items", KeyType.@string, null),
                    session.CreateSnapshot().Revision);

                var denied = service.Execute(session,
                    new DeleteTableCommand("Items"), added.RevisionAfter);
                var confirmed = service.Execute(session,
                    new DeleteTableCommand("Items"), added.RevisionAfter,
                    destructiveConfirmed: true);

                Assert.That(denied.Success, Is.False);
                Assert.That(denied.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.AuthorizationDenied));
                Assert.That(denied.Transaction.DeniedCommandKind,
                    Is.EqualTo(GameDBCommandKind.DeleteTable));
                Assert.That(denied.Snapshot.Tables.Select(table => table.Name),
                    Is.EqualTo(new[] { "Items" }));
                Assert.That(confirmed.Success, Is.True, confirmed.Message);
                Assert.That(confirmed.Snapshot.Tables, Is.Empty);
            }
        }

        [Test]
        public void Execute_RoundTripsMetadataTableFieldRowAndScalarCommandFamilies()
        {
            using (var session = CreateSession())
            {
                var service = new GameDBEditorCommandService();
                var revision = session.CreateSnapshot().Revision;

                revision = Success(service, session,
                    new SetDatabaseMetadataCommand("EditedScope", true), revision)
                    .RevisionAfter;
                revision = Success(service, session,
                    new AddTableCommand("Items", KeyType.@string, null), revision)
                    .RevisionAfter;
                revision = Success(service, session,
                    new AddFieldCommand("Items", "Name",
                        new GameDBFieldTypeSpec(FieldType.@string, false, null)), revision)
                    .RevisionAfter;
                revision = Success(service, session,
                    new AddRowCommand("Items", "Sword",
                        new Dictionary<string, object> { { "Name", "Iron" } }), revision)
                    .RevisionAfter;
                revision = Success(service, session,
                    new SetValueCommand("Items", "Sword", "Name", "Steel"), revision)
                    .RevisionAfter;
                revision = Success(service, session,
                    new RenameRowCommand("Items", "Sword", "Blade"), revision, true)
                    .RevisionAfter;
                revision = Success(service, session,
                    new RenameFieldCommand("Items", "Name", "Label"), revision, true)
                    .RevisionAfter;
                revision = Success(service, session,
                    new ReplaceFieldCommand("Items", "Label",
                        new GameDBFieldTypeSpec(FieldType.@int, false, null)), revision, true)
                    .RevisionAfter;
                revision = Success(service, session,
                    new RenameTableCommand("Items", "Gear"), revision, true)
                    .RevisionAfter;

                var snapshot = session.CreateSnapshot();
                Assert.That(snapshot.ScopeName, Is.EqualTo("EditedScope"));
                Assert.That(snapshot.LocalizationDatabase, Is.True);
                Assert.That(snapshot.Tables.Single().Name, Is.EqualTo("Gear"));
                Assert.That(snapshot.Tables.Single().Fields.Single().Name,
                    Is.EqualTo("Label"));
                Assert.That(snapshot.Tables.Single().Fields.Single().FieldType,
                    Is.EqualTo(FieldType.@int));
                Assert.That(snapshot.Tables.Single().Rows.Single().Key,
                    Is.EqualTo("Blade"));
                Assert.That(snapshot.Tables.Single().Rows.Single().Values["Label"],
                    Is.EqualTo(0));

                revision = Success(service, session,
                    new DeleteRowCommand("Gear", "Blade"), revision, true)
                    .RevisionAfter;
                revision = Success(service, session,
                    new DeleteFieldCommand("Gear", "Label"), revision, true)
                    .RevisionAfter;
                var deleted = Success(service, session,
                    new DeleteTableCommand("Gear"), revision, true);
                Assert.That(deleted.Snapshot.Tables, Is.Empty);
            }
        }

        [Test]
        public void Execute_DataOnlyAllowlistRejectsSchemaAndAllowsRowCommands()
        {
            using (var session = CreateSession())
            {
                var service = new GameDBEditorCommandService();
                var initial = session.CreateSnapshot();
                var denied = service.Execute(session,
                    new AddTableCommand("Items", KeyType.@string, null),
                    initial.Revision, allowedOperations:
                    GameDBEditorCommandService.DataOnlyOperations);

                Assert.That(denied.Success, Is.False);
                Assert.That(denied.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.AuthorizationDenied));
                Assert.That(denied.Transaction.DeniedCommandKind,
                    Is.EqualTo(GameDBCommandKind.AddTable));
                Assert.That(denied.Snapshot.Revision, Is.EqualTo(initial.Revision));

                var setup = service.Execute(session,
                    new AddTableCommand("Items", KeyType.@string, null),
                    initial.Revision);
                Assert.That(setup.Success, Is.True, setup.Message);
                var allowed = service.Execute(session,
                    new AddRowCommand("Items", "Sword", new Dictionary<string, object>()),
                    setup.RevisionAfter, allowedOperations:
                    GameDBEditorCommandService.DataOnlyOperations);
                Assert.That(allowed.Success, Is.True, allowed.Message);
                Assert.That(allowed.Snapshot.Tables.Single().Rows.Single().Key,
                    Is.EqualTo("Sword"));
            }
        }

        [Test]
        public void SessionPolicy_CannotBeWidenedByCaller()
        {
            using (var session = CreateSession())
            {
                session.SetAllowedOperations(GameDBEditorCommandService.DataOnlyOperations);
                var before = session.CreateSnapshot();

                var denied = session.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("Items", KeyType.@string, null)
                }, new GameDBTransactionOptions
                {
                    ExpectedRevision = before.Revision,
                    AllowedOperations = Enum.GetValues(typeof(GameDBCommandKind))
                        .Cast<GameDBCommandKind>().ToArray()
                });

                Assert.That(denied.Success, Is.False);
                Assert.That(denied.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.AuthorizationDenied));
                Assert.That(denied.DeniedCommandKind,
                    Is.EqualTo(GameDBCommandKind.AddTable));
                Assert.That(session.CreateSnapshot().Revision, Is.EqualTo(before.Revision));
            }
        }

        [Test]
        public void Execute_ValidationFailureKeepsCanonicalDocumentSnapshot()
        {
            using (var session = CreateSession())
            {
                var service = new GameDBEditorCommandService();
                var before = session.CreateSnapshot();

                var invalid = service.Execute(session,
                    new SetDatabaseMetadataCommand(string.Empty, false),
                    before.Revision);

                Assert.That(invalid.Success, Is.False);
                Assert.That(invalid.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.ValidationFailed));
                Assert.That(invalid.Transaction.AttemptedSnapshot.ScopeName, Is.Empty);
                Assert.That(invalid.Snapshot.ScopeName, Is.EqualTo("CommandServiceTests"));
                Assert.That(invalid.Snapshot.Revision, Is.EqualTo(before.Revision));
                Assert.That(session.CreateSnapshot().Revision, Is.EqualTo(before.Revision));
            }
        }

        private static GameDBEditorCommandResult Success(
            GameDBEditorCommandService service, GameDBAssetSession session,
            GameDBCommand command, string revision, bool destructive = false)
        {
            var result = service.Execute(session, command, revision, destructive);
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CommandKind, Is.EqualTo(command.Kind));
            Assert.That(result.RevisionBefore, Is.EqualTo(revision));
            Assert.That(result.Snapshot.Revision, Is.EqualTo(result.RevisionAfter));
            return result;
        }

        private static GameDBAssetSession CreateSession()
        {
            var assetPath = $"Assets/GameDBEditorCommandServiceTests/{Guid.NewGuid():N}.json";
            var document = GameDBDocument.CreateNew(assetPath,
                "CommandServiceTests", false);
            var registry = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);
            var opened = GameDBAssetSession.TryRestore(registry,
                document.CaptureState());
            Assert.That(opened.Status, Is.EqualTo(GameDBAssetSessionOpenStatus.Opened));
            return opened.Session;
        }
    }
}
