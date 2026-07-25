namespace GameDBLibrary
{
    public class Vector4Accessor : DataAccessor<Vector4>
    {
        private readonly Vector4 m_value;

        public Vector4Accessor(object value)
        {
            m_value = (Vector4)value;
        }

        public override Vector4 GetValue()
        {
            return m_value;
        }
    }
}