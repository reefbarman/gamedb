using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.UI;
using GameDBLibrary;
using NUnit.Framework;
using System.Collections.Generic;

namespace GameDBLibrary.Tests
{
    public class GameDBInspectorStateTests
    {
        [Test]
        public void Context_UsesStableTabTableAndFieldIdentity()
        {
            var first = GameDBInspectorContext.Field("tab", "document", "Items", "Name");
            var equal = GameDBInspectorContext.Field("tab", "document", "Items", "Name");
            var different = GameDBInspectorContext.Field("tab", "document", "Items", "Price");

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first, Is.Not.EqualTo(GameDBInspectorContext.Field(
                "tab", "other-document", "Items", "Name")));
            Assert.That(GameDBInspectorContext.Database("tab", "document").Kind,
                Is.EqualTo(GameDBInspectorContextKind.Database));
            Assert.Throws<System.ArgumentException>(() =>
                GameDBInspectorContext.Table("tab", "document", null));
            Assert.Throws<System.ArgumentException>(() =>
                GameDBInspectorContext.Table("tab", null, "Items"));
        }

        [Test]
        public void Fingerprint_IgnoresRevisionRowsAndSourceOrdering()
        {
            var first = Snapshot("revision-1", "Sword", 1,
                Field("Name", FieldType.@string),
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@enum, "Game.Stat", FieldType.@int, null)));
            var second = Snapshot("revision-2", "Shield", 999,
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@enum, "Game.Stat", FieldType.@int, null)),
                Field("Name", FieldType.@string));
            var context = GameDBInspectorContext.Field("tab", "document", "Items", "Name");

            Assert.That(GameDBInspectorSchemaFingerprint.Capture(first,
                GameDBInspectorTaskKind.RenameField, context), Is.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(second,
                    GameDBInspectorTaskKind.RenameField, context)));
        }

        [Test]
        public void Fingerprint_DetectsRelevantFieldAndDictionarySchemaChanges()
        {
            var context = GameDBInspectorContext.Field("tab", "document", "Items", "Stats");
            var first = Snapshot("revision-1", "Sword", 1,
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@string, null, FieldType.@int, null)));
            var valueTypeChanged = Snapshot("revision-2", "Sword", 1,
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@string, null, FieldType.@float, null)));
            var renamed = Snapshot("revision-3", "Sword", 1,
                Field("Statistics", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@string, null, FieldType.@int, null)));

            var opening = GameDBInspectorSchemaFingerprint.Capture(first,
                GameDBInspectorTaskKind.ChangeFieldType, context);
            Assert.That(opening, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(valueTypeChanged,
                    GameDBInspectorTaskKind.ChangeFieldType, context)));
            Assert.That(opening, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(renamed,
                    GameDBInspectorTaskKind.ChangeFieldType, context)));
        }

        [Test]
        public void TaskStaleness_IgnoresRowsButDetectsRelevantSchemaChanges()
        {
            var context = GameDBInspectorContext.Table("tab", "document", "Items");
            var opening = Snapshot("revision-1", "Sword", 1,
                Field("Name", FieldType.@string));
            var task = new GameDBInspectorTaskState(
                GameDBInspectorTaskKind.CreateField, context,
                new GameDBInspectorFieldDraft("Price",
                    new GameDBInspectorFieldTypeDraft(
                        FieldType.@float, false, null)), opening);

            task.MarkDirty();
            Assert.That(task.IsDirty, Is.True);
            Assert.That(task.RecheckStaleness(Snapshot("revision-2", "Shield", 2,
                Field("Name", FieldType.@string))), Is.False);
            Assert.That(task.RecheckStaleness(Snapshot("revision-3", "Shield", 2,
                Field("Name", FieldType.@string),
                Field("Price", FieldType.@float))), Is.True);
            Assert.That(task.RecheckStaleness(null), Is.True);
        }

        [Test]
        public void EditDatabaseFingerprint_TracksMetadataAndCompatibilityNotFieldDetails()
        {
            var context = GameDBInspectorContext.Database("tab", "document");
            var opening = Snapshot("revision-1", "Sword", 1,
                Field("Name", FieldType.@string));
            var compatibleSchemaChange = Snapshot("revision-2", "Sword", 1,
                Field("Label", FieldType.@string));
            var incompatibleSchemaChange = Snapshot("revision-3", "Sword", 1,
                Field("Power", FieldType.@int));
            incompatibleSchemaChange.ScopeName = opening.ScopeName;

            var fingerprint = GameDBInspectorSchemaFingerprint.Capture(opening,
                GameDBInspectorTaskKind.EditDatabase, context);
            Assert.That(fingerprint, Is.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(compatibleSchemaChange,
                    GameDBInspectorTaskKind.EditDatabase, context)));
            Assert.That(fingerprint, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(incompatibleSchemaChange,
                    GameDBInspectorTaskKind.EditDatabase, context)));
            incompatibleSchemaChange.Tables = compatibleSchemaChange.Tables;
            incompatibleSchemaChange.ScopeName = "Other";
            Assert.That(fingerprint, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(incompatibleSchemaChange,
                    GameDBInspectorTaskKind.EditDatabase, context)));
        }

        [Test]
        public void RenameFieldFingerprint_IgnoresSiblingTypesButTracksFieldNames()
        {
            var context = GameDBInspectorContext.Field(
                "tab", "document", "Items", "Name");
            var opening = Snapshot("revision-1", "Sword", 1,
                Field("Name", FieldType.@string),
                Field("Power", FieldType.@int));
            var siblingTypeChanged = Snapshot("revision-2", "Sword", 1,
                Field("Name", FieldType.@string),
                Field("Power", FieldType.@float));
            var siblingRenamed = Snapshot("revision-3", "Sword", 1,
                Field("Name", FieldType.@string),
                Field("Strength", FieldType.@float));

            var fingerprint = GameDBInspectorSchemaFingerprint.Capture(opening,
                GameDBInspectorTaskKind.RenameField, context);
            Assert.That(fingerprint, Is.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(siblingTypeChanged,
                    GameDBInspectorTaskKind.RenameField, context)));
            Assert.That(fingerprint, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(siblingRenamed,
                    GameDBInspectorTaskKind.RenameField, context)));
        }

        [Test]
        public void ChangeFieldTypeFingerprint_IgnoresSiblingFieldTypeChanges()
        {
            var context = GameDBInspectorContext.Field(
                "tab", "document", "Items", "Stats");
            var opening = Snapshot("revision-1", "Sword", 1,
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@string, null, FieldType.@int, null)),
                Field("Name", FieldType.@string));
            var siblingChanged = Snapshot("revision-2", "Sword", 1,
                Field("Stats", FieldType.dictionary, dictionary:
                    Dictionary(KeyType.@string, null, FieldType.@int, null)),
                Field("Name", FieldType.@int));

            Assert.That(GameDBInspectorSchemaFingerprint.Capture(opening,
                GameDBInspectorTaskKind.ChangeFieldType, context), Is.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(siblingChanged,
                    GameDBInspectorTaskKind.ChangeFieldType, context)));
        }

        [Test]
        public void Fingerprint_SeparatesTaskKindsWithSameCanonicalDependencies()
        {
            var snapshot = Snapshot("revision", "Sword", 1);
            var context = GameDBInspectorContext.Database("tab", "document");

            Assert.That(GameDBInspectorSchemaFingerprint.Capture(snapshot,
                GameDBInspectorTaskKind.CreateTable, context), Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(snapshot,
                    GameDBInspectorTaskKind.RenameTable, context)));
        }

        [Test]
        public void Task_RejectsIncompatibleContextAndDraft()
        {
            var snapshot = Snapshot("revision", "Sword", 1);
            var table = GameDBInspectorContext.Table("tab", "document", "Items");

            Assert.Throws<System.ArgumentException>(() =>
                new GameDBInspectorTaskState(
                    GameDBInspectorTaskKind.ChangeFieldType, table,
                    new GameDBInspectorFieldNameDraft("Name"), snapshot));
            Assert.Throws<System.ArgumentException>(() =>
                new GameDBInspectorTaskState(
                    GameDBInspectorTaskKind.CreateField, table,
                    new GameDBInspectorFieldNameDraft("Name"), snapshot));
        }

        [Test]
        public void CreateTableFingerprint_TracksTableCatalogButNotFieldsOrRows()
        {
            var context = GameDBInspectorContext.Database("tab", "document");
            var opening = Snapshot("revision-1", "Sword", 1,
                Field("Name", FieldType.@string));
            var changedRowsAndFields = Snapshot("revision-2", "Shield", 3,
                Field("Price", FieldType.@float));
            var addedTable = Snapshot("revision-3", "Shield", 3,
                Field("Price", FieldType.@float));
            addedTable.Tables.Add(new GameDBTableSnapshot
            {
                Name = "Abilities",
                KeyType = KeyType.@string
            });

            var fingerprint = GameDBInspectorSchemaFingerprint.Capture(opening,
                GameDBInspectorTaskKind.CreateTable, context);
            Assert.That(fingerprint, Is.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(changedRowsAndFields,
                    GameDBInspectorTaskKind.CreateTable, context)));
            Assert.That(fingerprint, Is.Not.EqualTo(
                GameDBInspectorSchemaFingerprint.Capture(addedTable,
                    GameDBInspectorTaskKind.CreateTable, context)));
        }

        [Test]
        public void PendingIntent_CannotBeReplacedUntilTakenOrTaskEnds()
        {
            var state = new GameDBInspectorState();
            var context = GameDBInspectorContext.Table("tab", "document", "Items");
            state.BeginTask(new GameDBInspectorTaskState(
                GameDBInspectorTaskKind.RenameTable, context,
                new GameDBInspectorTableDraft("Gear", KeyType.@string, null),
                Snapshot("revision", "Sword", 1)));
            var first = new GameDBInspectorPendingIntent(
                GameDBInspectorPendingIntentKind.SelectTable,
                GameDBInspectorContext.Table("tab", "document", "Abilities"));

            Assert.Throws<System.InvalidOperationException>(() =>
                state.BeginTask(new GameDBInspectorTaskState(
                    GameDBInspectorTaskKind.RenameTable, context,
                    new GameDBInspectorTableDraft("Other", KeyType.@string, null),
                    Snapshot("revision", "Sword", 1))));
            Assert.Throws<System.InvalidOperationException>(() => state.SetContext(
                GameDBInspectorContext.Table("tab", "document", "Abilities")));
            Assert.Throws<System.ArgumentException>(() =>
                new GameDBInspectorPendingIntent(
                    GameDBInspectorPendingIntentKind.SelectField, context));
            Assert.Throws<System.ArgumentException>(() =>
                new GameDBInspectorPendingIntent(
                    GameDBInspectorPendingIntentKind.SelectTable,
                    GameDBInspectorContext.Table("tab", "document", "Abilities"),
                    "other-tab"));
            Assert.That(state.TrySetPendingIntent(first), Is.True);
            Assert.That(state.TrySetPendingIntent(new GameDBInspectorPendingIntent(
                GameDBInspectorPendingIntentKind.CloseInspector)), Is.False);
            Assert.That(state.TakePendingIntent(), Is.SameAs(first));
            Assert.That(state.PendingIntent, Is.Null);
            Assert.That(state.TrySetPendingIntent(new GameDBInspectorPendingIntent(
                GameDBInspectorPendingIntentKind.CloseInspector)), Is.True);

            state.CompleteTask(GameDBInspectorContext.Table(
                "tab", "document", "Gear"));
            Assert.That(state.Context.TableName, Is.EqualTo("Gear"));
            Assert.That(state.Task, Is.Null);
            Assert.That(state.PendingIntent, Is.Null);
            Assert.Throws<System.InvalidOperationException>(() =>
                state.TrySetPendingIntent(first));
            state.Reset();
            Assert.That(state.Context, Is.Null);
        }

        private static GameDBSnapshot Snapshot(string revision, string rowKey,
            object value, params GameDBFieldSnapshot[] fields)
        {
            return new GameDBSnapshot
            {
                Revision = revision,
                ScopeName = "Game",
                Tables = new List<GameDBTableSnapshot>
                {
                    new GameDBTableSnapshot
                    {
                        Name = "Items",
                        KeyType = KeyType.@string,
                        Fields = new List<GameDBFieldSnapshot>(fields),
                        Rows = new List<GameDBRowSnapshot>
                        {
                            new GameDBRowSnapshot
                            {
                                Key = rowKey,
                                Values = new Dictionary<string, object>
                                {
                                    { "Name", value }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static GameDBFieldSnapshot Field(string name, FieldType type,
            bool isArray = false, string typeArgument = null,
            GameDBDictionaryTypeDefinition dictionary = null)
        {
            return new GameDBFieldSnapshot
            {
                Name = name,
                FieldType = type,
                IsArray = isArray,
                TypeArgument = typeArgument,
                DictionaryType = dictionary
            };
        }

        private static GameDBDictionaryTypeDefinition Dictionary(KeyType keyType,
            string keyTypeArgument, FieldType valueType, string valueTypeArgument)
        {
            return new GameDBDictionaryTypeDefinition
            {
                KeyType = keyType,
                KeyTypeArgument = keyTypeArgument,
                ValueType = valueType,
                ValueTypeArgument = valueTypeArgument
            };
        }
    }
}
