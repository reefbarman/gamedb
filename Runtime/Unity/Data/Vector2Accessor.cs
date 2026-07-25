using GameDBLibrary;
using Vector2 = UnityEngine.Vector2;

namespace GameDBLibraryUnity
{
    public class Vector2Accessor : DataAccessor<Vector2>
    {
        private readonly Vector2 m_value;

        public Vector2Accessor(object value)
        {
            m_value = ((GameDBLibrary.Vector2)value).ToUnityVector();
        }

        public override Vector2 GetValue()
        {
            return m_value;
        }
    }
}