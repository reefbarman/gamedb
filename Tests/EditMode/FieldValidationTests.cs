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
    }
}
