using NUnit.Framework;

namespace GameDBLibrary.Tests
{
    public class JsonPatchTests
    {
        [Test]
        public void Patch_AppliesAddReplaceAndRemoveOperations()
        {
            const string original = "{\"name\":\"base\",\"values\":[1,2],\"obsolete\":true}";
            const string patch = "["
                + "{\"op\":\"replace\",\"path\":\"/name\",\"value\":\"updated\"},"
                + "{\"op\":\"add\",\"path\":\"/values/-\",\"value\":3},"
                + "{\"op\":\"remove\",\"path\":\"/obsolete\"}"
                + "]";

            var result = new JsonPatch().Patch(original, patch);

            Assert.That(result, Is.EqualTo("{\"name\":\"updated\",\"values\":[1,2,3]}"));
        }

        [Test]
        public void Patch_TestTreatsEquivalentJsonNumbersAsEqual()
        {
            const string original = "{\"value\":1}";
            const string patch = "[{\"op\":\"test\",\"path\":\"/value\",\"value\":1.0}]";

            Assert.That(new JsonPatch().Patch(original, patch), Is.EqualTo(original));
        }
    }
}
