namespace GameDBLibrary
{
    public class UnityObjectAccessor : DataAccessor<string>
    {
        private readonly string m_path;

        public UnityObjectAccessor(object val)
        {
            m_path = val as string;
        }

        public override string GetValue()
        {
            return m_path;
        }
    }
}