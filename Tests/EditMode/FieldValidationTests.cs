using NUnit.Framework;
using System.Collections.Generic;

namespace GameDBLibrary.Tests
{
    public class FieldValidationTests
    {
        [Test]
        public void IntegerField_AcceptsInRangeInt64Value()
        {
            var field = new FieldBase("Value", FieldType.@int, false);

            Assert.That(field.IsValueValid(42L), Is.True);
        }

        [TestCase(2147483648L)]
        [TestCase(1.5)]
        [TestCase("1")]
        public void IntegerField_RejectsValuesOutsideInt32Contract(object value)
        {
            var field = new FieldBase("Value", FieldType.@int, false);

            Assert.That(field.IsValueValid(value), Is.False);
        }

        [Test]
        public void DictionaryType_DeserializesModernDictionaryShape()
        {
            var dictionaryType = new DictionaryType(KeyType.@string, null, FieldType.@int, null);
            var source = new Dictionary<string, object>
            {
                { "answer", 42L }
            };

            var result = (Dictionary<object, object>)dictionaryType.DeserializeValue(source);

            Assert.That(result["answer"], Is.EqualTo(42L));
        }

        [Test]
        public void UnityObjectField_AcceptsCanonicalAndEmptyWireValues()
        {
            var field = new FieldBase("Icon", FieldType.unityObject, false);

            Assert.That(field.IsValueValid(ReferenceWire(CanonicalGuid, CanonicalPath)), Is.True);
            Assert.That(field.IsValueValid(ReferenceWire(CanonicalGuid,
                "Assets/Game/Sword.asset")), Is.True);
            Assert.That(field.IsValueValid(ReferenceWire(CanonicalGuid,
                "Assets/Game/resources/Sword.asset")), Is.True);
            Assert.That(field.IsValueValid(ReferenceWire(CanonicalGuid,
                "Assets/Resources/Nested/Resources/Sword.asset")), Is.True);
            Assert.That(field.IsValueValid(ReferenceWire(string.Empty, string.Empty)), Is.True);
        }

        [Test]
        public void UnityObjectField_RejectsNonCanonicalWireValues()
        {
            var field = new FieldBase("Icon", FieldType.unityObject, false);
            var invalidValues = new object[]
            {
                null,
                CanonicalPath,
                new Dictionary<string, object> { { "path", CanonicalPath } },
                new Dictionary<string, object> { { "guid", CanonicalGuid } },
                new Dictionary<string, object>
                {
                    { "guid", CanonicalGuid },
                    { "path", CanonicalPath },
                    { "extra", true }
                },
                new Dictionary<string, object>
                {
                    { "Guid", CanonicalGuid },
                    { "Path", CanonicalPath }
                },
                ReferenceWire(42L, CanonicalPath),
                ReferenceWire(CanonicalGuid, 42L),
                ReferenceWire(CanonicalGuid, string.Empty),
                ReferenceWire(string.Empty, CanonicalPath),
                ReferenceWire(CanonicalGuid.ToUpperInvariant(), CanonicalPath),
                ReferenceWire("not-a-guid", CanonicalPath),
                ReferenceWire(CanonicalGuid, "Packages/Game/Resources/Sword.asset"),
                ReferenceWire(CanonicalGuid, "Assets/Game\\Resources\\Sword.asset"),
                ReferenceWire(CanonicalGuid, "Assets/Game/Resources"),
                ReferenceWire(CanonicalGuid, "Assets/Game/Resources/Sword")
            };

            foreach (var invalidValue in invalidValues)
            {
                Assert.That(field.IsValueValid(invalidValue), Is.False,
                    $"Unexpected valid value: {invalidValue}");
                Assert.That(UnityObjectReferenceWire.TryParse(invalidValue, out _), Is.False);
            }
        }

        [Test]
        public void UnityObjectArray_ValidatesEveryElement()
        {
            var field = new FieldBase("Icons", FieldType.unityObject, true);
            var valid = ReferenceWire(CanonicalGuid, CanonicalPath);
            var empty = ReferenceWire(string.Empty, string.Empty);

            Assert.That(field.IsValueValid(new List<object> { valid, empty }), Is.True);
            Assert.That(field.IsValueValid(new List<object> { valid, CanonicalPath }), Is.False);
            Assert.That(field.IsValueValid(new[] { valid, empty }), Is.False);
        }

        [Test]
        public void UnityObjectDictionary_ValidatesAndDeserializesEveryValue()
        {
            var dictionaryType = new DictionaryType(
                KeyType.@string, null, FieldType.unityObject, null);
            var valid = new Dictionary<string, object>
            {
                { "primary", ReferenceWire(CanonicalGuid, CanonicalPath) },
                { "empty", ReferenceWire(string.Empty, string.Empty) }
            };
            var invalid = new Dictionary<string, object>
            {
                { "primary", ReferenceWire(CanonicalGuid, CanonicalPath) },
                { "secondary", CanonicalPath }
            };

            Assert.That(dictionaryType.IsValueValid(valid), Is.True);
            Assert.That(dictionaryType.IsValueValid(invalid), Is.False);

            var result = (Dictionary<object, object>)dictionaryType.DeserializeValue(valid);
            Assert.That(result["primary"],
                Is.EqualTo(new UnityObjectReference(CanonicalGuid, CanonicalPath)));
            Assert.That(result["empty"], Is.SameAs(UnityObjectReference.Empty));
        }

        [Test]
        public void UnityObjectDefaults_UseCanonicalReferenceTypes()
        {
            var scalar = new FieldBase("Icon", FieldType.unityObject, false);
            var array = new FieldBase("Icons", FieldType.unityObject, true);

            Assert.That(scalar.GetDefaultValue(), Is.SameAs(UnityObjectReference.Empty));
            Assert.That(array.GetDefaultValue(), Is.TypeOf<List<UnityObjectReference>>());
            Assert.That((List<UnityObjectReference>)array.GetDefaultValue(), Is.Empty);
        }

        private const string CanonicalGuid = "0123456789abcdef0123456789abcdef";
        private const string CanonicalPath = "Assets/Game/Resources/Items/Sword.asset";

        private static Dictionary<string, object> ReferenceWire(object guid, object path)
        {
            return new Dictionary<string, object>
            {
                { "guid", guid },
                { "path", path }
            };
        }
    }
}
