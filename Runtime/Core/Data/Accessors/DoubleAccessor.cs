using System;

namespace GameDBLibrary
{
    public class DoubleAccessor : DataAccessor<double>
    {
        private readonly double m_value;

        public DoubleAccessor(object value)
        {
            if (!NumericValue.TryNormalizeDouble(value, out var normalized))
            {
                throw new FormatException("Value is not a finite Double.");
            }

            m_value = normalized;
        }

        public override double GetValue()
        {
            return m_value;
        }
    }
}
