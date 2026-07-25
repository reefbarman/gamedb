using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBDocumentTests
    {
        private const string AssetPath = "Assets/GameDBDocumentTests/database.json";

        [Test]
        public void ApplyTransaction_CommitsOrderedCommandsAndRaisesOneAggregateChange()
        {
            var document = GameDBDocument.CreateNew(AssetPath, "DocumentTests", false);
            var changes = new List<GameDBDocumentChange>();
            document.Changed += changes.Add;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Power", FieldSpec(FieldType.@int)),
                new AddRowCommand("Items", "Sword",
                    new Dictionary<string, object> { { "Power", 12L } })
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.None));
            Assert.That(result.Changes, Is.EqualTo(new[]
            {
                GameDBCommandKind.AddTable,
                GameDBCommandKind.AddField,
                GameDBCommandKind.AddRow
            }));
            Assert.That(result.AttemptedRevision, Is.EqualTo(document.CurrentRevision));
            Assert.That(document.IsDirty, Is.True);
            Assert.That(document.CreateSnapshot().Tables.Single().Rows.Single().Values["Power"], Is.EqualTo(12L));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(changes[0].RevisionBefore, Is.EqualTo(result.RevisionBefore));
            Assert.That(changes[0].RevisionAfter, Is.EqualTo(result.AttemptedRevision));
            Assert.That(changes[0].Commands, Is.EqualTo(result.Changes));
        }

        [Test]
        public void ApplyTransaction_FailedCommandDiscardsStageAndReportsIndex()
        {
            var document = CreateEmptyDocument();
            var revisionBefore = document.CurrentRevision;
            var notifications = 0;
            document.Changed += change => notifications++;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddTableCommand("Items", KeyType.@string, null)
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.CommandFailed));
            Assert.That(result.FailedCommandIndex, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("already exists"));
            Assert.That(result.AttemptedSnapshot.Tables.Select(table => table.Name), Does.Contain("Items"));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().Tables, Is.Empty);
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ApplyTransaction_ThrownBuiltInCommandDiscardsStage()
        {
            var document = CreateEmptyDocument();
            var revisionBefore = document.CurrentRevision;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand(null, KeyType.@string, null)
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.CommandThrew));
            Assert.That(result.FailedCommandIndex, Is.Zero);
            Assert.That(result.Message, Does.Contain("non-empty"));
            Assert.That(result.AttemptedSnapshot.Revision, Is.EqualTo(revisionBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().Tables, Is.Empty);
        }

        [Test]
        public void ApplyTransaction_RejectsUnknownCommandImplementations()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;
            var command = new MisclassifiedCommand();

            var result = document.ApplyTransaction(new GameDBCommand[] { command });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.InvalidRequest));
            Assert.That(result.Message, Does.Contain("Unsupported command type"));
            Assert.That(command.Executed, Is.False);
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void ApplyTransaction_ValidationFailureRetainsAttemptedSnapshotAndDiscardsStage()
        {
            var document = CreateReferenceDocument();
            var revisionBefore = document.CurrentRevision;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Recipes", "Forge", "Result", "Missing")
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.ValidationFailed));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.AttemptedRevision, Is.EqualTo(result.AttemptedSnapshot.Revision));
            Assert.That(result.AttemptedRevision, Is.Not.EqualTo(revisionBefore));
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("tableRef.row.missing"));
            Assert.That(RowValue(result.AttemptedSnapshot, "Recipes", "Forge", "Result"), Is.EqualTo("Missing"));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(RowValue(document.CreateSnapshot(), "Recipes", "Forge", "Result"), Is.EqualTo("Sword"));
            Assert.That(document.IsDirty, Is.False);
        }

        [Test]
        public void ApplyTransaction_DeniedDestructiveReportsIndexBeforeAnyCommandExecutes()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Transient", KeyType.@string, null),
                new DeleteTableCommand("Items")
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.AuthorizationDenied));
            Assert.That(result.DeniedCommandIndex, Is.EqualTo(1));
            Assert.That(result.DeniedCommandKind, Is.EqualTo(GameDBCommandKind.DeleteTable));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "Items" }));
        }

        [Test]
        public void ApplyTransaction_ExpectedRevisionConflictDoesNotExecute()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Transient", KeyType.@string, null)
            }, new GameDBTransactionOptions { ExpectedRevision = "stale" });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.RevisionConflict));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "Items" }));
        }

        [Test]
        public void DirtyState_NoOpDoesNotNotifyAndReturningToBaselineClearsDirty()
        {
            var document = CreateItemsDocument();
            var baseline = document.BaselineRevision;
            var notifications = 0;
            document.Changed += change => notifications++;

            var noOp = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 12L)
            });
            var changed = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 15L)
            });
            var restored = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 12L)
            });

            Assert.That(noOp.Success, Is.True, noOp.Message);
            Assert.That(document.BaselineRevision, Is.EqualTo(baseline));
            Assert.That(noOp.AttemptedRevision, Is.EqualTo(baseline));
            Assert.That(changed.Success, Is.True, changed.Message);
            Assert.That(restored.Success, Is.True, restored.Message);
            Assert.That(restored.AttemptedRevision, Is.EqualTo(baseline));
            Assert.That(document.CurrentRevision, Is.EqualTo(baseline));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void Changed_ReentrantTransactionPreservesFifoOrderForAllSubscribers()
        {
            var document = CreateEmptyDocument();
            var observed = new List<string>();
            var reentered = false;
            document.Changed += change =>
            {
                observed.Add("first:" + change.Commands.Single());
                if (!reentered)
                {
                    reentered = true;
                    var nested = document.ApplyTransaction(new GameDBCommand[]
                    {
                        new AddTableCommand("Second", KeyType.@string, null)
                    });
                    Assert.That(nested.Success, Is.True, nested.Message);
                }
            };
            document.Changed += change => observed.Add("second:" + change.Commands.Single());

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("First", KeyType.@string, null)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(observed, Is.EqualTo(new[]
            {
                "first:AddTable",
                "second:AddTable",
                "first:AddTable",
                "second:AddTable"
            }));
            Assert.That(document.CreateSnapshot().Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "First", "Second" }));
        }

        [Test]
        public void Changed_SubscriberFailureDoesNotBlockCommitOrOtherSubscribers()
        {
            var document = GameDBDocument.CreateNew(AssetPath, "DocumentTests", false);
            var observed = 0;
            document.Changed += change => throw new InvalidOperationException("subscriber exploded");
            document.Changed += change => observed++;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.NotificationErrors, Is.EqualTo(new[] { "subscriber exploded" }));
            Assert.That(observed, Is.EqualTo(1));
            Assert.That(document.CreateSnapshot().Tables.Select(table => table.Name), Does.Contain("Items"));
        }

        [Test]
        public void Commands_RenameReferencesReplaceAndDeleteInOrder()
        {
            var document = CreateReferenceDocument();
            var commands = new GameDBCommand[]
            {
                new RenameRowCommand("Items", "Sword", "Blade"),
                new RenameTableCommand("Items", "Gear"),
                new RenameFieldCommand("Recipes", "Result", "Product"),
                new ReplaceFieldCommand("Recipes", "Product", FieldSpec(FieldType.@string)),
                new DeleteRowCommand("Gear", "Blade"),
                new DeleteFieldCommand("Recipes", "Product"),
                new DeleteTableCommand("Gear"),
                new DeleteTableCommand("Recipes")
            };

            var result = document.ApplyTransaction(commands, Allow(
                GameDBCommandKind.RenameRow,
                GameDBCommandKind.RenameTable,
                GameDBCommandKind.RenameField,
                GameDBCommandKind.ReplaceField,
                GameDBCommandKind.DeleteRow,
                GameDBCommandKind.DeleteField,
                GameDBCommandKind.DeleteTable));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Changes, Is.EqualTo(commands.Select(command => command.Kind)));
            Assert.That(result.AttemptedSnapshot.Tables, Is.Empty);
            Assert.That(document.CreateSnapshot().Tables, Is.Empty);
            Assert.That(document.IsDirty, Is.True);
        }

        [Test]
        public void RowCommands_RejectReservedNullReferenceKey()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;

            var added = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddRowCommand("Items", FieldBase.NullRefToken, null)
            });
            var renamed = document.ApplyTransaction(new GameDBCommand[]
            {
                new RenameRowCommand("Items", "Sword", FieldBase.NullRefToken)
            }, Allow(GameDBCommandKind.RenameRow));

            Assert.That(added.Success, Is.False);
            Assert.That(added.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.CommandThrew));
            Assert.That(added.Message, Does.Contain("reserved"));
            Assert.That(renamed.Success, Is.False);
            Assert.That(renamed.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.CommandThrew));
            Assert.That(renamed.Message, Does.Contain("reserved"));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().Tables.Single().Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Sword" }));
        }

        [Test]
        public void ReservedImportedRow_CanBeDeletedWithoutRetargetingNullReferences()
        {
            var gameDB = CreateModel();
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Recipes", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            var recipes = (TableModel)gameDB.Tables["Recipes"];
            Assert.That(items.AddKey("ReservedRow"), Is.True);
            Assert.That(recipes.AddField("Result", FieldType.tableRef, false, "Items"), Is.True);
            Assert.That(recipes.AddField("Ingredients", FieldType.tableRef, true, "Items"), Is.True);
            Assert.That(recipes.AddField("Slots", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.tableRef, "Items")), Is.True);
            Assert.That(recipes.AddKey("Forge"), Is.True);
            Assert.That(recipes.SetValue("Forge", "Result", FieldBase.NullRefToken), Is.True);
            Assert.That(recipes.SetValue("Forge", "Ingredients",
                new List<object> { FieldBase.NullRefToken }), Is.True);
            Assert.That(recipes.SetValue("Forge", "Slots",
                new Dictionary<string, object> { { "Primary", FieldBase.NullRefToken } }), Is.True);
            var reservedRow = items.Data["ReservedRow"];
            items.Data.Remove("ReservedRow");
            items.Data.Add(FieldBase.NullRefToken, reservedRow);
            var serialized = GameDBModelCodec.Serialize(gameDB);
            var malformed = GameDBModelCodec.Import(
                serialized.DataJson, serialized.SchemaJson, gameDB.LoadedPath);
            var document = CreateDocument(malformed);

            Assert.That(document.Validate().Select(issue => issue.Code), Does.Contain("row.key.reserved"));
            var deleted = document.ApplyTransaction(new GameDBCommand[]
            {
                new DeleteRowCommand("Items", FieldBase.NullRefToken)
            }, Allow(GameDBCommandKind.DeleteRow));

            Assert.That(deleted.Success, Is.True, deleted.Message);
            Assert.That(deleted.Issues, Is.Empty);
            var snapshot = document.CreateSnapshot();
            Assert.That(snapshot.Tables.Single(table => table.Name == "Items").Rows, Is.Empty);
            var recipe = snapshot.Tables.Single(table => table.Name == "Recipes").Rows.Single();
            Assert.That(recipe.Values["Result"], Is.Null);
            Assert.That(((IList<object>)recipe.Values["Ingredients"]).Single(), Is.Null);
            Assert.That(((Dictionary<object, object>)recipe.Values["Slots"])["Primary"], Is.Null);
        }

        [Test]
        public void CommandConstructor_CopiesSupportedWireValues()
        {
            var document = CreateArrayDocument();
            var values = new List<object> { "melee" };
            var command = new SetValueCommand("Items", "Sword", "Tags", values);
            values[0] = "mutated";
            values.Add("external");

            var result = document.ApplyTransaction(new GameDBCommand[] { command });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That((IEnumerable<object>)RowValue(document.CreateSnapshot(),
                "Items", "Sword", "Tags"), Is.EqualTo(new object[] { "melee" }));
        }

        [Test]
        public void ApplyTransaction_NullAndEmptyCommandListsHaveDefinedResults()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;
            var notifications = 0;
            document.Changed += change => notifications++;

            var invalid = document.ApplyTransaction(null);
            var empty = document.ApplyTransaction(Array.Empty<GameDBCommand>());

            Assert.That(invalid.Success, Is.False);
            Assert.That(invalid.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.InvalidRequest));
            Assert.That(empty.Success, Is.True, empty.Message);
            Assert.That(empty.AttemptedRevision, Is.EqualTo(revisionBefore));
            Assert.That(empty.AttemptedSnapshot.Revision, Is.EqualTo(revisionBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.Zero);
        }

        private static GameDBDocument CreateEmptyDocument()
        {
            return CreateDocument(CreateModel());
        }

        private static GameDBDocument CreateItemsDocument()
        {
            var gameDB = CreateModel();
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Power", FieldType.@int, false), Is.True);
            Assert.That(items.AddKey("Sword"), Is.True);
            Assert.That(items.SetValue("Sword", "Power", 12L), Is.True);
            return CreateDocument(gameDB);
        }

        private static GameDBDocument CreateArrayDocument()
        {
            var gameDB = CreateModel();
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Tags", FieldType.@string, true), Is.True);
            Assert.That(items.AddKey("Sword"), Is.True);
            return CreateDocument(gameDB);
        }

        private static GameDBDocument CreateReferenceDocument()
        {
            var gameDB = CreateModel();
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Recipes", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            var recipes = (TableModel)gameDB.Tables["Recipes"];
            Assert.That(items.AddKey("Sword"), Is.True);
            Assert.That(recipes.AddField("Result", FieldType.tableRef, false, "Items"), Is.True);
            Assert.That(recipes.AddKey("Forge"), Is.True);
            Assert.That(recipes.SetValue("Forge", "Result", "Sword"), Is.True);
            return CreateDocument(gameDB);
        }

        private static GameDB CreateModel()
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory("GameDBDocumentTests/database.json");
            gameDB.ScopeName = "DocumentTests";
            return gameDB;
        }

        private static GameDBDocument CreateDocument(GameDB gameDB)
        {
            var serialized = GameDBModelCodec.Serialize(gameDB);
            return GameDBDocument.RestoreState(new GameDBDocumentState
            {
                DocumentId = Guid.NewGuid().ToString("N"),
                AssetPath = AssetPath,
                DataJson = serialized.DataJson,
                SchemaJson = serialized.SchemaJson,
                BaselineRevision = serialized.Revision,
                BaselineDiskToken = GameDBDiskToken.Absent,
                WasDirty = false
            });
        }

        private static GameDBFieldTypeSpec FieldSpec(FieldType fieldType, bool isArray = false,
            string typeArgument = null)
        {
            return new GameDBFieldTypeSpec(fieldType, isArray, typeArgument);
        }

        private static GameDBTransactionOptions Allow(params GameDBCommandKind[] kinds)
        {
            return new GameDBTransactionOptions { AllowedDestructiveOperations = kinds };
        }

        private static object RowValue(GameDBSnapshot snapshot, string tableName,
            string rowKey, string fieldName)
        {
            return snapshot.Tables.Single(table => table.Name == tableName)
                .Rows.Single(row => row.Key == rowKey).Values[fieldName];
        }

        private sealed class MisclassifiedCommand : GameDBCommand
        {
            internal bool Executed { get; private set; }
            internal override GameDBCommandKind Kind => GameDBCommandKind.DeleteTable;
            internal override bool IsDestructive => false;

            internal override GameDBCommandExecution Execute(GameDBCommandContext context)
            {
                Executed = true;
                context.Model.RemoveTable("Items");
                return GameDBCommandExecution.Succeeded();
            }
        }
    }
}
