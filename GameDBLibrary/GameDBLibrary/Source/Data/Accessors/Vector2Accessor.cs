namespace GameDBLibrary
{
    public class Vector2Accessor : DataAccessor<Vector2>
    {
        private readonly Vector2 m_value;

        public Vector2Accessor(object value)
        {
            m_value = (Vector2) value;
        }

        public override Vector2 GetValue()
        {
            return m_value;
        }
    }
}