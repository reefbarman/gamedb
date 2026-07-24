namespace GameDBLibrary
{
    public class Vector3Accessor : DataAccessor<Vector3>
    {
        private readonly Vector3 m_value;

        public Vector3Accessor(object value)
        {
            m_value = (Vector3)value;
        }

        public override Vector3 GetValue()
        {
            return m_value;
        }
    }
}