using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace GameDBLibrary.Tests
{
    internal sealed class GameDBAtomicImportTests
    {
        [Test]
        public void Import_SuccessPublishesEveryTableAndPreservesTableIdentity()
        {
            var gameDB = CreateLoadedDatabase("old-first", "old-second");
            var firstTable = gameDB.GetTable("First");
            var secondTable = gameDB.GetTable("Second");
            var oldFirstRow = gameDB.GetRow("First");

            var error = gameDB.Import(Json("new-first", "new-second"));

            Assert.That(error, Is.Null);
            Assert.That(gameDB.GetTable("First"), Is.SameAs(firstTable));
            Assert.That(gameDB.GetTable("Second"), Is.SameAs(secondTable));
            Assert.That(gameDB.GetValue("First"), Is.EqualTo("new-first"));
            Assert.That(gameDB.GetValue("Second"), Is.EqualTo("new-second"));
            Assert.That(gameDB.GetRow("First"), Is.Not.SameAs(oldFirstRow));
            Assert.That(oldFirstRow.GetValue("Value"), Is.EqualTo("old-first"));
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

        private static string JsonValue(object value)
        {
            return value is string text ? "\"" + text + "\"" : value.ToString();
        }

        private sealed class AtomicTestGameDB : GameDBBase
        {
            internal AtomicTestGameDB()
                : base("AtomicTest", "AtomicTest")
            {
                Tables.Add("First", CreateTable("First"));
                Tables.Add("Second", CreateTable("Second"));
                Tables.Add("Third", CreateTable("Third"));
            }

            internal GameDBInternal Internal => m_internal;

            internal TableBase GetTable(string name)
            {
                return Tables[name];
            }

            internal RowBase GetRow(string table)
            {
                return Tables[table].GetByKeyRaw("Row");
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
