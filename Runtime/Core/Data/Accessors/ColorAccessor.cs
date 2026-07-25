namespace GameDBLibrary
{
    public class ColorAccessor : DataAccessor<Color>
    {
        private readonly Color m_value;

        public ColorAccessor(object value)
        {
            m_value = (Color)value;
        }

        public override Color GetValue()
        {
            return m_value;
        }
    }
}