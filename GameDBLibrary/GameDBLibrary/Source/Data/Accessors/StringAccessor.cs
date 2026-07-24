namespace GameDBLibrary
{
    public class StringAccessor : DataAccessor<string>
    {
        private readonly string m_value;

        public StringAccessor(object value)
        {
            m_value = value as string;
        }

        public override string GetValue()
        {
            return m_value;
        }
    }
}