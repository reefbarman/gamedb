using System;

namespace GameDBLibrary
{
    public class BoolAccessor : DataAccessor<bool>
    {
        private readonly bool m_value;

        public BoolAccessor(object value)
        {
            m_value = Convert.ToBoolean(value);
        }

        public override bool GetValue()
        {
            return m_value;
        }
    }
}