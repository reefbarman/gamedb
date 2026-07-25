namespace GameDBLibrary
{
    public class EnumAccessor<T> : DataAccessor<T>
    {
        private T m_value;

        public EnumAccessor(object value)
        {
            m_value = (T)value;
        }

        public override T GetValue()
        {
            return m_value;
        }
    }
}