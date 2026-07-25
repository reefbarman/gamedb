using GameDBLibrary;
using Vector4 = UnityEngine.Vector4;

namespace GameDBLibraryUnity
{
    public class Vector4Accessor : DataAccessor<Vector4>
    {
        private readonly Vector4 m_value;

        public Vector4Accessor(object value)
        {
            m_value = ((GameDBLibrary.Vector4)value).ToUnityVector();
        }

        public override Vector4 GetValue()
        {
            return m_value;
        }
    }
}