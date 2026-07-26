using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBNumericTypeTests
    {
        [Test]
        public void NumericTypes_ExposeExpectedSystemTypesAndDefaults()
        {
            var longField = new FieldBase("LongValue", FieldType.@long, false);
            var longArray = new FieldBase("LongValues", FieldType.@long, true);
            var doubleField = new FieldBase("DoubleValue", FieldType.@double, false);
            var doubleArray = new FieldBase("DoubleValues", FieldType.@double, true);

            Assert.That(longField.GetSystemType(), Is.EqualTo(typeof(long)));
            Assert.That(longField.GetDefaultValue(), Is.TypeOf<long>().And.EqualTo(0L));
            Assert.That(longArray.GetDefaultValue(), Is.TypeOf<List<long>>().And.Empty);
            Assert.That(doubleField.GetSystemType(), Is.EqualTo(typeof(double)));
            Assert.That(doubleField.GetDefaultValue(), Is.TypeOf<double>().And.EqualTo(0d));
            Assert.That(doubleArray.GetDefaultValue(), Is.TypeOf<List<double>>().And.Empty);
        }

        [Test]
        public void LongField_AcceptsSignedInt64RangeAndRejectsNonIntegralInputs()
        {
            var field = new FieldBase("Value", FieldType.@long, false);

            Assert.That(field.IsValueValid(long.MinValue), Is.True);
            Assert.That(field.IsValueValid(long.MaxValue), Is.True);
            Assert.That(field.IsValueValid(42), Is.True);
            Assert.That(field.IsValueValid((ulong)long.MaxValue), Is.True);
            Assert.That(field.IsValueValid((ulong)long.MaxValue + 1UL), Is.False);
            Assert.That(field.IsValueValid(1d), Is.False);
            Assert.That(field.IsValueValid(1.5m), Is.False);
            Assert.That(field.IsValueValid("1"), Is.False);
        }

        [Test]
        public void FloatField_AcceptsFiniteSingleValuesAndNormalizesNegativeZero()
        {
            var field = new FieldBase("Value", FieldType.@float, false);

            Assert.That(field.IsValueValid(float.MaxValue), Is.True);
            Assert.That(field.IsValueValid(float.NaN), Is.False);
            Assert.That(field.IsValueValid(float.PositiveInfinity), Is.False);
            Assert.That(field.IsValueValid(float.NegativeInfinity), Is.False);
            Assert.That(NumericValue.TryNormalizeSingle(-0f, out var zero), Is.True);
            Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(zero), 0), Is.EqualTo(0));
        }

        [Test]
        public void DoubleField_AcceptsFiniteNumbersAndRejectsNonFiniteValues()
        {
            var field = new FieldBase("Value", FieldType.@double, false);

            Assert.That(field.IsValueValid(double.MaxValue), Is.True);
            Assert.That(field.IsValueValid(double.Epsilon), Is.True);
            Assert.That(field.IsValueValid(42L), Is.True);
            Assert.That(field.IsValueValid(double.NaN), Is.False);
            Assert.That(field.IsValueValid(double.PositiveInfinity), Is.False);
            Assert.That(field.IsValueValid(double.NegativeInfinity), Is.False);
        }

        [Test]
        public void NumericArrays_ValidateEveryValueAndNormalizeClrTypes()
        {
            var longField = new FieldBase("LongValues", FieldType.@long, true);
            var doubleField = new FieldBase("DoubleValues", FieldType.@double, true);
            var longWire = new List<object> { int.MinValue, long.MaxValue };
            var doubleWire = new List<object> { 1f, -0d, double.Epsilon };

            Assert.That(longField.IsValueValid(longWire), Is.True);
            Assert.That(longField.IsValueValid(new List<object> { 1L, 1d }), Is.False);
            Assert.That(doubleField.IsValueValid(doubleWire), Is.True);
            Assert.That(doubleField.IsValueValid(new List<object> { 1d, double.NaN }), Is.False);

            var longs = (List<object>)TypeUtils.DeserializeValue(
                FieldType.@long, true, null, longWire);
            var doubles = (List<object>)TypeUtils.DeserializeValue(
                FieldType.@double, true, null, doubleWire);

            Assert.That(longs, Is.All.TypeOf<long>());
            Assert.That(longs, Is.EqualTo(new object[] { (long)int.MinValue, long.MaxValue }));
            Assert.That(doubles, Is.All.TypeOf<double>());
            Assert.That(BitConverter.DoubleToInt64Bits((double)doubles[1]), Is.EqualTo(0L));
        }

        [Test]
        public void NumericDictionaries_ValidateAndNormalizeEveryValue()
        {
            var longType = new DictionaryType(KeyType.@string, null, FieldType.@long, null);
            var doubleType = new DictionaryType(KeyType.@string, null, FieldType.@double, null);
            var longWire = new Dictionary<string, object>
            {
                { "minimum", long.MinValue },
                { "maximum", (ulong)long.MaxValue }
            };
            var doubleWire = new Dictionary<string, object>
            {
                { "precise", 1.0000000000000002d },
                { "zero", -0d }
            };

            Assert.That(longType.IsValueValid(longWire), Is.True);
            Assert.That(longType.IsValueValid(new Dictionary<string, object> { { "bad", 1d } }), Is.False);
            Assert.That(doubleType.IsValueValid(doubleWire), Is.True);
            Assert.That(doubleType.IsValueValid(new Dictionary<string, object>
                { { "bad", double.PositiveInfinity } }), Is.False);

            var longs = (Dictionary<object, object>)longType.DeserializeValue(longWire);
            var doubles = (Dictionary<object, object>)doubleType.DeserializeValue(doubleWire);
            Assert.That(longs.Values, Is.All.TypeOf<long>());
            Assert.That(doubles.Values, Is.All.TypeOf<double>());
            Assert.That(BitConverter.DoubleToInt64Bits((double)doubles["zero"]), Is.EqualTo(0L));
        }

        [Test]
        public void NumericAccessors_NormalizeValuesAndRejectInvalidInputs()
        {
            Assert.That(new LongAccessor(uint.MaxValue).GetValue(), Is.EqualTo((long)uint.MaxValue));
            Assert.That(new DoubleAccessor(-0d).GetValue(), Is.EqualTo(0d));
            Assert.That(BitConverter.DoubleToInt64Bits(new DoubleAccessor(-0d).GetValue()), Is.EqualTo(0L));
            Assert.Throws<FormatException>(() => new LongAccessor(1d));
            Assert.Throws<FormatException>(() => new DoubleAccessor(double.NaN));
        }

        [Test]
        public void JsonSerialization_RoundTripsInt64AndDoubleExactly()
        {
            var source = new Dictionary<string, object>
            {
                { "minimumLong", long.MinValue },
                { "maximumLong", long.MaxValue },
                { "maximumDouble", double.MaxValue },
                { "subnormal", double.Epsilon },
                { "precise", 1.0000000000000002d },
                { "zero", -0d }
            };

            var result = (IDictionary<string, object>)JsonSerialization.Deserialize(
                JsonSerialization.Serialize(source));

            Assert.That(result["minimumLong"], Is.TypeOf<long>().And.EqualTo(long.MinValue));
            Assert.That(result["maximumLong"], Is.TypeOf<long>().And.EqualTo(long.MaxValue));
            AssertDoubleBits(result["maximumDouble"], double.MaxValue);
            AssertDoubleBits(result["subnormal"], double.Epsilon);
            AssertDoubleBits(result["precise"], 1.0000000000000002d);
            Assert.That(BitConverter.DoubleToInt64Bits((double)result["zero"]), Is.EqualTo(0L));
        }

        [Test]
        public void JsonSerialization_RejectsNonFiniteValuesRecursively()
        {
            var value = new Dictionary<string, object>
            {
                { "items", new List<object> { 1d, double.PositiveInfinity } }
            };

            Assert.Throws<FormatException>(() => JsonSerialization.Serialize(value));
            Assert.That(() => JsonSerialization.Deserialize("1e10000"), Throws.Exception);
        }

        [Test]
        public void JsonPatch_ComparesLargeAndMixedNumbersWithoutPrecisionCollapse()
        {
            const string adjacentOriginal = "{\"value\":9007199254740993}";
            const string adjacentPatch = "[{\"op\":\"test\",\"path\":\"/value\",\"value\":9007199254740992}]";
            const string mixedOriginal = "{\"value\":9007199254740992}";
            const string mixedPatch = "[{\"op\":\"test\",\"path\":\"/value\",\"value\":9007199254740992.0}]";
            const string largeDoubleOriginal = "{\"value\":1e100}";
            const string largeDoublePatch = "[{\"op\":\"test\",\"path\":\"/value\",\"value\":1e100}]";

            Assert.Throws<ArgumentException>(() =>
                new JsonPatch().Patch(adjacentOriginal, adjacentPatch));
            Assert.That(new JsonPatch().Patch(mixedOriginal, mixedPatch), Is.EqualTo(mixedOriginal));
            var largeDoubleResult = (IDictionary<string, object>)JsonSerialization.Deserialize(
                new JsonPatch().Patch(largeDoubleOriginal, largeDoublePatch));
            AssertDoubleBits(largeDoubleResult["value"], 1e100d);
        }

        [Test]
        public void DictionarySupportedTypes_MapEveryNonDictionaryFieldExactlyOnce()
        {
            var expected = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                .Where(type => type != FieldType.dictionary).ToArray();
            var actual = DictionaryType.GetSupportedFieldTypes();

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Distinct().Count(), Is.EqualTo(actual.Length));
            Assert.That(DictionaryType.GetSupportedTypes().Length, Is.EqualTo(actual.Length));
            Assert.That(actual, Does.Contain(FieldType.@long));
            Assert.That(actual, Does.Contain(FieldType.@double));
        }

        private static void AssertDoubleBits(object actual, double expected)
        {
            Assert.That(actual, Is.TypeOf<double>());
            Assert.That(BitConverter.DoubleToInt64Bits((double)actual),
                Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)));
        }
    }
}
