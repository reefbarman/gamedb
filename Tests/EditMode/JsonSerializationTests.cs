using NUnit.Framework;
using System.Collections.Generic;

namespace GameDBLibrary.Tests
{
    public class JsonSerializationTests
    {
        [Test]
        public void Deserialize_ReturnsPlainCollectionsAndInt64Numbers()
        {
            var result = (IDictionary<string, object>)JsonSerialization.Deserialize("{\"value\":2147483648,\"items\":[true,2.5]}");

            Assert.That(result["value"], Is.TypeOf<long>());
            Assert.That(result["value"], Is.EqualTo(2147483648L));
            Assert.That(result["items"], Is.TypeOf<List<object>>());
        }

        [Test]
        public void Deserialize_LeavesDateLikeValuesAsStrings()
        {
            var result = (IDictionary<string, object>)JsonSerialization.Deserialize("{\"value\":\"2026-07-24T12:34:56Z\"}");

            Assert.That(result["value"], Is.TypeOf<string>());
            Assert.That(result["value"], Is.EqualTo("2026-07-24T12:34:56Z"));
        }

        [Test]
        public void Deserialize_RejectsMalformedJson()
        {
            Assert.That(() => JsonSerialization.Deserialize("{\"value\":"), Throws.Exception);
        }

        [Test]
        public void Deserialize_RejectsDuplicateProperties()
        {
            Assert.That(() => JsonSerialization.Deserialize("{\"value\":1,\"value\":2}"), Throws.Exception);
        }

        [Test]
        public void Serialize_PreservesDictionaryInsertionOrder()
        {
            var value = new Dictionary<string, object>
            {
                { "second", 2 },
                { "first", 1 }
            };

            Assert.That(JsonSerialization.Serialize(value), Is.EqualTo("{\"second\":2,\"first\":1}"));
        }
    }
}
