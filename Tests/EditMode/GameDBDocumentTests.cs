using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections;
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
        public void SessionState_InitialBindAndTransactionNotificationsTrackDirtyTransitions()
        {
            var document = CreateItemsDocument();
            var initial = document.GetSessionState();
            var changes = new List<GameDBDocumentStateChange>();
            document.StateChanged += changes.Add;

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

            Assert.That(initial.DocumentId, Is.EqualTo(document.DocumentId));
            Assert.That(initial.CurrentRevision, Is.EqualTo(initial.BaselineRevision));
            Assert.That(initial.IsDirty, Is.False);
            Assert.That(initial.HasPendingPostSaveWork, Is.False);
            Assert.That(initial.PersistenceStateUnknown, Is.False);
            Assert.That(noOp.Success, Is.True, noOp.Message);
            Assert.That(changed.Success, Is.True, changed.Message);
            Assert.That(restored.Success, Is.True, restored.Message);
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes.Select(change => change.Origin), Is.EqualTo(new[]
            {
                GameDBDocumentStateChangeOrigin.Transaction,
                GameDBDocumentStateChangeOrigin.Transaction
            }));
            Assert.That(changes[0].Previous, Is.EqualTo(initial));
            Assert.That(changes[0].Current.IsDirty, Is.True);
            Assert.That(changes[1].Previous, Is.EqualTo(changes[0].Current));
            Assert.That(changes[1].Current.IsDirty, Is.False);
            Assert.That(changes[1].Current, Is.EqualTo(document.GetSessionState()));
            Assert.That(changes.All(change => change.SaveStatus == null), Is.True);
        }

        [Test]
        public void StateChanged_CombinedNotificationsPreserveWholeItemFifoAndSubscriberFailures()
        {
            var document = CreateEmptyDocument();
            var observed = new List<string>();
            var nested = false;
            GameDBTransactionResult nestedResult = null;
            document.Changed += change => observed.Add("content:" + change.RevisionAfter);
            document.StateChanged += change =>
            {
                observed.Add("state:" + change.Current.CurrentRevision);
                if (!nested)
                {
                    nested = true;
                    nestedResult = document.ApplyTransaction(new GameDBCommand[]
                    {
                        new AddTableCommand("Second", KeyType.@string, null)
                    });
                    Assert.That(nestedResult.Success, Is.True);
                }
            };
            document.StateChanged += change =>
                throw new InvalidOperationException("state subscriber exploded");

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("First", KeyType.@string, null)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.NotificationErrors,
                Is.EqualTo(new[] { "state subscriber exploded" }));
            Assert.That(result.NotificationErrorsDeferred, Is.False);
            Assert.That(nestedResult, Is.Not.Null);
            Assert.That(nestedResult.NotificationErrorsDeferred, Is.True);
            Assert.That(nestedResult.NotificationErrors, Is.Empty);
            Assert.That(observed, Has.Count.EqualTo(4));
            Assert.That(observed[0], Does.StartWith("content:"));
            Assert.That(observed[1], Does.StartWith("state:"));
            Assert.That(observed[2], Does.StartWith("content:"));
            Assert.That(observed[3], Does.StartWith("state:"));
            Assert.That(document.CreateSnapshot().Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "First", "Second" }));
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
        public void RowReferenceTraversal_ReportsAndRewritesTheSameSitesAndOccurrences()
        {
            var gameDB = CreateModel();
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Recipes", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            var recipes = (TableModel)gameDB.Tables["Recipes"];
            Assert.That(items.AddKey("Sword"), Is.True);
            Assert.That(recipes.AddField("Result", FieldType.tableRef, false, "Items"),
                Is.True);
            Assert.That(recipes.AddField("Ingredients", FieldType.tableRef, true, "Items"),
                Is.True);
            Assert.That(recipes.AddField("Slots", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.tableRef, "Items")),
                Is.True);
            Assert.That(recipes.AddKey("Forge"), Is.True);
            Assert.That(recipes.AddKey("Enumerable"), Is.True);
            Assert.That(recipes.AddKey("Malformed"), Is.True);
            Assert.That(recipes.SetValue("Forge", "Result", "Sword"), Is.True);
            Assert.That(recipes.SetValue("Forge", "Ingredients",
                new List<object> { "Sword", "Sword", FieldBase.NullRefToken }), Is.True);
            Assert.That(recipes.SetValue("Forge", "Slots",
                new Dictionary<string, object>
                {
                    { "Primary", "Sword" },
                    { "Secondary", "Sword" }
                }), Is.True);
            ((RowModel)recipes.Data["Enumerable"]).SetValue("Ingredients",
                new EnumerableOnly("Sword", "Sword", FieldBase.NullRefToken));
            ((RowModel)recipes.Data["Malformed"]).SetValue("Slots", "Sword");

            var sites = GameDBModelOperations.FindRowReferenceSites(
                gameDB, "Items", "Sword");

            Assert.That(sites.Select(site => new
            {
                site.Path,
                site.Kind,
                site.OccurrenceCount
            }), Is.EquivalentTo(new[]
            {
                new
                {
                    Path = "Recipes[Forge].Result",
                    Kind = GameDBRowReferenceKind.Scalar,
                    OccurrenceCount = 1
                },
                new
                {
                    Path = "Recipes[Forge].Ingredients",
                    Kind = GameDBRowReferenceKind.ArrayElement,
                    OccurrenceCount = 2
                },
                new
                {
                    Path = "Recipes[Forge].Slots",
                    Kind = GameDBRowReferenceKind.DictionaryValue,
                    OccurrenceCount = 2
                },
                new
                {
                    Path = "Recipes[Enumerable].Ingredients",
                    Kind = GameDBRowReferenceKind.ArrayElement,
                    OccurrenceCount = 2
                },
                new
                {
                    Path = "Recipes[Malformed].Slots",
                    Kind = GameDBRowReferenceKind.InvalidShape,
                    OccurrenceCount = 1
                }
            }));
            Assert.That(GameDBModelOperations.FindRowReferences(
                gameDB, "Items", "Sword"), Is.EquivalentTo(sites.Select(site => site.Path)));
            var impact = GameDBModelOperations.GetRowReferenceImpact(
                gameDB, "Items", "Sword");
            Assert.That(impact.SiteCount, Is.EqualTo(5));
            Assert.That(impact.OccurrenceCount, Is.EqualTo(8));
            Assert.That(impact.RewriteOccurrenceCount, Is.EqualTo(7));

            GameDBModelOperations.RenameRowReferences(gameDB, "Items", "Sword", "Blade");

            var unresolved = GameDBModelOperations.FindRowReferenceSites(
                gameDB, "Items", "Sword");
            Assert.That(unresolved, Has.Count.EqualTo(1));
            Assert.That(unresolved.Single().Kind, Is.EqualTo(
                GameDBRowReferenceKind.InvalidShape));
            var renamed = GameDBModelOperations.FindRowReferenceSites(
                gameDB, "Items", "Blade");
            Assert.That(renamed.Sum(site => site.OccurrenceCount), Is.EqualTo(7));
            var forge = (RowModel)recipes.Data["Forge"];
            Assert.That(forge.Data["Result"], Is.EqualTo("Blade"));
            Assert.That(((IEnumerable)forge.Data["Ingredients"]).Cast<object>(),
                Is.EqualTo(new[] { "Blade", "Blade", FieldBase.NullRefToken }));
            Assert.That(((IDictionary)forge.Data["Slots"]).Values.Cast<object>(),
                Is.EquivalentTo(new[] { "Blade", "Blade" }));
            Assert.That(((IEnumerable)((RowModel)recipes.Data["Enumerable"])
                .Data["Ingredients"]).Cast<object>(),
                Is.EqualTo(new[] { "Blade", "Blade", FieldBase.NullRefToken }));
            Assert.That(((RowModel)recipes.Data["Malformed"]).Data["Slots"],
                Is.EqualTo("Sword"));
            Assert.That(unresolved.Single().Path,
                Is.EqualTo("Recipes[Malformed].Slots"));
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
        public void SetDatabaseMetadata_CommitsScopeAndLocalizationAsOneChange()
        {
            var document = CreateItemsDocument();
            var changes = new List<GameDBDocumentChange>();
            document.Changed += changes.Add;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("LocalizedItems", true)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Changes, Is.EqualTo(new[]
            {
                GameDBCommandKind.SetDatabaseMetadata
            }));
            Assert.That(result.AttemptedSnapshot.ScopeName, Is.EqualTo("LocalizedItems"));
            Assert.That(result.AttemptedSnapshot.LocalizationDatabase, Is.True);
            Assert.That(document.CurrentRevision, Is.EqualTo(result.AttemptedRevision));
            Assert.That(document.IsDirty, Is.True);
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Commands, Is.EqualTo(result.Changes));
        }

        [Test]
        public void SetDatabaseMetadata_NoOpDoesNotNotifyOrDirtyDocument()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;
            var notifications = 0;
            document.Changed += change => notifications++;

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("DocumentTests", false)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.AttemptedRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void SetDatabaseMetadata_EmptyScopeFailsValidationAndPreservesDocument()
        {
            var document = CreateItemsDocument();
            var revisionBefore = document.CurrentRevision;
            var snapshotBefore = document.CreateSnapshot();

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("", true)
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBTransactionFailureKind.ValidationFailed));
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("scope.empty"));
            Assert.That(result.AttemptedSnapshot.ScopeName, Is.Empty);
            Assert.That(result.AttemptedSnapshot.LocalizationDatabase, Is.True);
            Assert.That(document.CurrentRevision, Is.EqualTo(revisionBefore));
            Assert.That(document.CreateSnapshot().ScopeName, Is.EqualTo(snapshotBefore.ScopeName));
            Assert.That(document.CreateSnapshot().LocalizationDatabase,
                Is.EqualTo(snapshotBefore.LocalizationDatabase));
            Assert.That(document.IsDirty, Is.False);
        }

        [Test]
        public void ReplaceWorkingState_PublishesCanonicalStateWithOriginAndNotification()
        {
            var document = CreateItemsDocument();
            var target = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 15L)
            }).AttemptedState;
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20L)
            }).Success, Is.True);
            var revisionBefore = document.CurrentRevision;
            var changes = new List<GameDBDocumentChange>();
            document.Changed += changes.Add;

            var result = document.ReplaceWorkingState(target.DataJson, target.SchemaJson,
                revisionBefore, GameDBDocumentChangeOrigin.Undo);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.FailureKind, Is.EqualTo(GameDBWorkingStateFailureKind.None));
            Assert.That(result.RevisionBefore, Is.EqualTo(revisionBefore));
            Assert.That(result.RevisionAfter, Is.EqualTo(target.Revision));
            Assert.That(result.AttemptedRevision, Is.EqualTo(target.Revision));
            Assert.That(result.AttemptedState.Revision, Is.EqualTo(target.Revision));
            Assert.That(result.AttemptedSnapshot.Revision, Is.EqualTo(target.Revision));
            Assert.That(RowValue(result.AttemptedSnapshot, "Items", "Sword", "Power"),
                Is.EqualTo(15L));
            Assert.That(document.CurrentRevision, Is.EqualTo(target.Revision));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Origin, Is.EqualTo(GameDBDocumentChangeOrigin.Undo));
            Assert.That(changes[0].Commands, Is.Empty);
        }

        [Test]
        public void ReplaceWorkingState_StateNotificationMapsOriginAndSnapshot()
        {
            var document = CreateItemsDocument();
            var target = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 15L)
            }).AttemptedState;
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20L)
            }).Success, Is.True);
            var before = document.GetSessionState();
            GameDBDocumentStateChange observed = null;
            document.StateChanged += change => observed = change;

            var result = document.ReplaceWorkingState(target.DataJson, target.SchemaJson,
                document.CurrentRevision, GameDBDocumentChangeOrigin.Redo);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(observed, Is.Not.Null);
            Assert.That(observed.Origin, Is.EqualTo(GameDBDocumentStateChangeOrigin.Redo));
            Assert.That(observed.Previous, Is.EqualTo(before));
            Assert.That(observed.Current.CurrentRevision, Is.EqualTo(target.Revision));
            Assert.That(observed.Current, Is.EqualTo(document.GetSessionState()));
            Assert.That(observed.SaveStatus, Is.Null);
        }

        [Test]
        public void ReplaceWorkingState_NoOpDoesNotNotify()
        {
            var document = CreateItemsDocument();
            var current = document.SerializeCurrent();
            var notifications = 0;
            var stateNotifications = 0;
            document.Changed += change => notifications++;
            document.StateChanged += change => stateNotifications++;

            var result = document.ReplaceWorkingState(current.DataJson, current.SchemaJson,
                current.Revision, GameDBDocumentChangeOrigin.Recovery);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.RevisionAfter, Is.EqualTo(current.Revision));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.Zero);
            Assert.That(stateNotifications, Is.Zero);
        }

        [Test]
        public void ReplaceWorkingState_StaleRevisionAndMalformedJsonPreserveDocument()
        {
            var document = CreateItemsDocument();
            var current = document.SerializeCurrent();
            var notifications = 0;
            document.Changed += change => notifications++;

            var missingExpected = document.ReplaceWorkingState(current.DataJson,
                current.SchemaJson, null, GameDBDocumentChangeOrigin.Undo);
            var transactionOrigin = document.ReplaceWorkingState(current.DataJson,
                current.SchemaJson, current.Revision, GameDBDocumentChangeOrigin.Transaction);
            var missingJson = document.ReplaceWorkingState(null, current.SchemaJson,
                current.Revision, GameDBDocumentChangeOrigin.Undo);
            var stale = document.ReplaceWorkingState(current.DataJson, current.SchemaJson,
                "stale", GameDBDocumentChangeOrigin.Undo);
            var invalidOrigin = document.ReplaceWorkingState(current.DataJson, current.SchemaJson,
                current.Revision, (GameDBDocumentChangeOrigin)999);
            var malformed = document.ReplaceWorkingState("{", current.SchemaJson,
                current.Revision, GameDBDocumentChangeOrigin.Undo);

            Assert.That(missingExpected.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.InvalidRequest));
            Assert.That(transactionOrigin.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.InvalidRequest));
            Assert.That(missingJson.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.InvalidRequest));
            Assert.That(stale.Success, Is.False);
            Assert.That(stale.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.RevisionConflict));
            Assert.That(invalidOrigin.Success, Is.False);
            Assert.That(invalidOrigin.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.InvalidRequest));
            Assert.That(malformed.Success, Is.False);
            Assert.That(malformed.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.ImportFailed));
            Assert.That(document.CurrentRevision, Is.EqualTo(current.Revision));
            Assert.That(RowValue(document.CreateSnapshot(), "Items", "Sword", "Power"),
                Is.EqualTo(12L));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ReplaceWorkingState_ValidationFailureRetainsAttemptAndPreservesDocument()
        {
            var document = CreateItemsDocument();
            var current = document.SerializeCurrent();
            var notifications = 0;
            document.Changed += change => notifications++;
            var invalid = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetDatabaseMetadataCommand("", false)
            });
            Assert.That(invalid.FailureKind,
                Is.EqualTo(GameDBTransactionFailureKind.ValidationFailed));

            var result = document.ReplaceWorkingState(
                invalid.AttemptedState.DataJson, invalid.AttemptedState.SchemaJson,
                current.Revision, GameDBDocumentChangeOrigin.Redo);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureKind,
                Is.EqualTo(GameDBWorkingStateFailureKind.ValidationFailed));
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("scope.empty"));
            Assert.That(result.RevisionAfter, Is.EqualTo(current.Revision));
            Assert.That(result.AttemptedRevision, Is.EqualTo(invalid.AttemptedRevision));
            Assert.That(result.AttemptedState.Revision, Is.EqualTo(invalid.AttemptedRevision));
            Assert.That(result.AttemptedSnapshot.ScopeName, Is.Empty);
            Assert.That(document.CurrentRevision, Is.EqualTo(current.Revision));
            Assert.That(document.CreateSnapshot().ScopeName, Is.EqualTo("DocumentTests"));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Changed_ReentrantWorkingStateReplacementPreservesMixedFifoOrder()
        {
            var document = CreateItemsDocument();
            var target = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 15L)
            }).AttemptedState;
            var observed = new List<string>();
            document.Changed += change =>
            {
                observed.Add("first:" + change.Origin);
                if (change.Origin == GameDBDocumentChangeOrigin.Transaction)
                {
                    var nested = document.ReplaceWorkingState(target.DataJson, target.SchemaJson,
                        change.RevisionAfter, GameDBDocumentChangeOrigin.Undo);
                    Assert.That(nested.Success, Is.True, nested.Message);
                }
            };
            document.Changed += change => observed.Add("second:" + change.Origin);

            var result = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20L)
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(observed, Is.EqualTo(new[]
            {
                "first:Transaction",
                "second:Transaction",
                "first:Undo",
                "second:Undo"
            }));
            Assert.That(document.CurrentRevision, Is.EqualTo(target.Revision));
        }

        [Test]
        public void ReplaceWorkingState_SubscriberFailureDoesNotBlockPublication()
        {
            var document = CreateItemsDocument();
            var target = document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 15L)
            }).AttemptedState;
            Assert.That(document.ApplyTransaction(new GameDBCommand[]
            {
                new SetValueCommand("Items", "Sword", "Power", 20L)
            }).Success, Is.True);
            document.Changed += change => throw new InvalidOperationException("subscriber exploded");

            var result = document.ReplaceWorkingState(target.DataJson, target.SchemaJson,
                document.CurrentRevision, GameDBDocumentChangeOrigin.Undo);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(document.CurrentRevision, Is.EqualTo(target.Revision));
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

        private sealed class EnumerableOnly : IEnumerable
        {
            private readonly object[] m_values;

            internal EnumerableOnly(params object[] values)
            {
                m_values = values;
            }

            public IEnumerator GetEnumerator()
            {
                return m_values.GetEnumerator();
            }
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
