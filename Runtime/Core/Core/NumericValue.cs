using System;
using System.Globalization;

namespace GameDBLibrary
{
    internal static class NumericValue
    {
        internal static bool TryNormalizeInt32(object value, out int result)
        {
            result = 0;
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                var converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (converted < int.MinValue || converted > int.MaxValue
                    || Convert.ToDecimal(value, CultureInfo.InvariantCulture) != converted)
                {
                    return false;
                }

                result = (int)converted;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool TryNormalizeInt64(object value, out long result)
        {
            switch (value)
            {
                case sbyte signedByte:
                    result = signedByte;
                    return true;
                case byte unsignedByte:
                    result = unsignedByte;
                    return true;
                case short signedShort:
                    result = signedShort;
                    return true;
                case ushort unsignedShort:
                    result = unsignedShort;
                    return true;
                case int signedInteger:
                    result = signedInteger;
                    return true;
                case uint unsignedInteger:
                    result = unsignedInteger;
                    return true;
                case long signedLong:
                    result = signedLong;
                    return true;
                case ulong unsignedLong when unsignedLong <= long.MaxValue:
                    result = (long)unsignedLong;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        internal static bool TryNormalizeSingle(object value, out float result)
        {
            result = 0f;
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                if (float.IsNaN(result) || float.IsInfinity(result))
                {
                    result = 0f;
                    return false;
                }

                if (result == 0f)
                {
                    result = 0f;
                }

                return true;
            }
            catch (Exception)
            {
                result = 0f;
                return false;
            }
        }

        internal static bool TryNormalizeDouble(object value, out double result)
        {
            result = 0d;
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(result) || double.IsInfinity(result))
                {
                    result = 0d;
                    return false;
                }

                if (result == 0d)
                {
                    result = 0d;
                }

                return true;
            }
            catch (Exception)
            {
                result = 0d;
                return false;
            }
        }

        internal static bool JsonNumbersEqual(object left, object right)
        {
            var leftIsInteger = TryNormalizeInt64(left, out var leftInteger);
            var rightIsInteger = TryNormalizeInt64(right, out var rightInteger);
            if (leftIsInteger && rightIsInteger)
            {
                return leftInteger == rightInteger;
            }

            if (!TryNormalizeDouble(left, out var leftDouble)
                || !TryNormalizeDouble(right, out var rightDouble))
            {
                return false;
            }

            if (leftIsInteger)
            {
                return IntegerEqualsDouble(leftInteger, rightDouble);
            }

            if (rightIsInteger)
            {
                return IntegerEqualsDouble(rightInteger, leftDouble);
            }

            return leftDouble.Equals(rightDouble);
        }

        internal static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static bool IntegerEqualsDouble(long integer, double number)
        {
            if (Math.Truncate(number) != number)
            {
                return false;
            }

            try
            {
                var converted = checked((long)number);
                return converted == integer && (double)converted == number;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
