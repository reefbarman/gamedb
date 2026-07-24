using System;

namespace GameDBLibrary
{
    public class IntAccessor : DataAccessor<int>
    {
        private readonly int m_value;

        public IntAccessor(object value)
        {
            m_value = Convert.ToInt32(value);
        }

        public override int GetValue()
        {
            return m_value;
        }
    }
}