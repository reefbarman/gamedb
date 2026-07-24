using GameDBLibrary;
using Color = UnityEngine.Color;

namespace GameDBLibraryUnity
{
    public class ColorAccessor : DataAccessor<Color>
    {
        private readonly Color m_value;

        public ColorAccessor(object value)
        {
            m_value = ((GameDBLibrary.Color)value).ToUnityColor();
        }

        public override Color GetValue()
        {
            return m_value;
        }
    }
}