using GameDBLibrary.UnitTests.MiniJSON;
using GameDBLibrary.UnitTests.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace GameDBLibrary.UnitTests {
    public class JsonPatchTests {

        private readonly ITestOutputHelper _output;

        public JsonPatchTests(ITestOutputHelper output) {
            _output = output;
        }

        [Fact]
        public void MainTests() {
            RunTest(Encoding.UTF8.GetString(Resources.mainTest));
        }

        [Fact]
        public void SpecTests() {
            RunTest(Encoding.UTF8.GetString(Resources.specTest));
        }

        private void RunTest(string testJson) {
            var testsObj = Json.Deserialize(testJson);

            var tests = testsObj as List<object>;
            
            for (var i = 0; i < tests.Count; i++) {
                var test = tests[i] as IDictionary<string, object>;

                var comment = test.ContainsKey("comment") ? Json.Serialize(test["comment"]) : string.Empty;
                _output.WriteLine($"Running test {i + 1} of {tests.Count}: {comment}");

                var patcher = new JsonPatch();

                var error = false;
                var patchedJson = string.Empty;

                try {
                    patchedJson = patcher.Patch(Json.Serialize(test["doc"]), Json.Serialize(test["patch"]));
                    _output.WriteLine("Test Output:");
                    _output.WriteLine(patchedJson);
                }
                catch (Exception e) {
                    _output.WriteLine("Test Output:");
                    _output.WriteLine($"error: {e.Message}");
                    error = true;
                }

                _output.WriteLine("Test Completed");
                if (test.ContainsKey("error")) {
                    _output.WriteLine($"Expected error: {Json.Serialize(test["error"])}");
                    Assert.True(error);
                }
                else if (test.ContainsKey("expected")) {
                    _output.WriteLine($"Expected: {Json.Serialize(test["expected"])}");
                    Assert.True(CalculateAsciiCost(patchedJson) == CalculateAsciiCost(Json.Serialize(test["expected"])));
                }
                else if (!test.ContainsKey("error") && !test.ContainsKey("expected")) {
                    Assert.False(error);
                }
                else {
                    var expectedOutput = test.ContainsKey("expected") ? Json.Serialize(test["expected"]) : (test.ContainsKey("error") ? Json.Serialize(test["error"]) : string.Empty);

                    _output.WriteLine($"Test Failed - expected: {expectedOutput}");
                    Assert.True(false);
                }
            }
        }

        private static int CalculateAsciiCost(string stringToCost) {
            byte[] ascii = Encoding.ASCII.GetBytes(stringToCost);
            return ascii.Aggregate(0, (a, b) => (int)a + (int)b);
        }
    }
}
