using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameDBLibrary.Tests
{
    internal sealed class GameDBAtomicImportTests
    {
        [Test]
        public void RawRowAndTableLookupsAreNotPublic()
        {
            Assert.That(typeof(GameDBBase).Assembly.GetType("GameDBLibrary.IGameDB"),
                Is.Null);
            Assert.That(typeof(RowBase).GetMethod("GetValue",
                BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(TableBase).GetMethod("GetByKeyRaw",
                BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(global::Row).GetMethod("GetCacheOrCreateAccessor",
                BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(global::Row).GetMethod("GetCacheOrCreateListAccessor",
                BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void CoreMutableValueAccessorsReturnDefensiveCopies()
        {
            var colorAccessor = new ColorAccessor(new Color(1, 2, 3, 4));
            var firstColor = colorAccessor.GetValue();
            firstColor.r = 99;
            Assert.That(colorAccessor.GetValue().r, Is.EqualTo(1));

            var vector2Accessor = new Vector2Accessor(new Vector2(1f, 2f));
            var firstVector2 = vector2Accessor.GetValue();
            firstVector2.x = 99f;
            Assert.That(vector2Accessor.GetValue().x, Is.EqualTo(1f));

            var vector3Accessor = new Vector3Accessor(new Vector3(1f, 2f, 3f));
            var firstVector3 = vector3Accessor.GetValue();
            firstVector3.y = 99f;
            Assert.That(vector3Accessor.GetValue().y, Is.EqualTo(2f));

            var vector4Accessor = new Vector4Accessor(new Vector4(1f, 2f, 3f, 4f));
            var firstVector4 = vector4Accessor.GetValue();
            firstVector4.w = 99f;
            Assert.That(vector4Accessor.GetValue().w, Is.EqualTo(4f));
        }

        [Test]
        public void Import_SuccessPublishesEveryTableAndPreservesTableIdentity()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var firstTable = gameDB.GetTable("First");
            var secondTable = gameDB.GetTable("Second");
            var oldFirstRow = gameDB.GetRow("First");
            var oldSnapshot = gameDB.Snapshot;

            var error = gameDB.Import(Json("new-first", "new-second"));

            Assert.That(error, Is.Null);
            Assert.That(gameDB.GetTable("First"), Is.SameAs(firstTable));
            Assert.That(gameDB.GetTable("Second"), Is.SameAs(secondTable));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));
            Assert.That(gameDB.GetRow("First"), Is.Not.SameAs(oldFirstRow));
            Assert.That(oldFirstRow.GetValue("Value"), Is.EqualTo("old-first"));
            Assert.That(gameDB.Snapshot, Is.Not.SameAs(oldSnapshot));
        }

        [TestCase("First")]
        [TestCase("Second")]
        [TestCase("Third")]
        public void Import_MalformedTableAtAnyPositionPreservesEveryPreviousRow(
            string malformedTable)
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second", "old-third");
            var firstRow = gameDB.GetRow("First");
            var secondRow = gameDB.GetRow("Second");
            var thirdRow = gameDB.GetRow("Third");
            var snapshot = gameDB.Snapshot;

            var error = gameDB.Import(Json(
                malformedTable == "First" ? 42 : "new-first",
                malformedTable == "Second" ? 42 : "new-second",
                malformedTable == "Third" ? 42 : "new-third"));

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(gameDB.GetRow("First"), Is.SameAs(firstRow));
            Assert.That(gameDB.GetRow("Second"), Is.SameAs(secondRow));
            Assert.That(gameDB.GetRow("Third"), Is.SameAs(thirdRow));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            Assert.That(gameDB.GetValue("Third"), Is.EqualTo("old-third"));
            Assert.That(gameDB.Snapshot, Is.SameAs(snapshot));
        }

        [Test]
        public void Import_SuccessfulPartialColumnsPublishFreshRows()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");

            var error = gameDB.Import(Json("new-first", "new-second",
                "new-extra-first", "new-extra-second"), new[] { "Value" }, false);

            Assert.That(error, Is.Null);
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));
            Assert.Throws<KeyNotFoundException>(() => gameDB.GetExtra("First"));
            Assert.Throws<KeyNotFoundException>(() => gameDB.GetExtra("Second"));
        }

        [Test]
        public void Import_PartialColumnsRemainAtomicAcrossTables()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var firstRow = gameDB.GetRow("First");
            var secondRow = gameDB.GetRow("Second");

            var error = gameDB.Import(Json("new-first", 42, "new-extra-first",
                "new-extra-second"), new[] { "Value" });

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(gameDB.GetRow("First"), Is.SameAs(firstRow));
            Assert.That(gameDB.GetRow("Second"), Is.SameAs(secondRow));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetExtra("First"), Is.EqualTo("old-extra-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            Assert.That(gameDB.GetExtra("Second"), Is.EqualTo("old-extra-second"));
        }

        [Test]
        public void Import_PartialColumnsRejectMissingSelectedFields()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var firstRow = gameDB.GetRow("First");
            var secondRow = gameDB.GetRow("Second");
            var thirdRow = gameDB.GetRow("Third");

            var error = gameDB.Import(SparseLocalizationJson(
                "new-primary", "new-fallback", string.Empty),
                new[] { "Value", "Extra" });

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(gameDB.GetRow("First"), Is.SameAs(firstRow));
            Assert.That(gameDB.GetRow("Second"), Is.SameAs(secondRow));
            Assert.That(gameDB.GetRow("Third"), Is.SameAs(thirdRow));
        }

        [Test]
        public void ImportLocalizationData_AllowsMissingSelectedFieldsAndPublishesMetadata()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            gameDB.SetMetadata("old-language");
            var notifications = 0;
            string metadataAtNotification = null;
            string fallbackAtNotification = null;
            gameDB.OnDBLoaded += () =>
            {
                notifications++;
                metadataAtNotification = gameDB.Metadata;
                fallbackAtNotification = gameDB.GetExtra("Second");
            };

            var error = gameDB.ImportLocalization(
                SparseLocalizationJson("new-primary", "new-fallback", string.Empty),
                new[] { "Value", "Extra" }, "new-language");

            Assert.That(error, Is.Null);
            Assert.That(gameDB.Metadata, Is.EqualTo("new-language"));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-primary"));
            Assert.Throws<KeyNotFoundException>(() => gameDB.GetExtra("First"));
            Assert.Throws<KeyNotFoundException>(() => gameDB.GetValue("Second"));
            Assert.That(gameDB.GetExtra("Second"), Is.EqualTo("new-fallback"));
            Assert.That(gameDB.GetValue("Third"), Is.Empty);
            Assert.Throws<KeyNotFoundException>(() => gameDB.GetExtra("Third"));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(metadataAtNotification, Is.EqualTo("new-language"));
            Assert.That(fallbackAtNotification, Is.EqualTo("new-fallback"));
        }

        [Test]
        public void ImportLocalizationData_InvalidPresentFieldPreservesRowsAndMetadata()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            gameDB.SetMetadata("old-language");
            var firstRow = gameDB.GetRow("First");
            var secondRow = gameDB.GetRow("Second");
            var notifications = 0;
            gameDB.OnDBLoaded += () => notifications++;

            var error = gameDB.ImportLocalization(
                SparseLocalizationJson("new-primary", 42, string.Empty),
                new[] { "Value", "Extra" }, "new-language");

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(gameDB.Metadata, Is.EqualTo("old-language"));
            Assert.That(gameDB.GetRow("First"), Is.SameAs(firstRow));
            Assert.That(gameDB.GetRow("Second"), Is.SameAs(secondRow));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Import_NotifiesOnceAfterCompletePublication()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var notifications = 0;
            string firstAtNotification = null;
            string secondAtNotification = null;
            gameDB.OnDBLoaded += () =>
            {
                notifications++;
                firstAtNotification = gameDB.GetValue("First");
                secondAtNotification = gameDB.GetValue("Second");
            };

            var error = gameDB.Import(Json("new-first", "new-second"));

            Assert.That(error, Is.Null);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(firstAtNotification, Is.EqualTo("new-first"));
            Assert.That(secondAtNotification, Is.EqualTo("new-second"));
        }

        [Test]
        public void Import_NotifyFalseCommitsWithoutNotification()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var notifications = 0;
            gameDB.OnDBLoaded += () => notifications++;

            var error = gameDB.Import(Json("new-first", "new-second"), false);

            Assert.That(error, Is.Null);
            Assert.That(notifications, Is.Zero);
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));
        }

        [Test]
        public void Import_FailureDoesNotNotify()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var notifications = 0;
            gameDB.OnDBLoaded += () => notifications++;

            var error = gameDB.Import(Json("new-first", 42));

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Import_CallbackExceptionPropagatesAfterCommitAndReleasesGate()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            Action callback = () => throw new InvalidOperationException("callback failed");
            gameDB.OnDBLoaded += callback;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                gameDB.Import(Json("new-first", "new-second")));

            Assert.That(exception.Message, Is.EqualTo("callback failed"));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));

            gameDB.OnDBLoaded -= callback;
            Assert.That(gameDB.Import(Json("final-first", "final-second")), Is.Null);
        }

        [Test]
        public void Import_ReentrantCallbackIsRejectedUntilNotificationCompletes()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            Exception reentrantError = null;
            gameDB.OnDBLoaded += () =>
            {
                reentrantError = gameDB.Import(Json("reentrant-first", "reentrant-second"));
            };

            var error = gameDB.Import(Json("new-first", "new-second"));

            Assert.That(error, Is.Null);
            Assert.That(reentrantError, Is.TypeOf<InvalidOperationException>());
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));
        }

        [TestCase("direct")]
        [TestCase("array")]
        [TestCase("dictionary")]
        public void Import_MissingTableReferencePreservesPublication(string position)
        {
            var gameDB = new ReferenceTestGameDB();
            Assert.That(gameDB.Import(ReferenceJson("Existing", "Existing",
                "Existing"), false), Is.Null);
            var snapshot = gameDB.Snapshot;
            var missing = "Missing";

            var error = gameDB.Import(ReferenceJson(
                position == "direct" ? missing : "Existing",
                position == "array" ? missing : "Existing",
                position == "dictionary" ? missing : "Existing"), false);

            Assert.That(error, Is.TypeOf<FormatException>());
            Assert.That(error.Message, Does.Contain("Sources[Source]"));
            Assert.That(error.Message, Does.Contain("Targets[Missing]"));
            Assert.That(gameDB.Snapshot, Is.SameAs(snapshot));
        }

        [Test]
        public void Import_ForwardTableReferenceUsesCompleteStagedGraph()
        {
            var gameDB = new ReferenceTestGameDB();

            var error = gameDB.Import(ReferenceJson("Existing", "Existing",
                "Existing"), false);

            Assert.That(error, Is.Null);
        }

        [Test]
        public void FailedGateAcquirersDoNotReleaseCurrentOwner()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            Assert.That(gameDB.Internal.TryBeginOperation(), Is.True);

            try
            {
                var second = gameDB.Import(Json("second-first", "second-second"));
                var third = gameDB.Import(Json("third-first", "third-second"));

                Assert.That(second, Is.TypeOf<InvalidOperationException>());
                Assert.That(third, Is.TypeOf<InvalidOperationException>());
                Assert.That(gameDB.GetValue("First"), Is.EqualTo("old-first"));
                Assert.That(gameDB.GetValue("Second"), Is.EqualTo("old-second"));
            }
            finally
            {
                gameDB.Internal.EndOperation();
            }

            Assert.That(gameDB.Import(Json("final-first", "final-second")), Is.Null);
        }

        private static AtomicTestGameDB CreateLoadedDatabase(string first,
            string second, string third = "old-third")
        {
            var gameDB = new AtomicTestGameDB();
            Assert.That(gameDB.Import(Json(first, second, third,
                "old-extra-first", "old-extra-second", "old-extra-third"), false),
                Is.Null);
            return gameDB;
        }

        private static string Json(object firstValue, object secondValue,
            object thirdValue = null, string firstExtra = "extra-first",
            string secondExtra = "extra-second", string thirdExtra = "extra-third")
        {
            thirdValue ??= "third";
            return "{\"tables\":{" +
                "\"First\":{\"Row\":{\"Value\":" + JsonValue(firstValue) +
                ",\"Extra\":\"" + firstExtra + "\"}}," +
                "\"Second\":{\"Row\":{\"Value\":" + JsonValue(secondValue) +
                ",\"Extra\":\"" + secondExtra + "\"}}," +
                "\"Third\":{\"Row\":{\"Value\":" + JsonValue(thirdValue) +
                ",\"Extra\":\"" + thirdExtra + "\"}}}}";
        }

        private static string ReferenceJson(string direct, string array,
            string dictionary)
        {
            return "{\"tables\":{" +
                "\"Sources\":{\"Source\":{" +
                "\"Direct\":\"" + direct + "\"," +
                "\"Array\":[\"" + array + "\"]," +
                "\"Dictionary\":{\"Primary\":\"" + dictionary + "\"}}}," +
                "\"Targets\":{\"Existing\":{}}}}";
        }

        private static string SparseLocalizationJson(object firstValue,
            object secondExtra, object thirdValue)
        {
            return "{\"tables\":{" +
                "\"First\":{\"Row\":{\"Value\":" + JsonValue(firstValue) + "}}," +
                "\"Second\":{\"Row\":{\"Extra\":" + JsonValue(secondExtra) + "}}," +
                "\"Third\":{\"Row\":{\"Value\":" + JsonValue(thirdValue) + "}}}}";
        }

        private static string JsonValue(object value)
        {
            return value is string text ? "\"" + text + "\"" : value.ToString();
        }

        private sealed class ReferenceTestGameDB : GameDBBase
        {
            internal ReferenceTestGameDB()
                : base("ReferenceTest", "ReferenceTest")
            {
                var sources = new TableBase("Sources", KeyType.@string, null,
                    key => new RowBase(key));
                sources.Fields.Add("Direct", new FieldBase("Direct",
                    FieldType.tableRef, false, "Targets"));
                sources.Fields.Add("Array", new FieldBase("Array",
                    FieldType.tableRef, true, "Targets"));
                sources.Fields.Add("Dictionary", new FieldBase("Dictionary",
                    FieldType.dictionary, false,
                    new DictionaryType(KeyType.@string, null,
                        FieldType.tableRef, "Targets")));
                RegisterTable("Sources", sources);
                RegisterTable("Targets", new TableBase("Targets", KeyType.@string,
                    null, key => new RowBase(key)));
            }

            internal RuntimeGameDBSnapshot Snapshot => m_internal.CurrentSnapshot;
        }

        private sealed class AtomicTestGameDB : GameDBBase
        {
            internal AtomicTestGameDB()
                : base("AtomicTest", "AtomicTest")
            {
                RegisterTable("First", CreateTable("First"));
                RegisterTable("Second", CreateTable("Second"));
                RegisterTable("Third", CreateTable("Third"));
            }

            internal GameDBInternal Internal => m_internal;
            internal RuntimeGameDBSnapshot Snapshot => m_internal.CurrentSnapshot;
            internal string Metadata => Snapshot?.Metadata as string;

            internal Exception ImportLocalization(string jsonData,
                string[] columnImportList, string metadata, bool notify = true)
            {
                return ImportLocalizationData(jsonData, columnImportList, notify,
                    metadata);
            }

            internal void SetMetadata(string metadata)
            {
                var error = ImportLocalization(Json("old-first", "old-second"),
                    new[] { "Value", "Extra" }, metadata, false);
                if (error != null)
                {
                    throw error;
                }
            }

            internal TableBase GetTable(string name)
            {
                return m_internal.Tables[name];
            }

            internal RowBase GetRow(string table)
            {
                return m_internal.Tables[table].GetByKeyRaw("Row");
            }

            internal string GetValue(string table)
            {
                return (string)GetRow(table).GetValue("Value");
            }

            internal string GetExtra(string table)
            {
                return (string)GetRow(table).GetValue("Extra");
            }

            private static TableBase CreateTable(string name)
            {
                var table = new TableBase(name, KeyType.@string, null,
                    key => new RowBase(key));
                table.Fields.Add("Value", new FieldBase("Value", FieldType.@string, false));
                table.Fields.Add("Extra", new FieldBase("Extra", FieldType.@string, false));
                return table;
            }
        }
    }
}
