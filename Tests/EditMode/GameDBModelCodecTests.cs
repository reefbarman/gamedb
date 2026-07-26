using GameDBEditorLibrary;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBModelCodecTests
    {
        [Test]
        public void SerializeAndImport_PreserveCanonicalRevisionAndLoadedPath()
        {
            var source = CreateRepresentativeDatabase();

            var serialized = GameDBModelCodec.Serialize(source);
            var imported = GameDBModelCodec.Import(serialized.DataJson, serialized.SchemaJson, source.LoadedPath);

            Assert.That(serialized.Revision, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(GameDBModelCodec.ComputeRevision(imported), Is.EqualTo(serialized.Revision));
            Assert.That(imported.LoadedPath, Is.EqualTo(source.LoadedPath));
            Assert.That(imported.ScopeName, Is.EqualTo(source.ScopeName));
            Assert.That(imported.LocalizationDB, Is.EqualTo(source.LocalizationDB));
        }

        [Test]
        public void Serialize_WritesCurrentSchemaFormatVersion()
        {
            var serialized = GameDBModelCodec.Serialize(CreateRepresentativeDatabase());
            var schema = (IDictionary<string, object>)JsonSerialization.Deserialize(serialized.SchemaJson);

            Assert.That(schema["formatVersion"], Is.EqualTo((long)GameDBSchemaFormat.CurrentVersion));
        }

        [TestCase("{\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":null,\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":\"1\",\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":1.5,\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":0,\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":-1,\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        [TestCase("{\"formatVersion\":2147483648,\"tables\":{},\"scope\":\"Test\",\"localizationDB\":false}")]
        public void Import_RejectsMissingOrMalformedSchemaFormatVersion(string schemaJson)
        {
            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBModelCodec.Import("{\"tables\":{}}", schemaJson));

            Assert.That(exception.SupportedVersion, Is.EqualTo(GameDBSchemaFormat.CurrentVersion));
            Assert.That(exception.Message, Does.Contain("formatVersion"));
        }

        [Test]
        public void Import_RejectsNewerFormatBeforeHydratingSchema()
        {
            const string schemaJson = "{\"formatVersion\":2,\"tables\":\"invalid\",\"scope\":\"Test\",\"localizationDB\":false}";

            var exception = Assert.Throws<GameDBSchemaFormatException>(() =>
                GameDBModelCodec.Import("{\"tables\":{}}", schemaJson));

            Assert.That(exception.FoundVersion, Is.EqualTo(2));
            Assert.That(exception.SupportedVersion, Is.EqualTo(1));
            Assert.That(exception.Message, Does.Contain("newer").And.Contain("version 2").And.Contain("version 1"));
            Assert.That(exception.Message, Does.Contain("newer GameDB package"));
        }

        [Test]
        public void Import_CurrentFormatVersionRoundTripsCanonically()
        {
            var serialized = GameDBModelCodec.Serialize(CreateRepresentativeDatabase());
            var imported = GameDBModelCodec.Import(serialized.DataJson, serialized.SchemaJson);
            var roundTripped = GameDBModelCodec.Serialize(imported);

            Assert.That(roundTripped.SchemaJson, Is.EqualTo(serialized.SchemaJson));
            Assert.That(roundTripped.DataJson, Is.EqualTo(serialized.DataJson));
            Assert.That(roundTripped.Revision, Is.EqualTo(serialized.Revision));
        }

        [Test]
        public void CreateDetachedModel_ReturnsIndependentMutableGraph()
        {
            var source = CreateRepresentativeDatabase();
            var sourceRevision = GameDBModelCodec.ComputeRevision(source);

            var detached = GameDBModelCodec.CreateDetachedModel(source);
            var detachedItems = (TableModel)detached.Tables["Items"];
            Assert.That(detachedItems.SetValue("Sword", "Tint", "#FFFFFF"), Is.True);
            Assert.That(detachedItems.SetValue("Sword", "Offset", "9,10"), Is.True);

            Assert.That(GameDBModelCodec.ComputeRevision(source), Is.EqualTo(sourceRevision));
            var sourceItems = (TableModel)source.Tables["Items"];
            Assert.That(((Color)sourceItems.Data["Sword"].Data["Tint"]).Hex, Is.EqualTo("#10203040"));
            Assert.That(((Vector2)sourceItems.Data["Sword"].Data["Offset"]).x, Is.EqualTo(1f));
        }

        [Test]
        public void CreateSnapshot_OrdersMembersAndDetachesNestedValues()
        {
            var source = CreateRepresentativeDatabase();
            var sourceRevision = GameDBModelCodec.ComputeRevision(source);

            var snapshot = GameDBModelCodec.CreateSnapshot("Assets/Test/database.json", "Assets/Test/database.schema.json", source);
            var items = snapshot.Tables.Single(table => table.Name == "Items");
            var sword = items.Rows.Single(row => row.Key == "Sword");
            var tags = (List<object>)sword.Values["Tags"];
            var attributes = (Dictionary<object, object>)sword.Values["Attributes"];
            var tint = (Color)sword.Values["Tint"];
            var offset = (Vector2)sword.Values["Offset"];

            tags.Add("mutated");
            attributes["Power"] = 99L;
            tint.r = 255;
            offset.x = 99f;

            Assert.That(snapshot.Tables.Select(table => table.Name), Is.EqualTo(new[] { "Alpha", "Items" }));
            Assert.That(items.Fields.Select(field => field.Name),
                Is.EqualTo(new[] { "Attributes", "Offset", "Tags", "Tint" }));
            Assert.That(items.Rows.Select(row => row.Key), Is.EqualTo(new[] { "Shield", "Sword" }));
            Assert.That(GameDBModelCodec.ComputeRevision(source), Is.EqualTo(sourceRevision));

            var sourceRow = source.Tables["Items"].Data["Sword"].Data;
            Assert.That((IEnumerable<object>)sourceRow["Tags"], Is.EqualTo(new object[] { "melee" }));
            Assert.That(((Dictionary<object, object>)sourceRow["Attributes"])["Power"], Is.EqualTo(12L));
            Assert.That(((Color)sourceRow["Tint"]).Hex, Is.EqualTo("#10203040"));
            Assert.That(((Vector2)sourceRow["Offset"]).x, Is.EqualTo(1f));
        }

        [Test]
        public void DetachValue_RejectsUnknownMutableTypes()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => GameDBModelCodec.DetachValue(new UnknownMutableValue()));

            Assert.That(exception.Message, Does.Contain(typeof(UnknownMutableValue).FullName));
        }

        private static GameDB CreateRepresentativeDatabase()
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory("CodecTests/database.json");
            gameDB.ScopeName = "CodecTests";
            gameDB.LocalizationDB = true;

            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Alpha", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Tint", FieldType.color, false), Is.True);
            Assert.That(items.AddField("Tags", FieldType.@string, true), Is.True);
            Assert.That(items.AddField("Offset", FieldType.vector2, false), Is.True);
            Assert.That(items.AddField("Attributes", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.@int, null)), Is.True);
            Assert.That(items.AddKey("Sword"), Is.True);
            Assert.That(items.AddKey("Shield"), Is.True);
            Assert.That(items.SetValue("Sword", "Tint", "#10203040"), Is.True);
            Assert.That(items.SetValue("Sword", "Tags", new List<object> { "melee" }), Is.True);
            Assert.That(items.SetValue("Sword", "Offset", "1,2"), Is.True);
            Assert.That(items.SetValue("Sword", "Attributes",
                new Dictionary<string, object> { { "Power", 12L } }), Is.True);
            return gameDB;
        }

        private sealed class UnknownMutableValue
        {
            internal int Value { get; set; }
        }
    }
}
