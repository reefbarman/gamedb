using System;

namespace GameDBLibrary
{
    public class FloatAccessor : DataAccessor<float>
    {
        private readonly float m_value;

        public FloatAccessor(object value)
        {
            m_value = Convert.ToSingle(value);
        }

        public override float GetValue()
        {
            return m_value;
        }
    }
}