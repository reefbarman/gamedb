using System;

namespace GameDBLibrary
{
    public class LongAccessor : DataAccessor<long>
    {
        private readonly long m_value;

        public LongAccessor(object value)
        {
            if (!NumericValue.TryNormalizeInt64(value, out var normalized))
            {
                throw new FormatException("Value is not a valid Int64.");
            }

            m_value = normalized;
        }

        public override long GetValue()
        {
            return m_value;
        }
    }
}
