using GameDBLibrary;
using Vector3 = UnityEngine.Vector3;

namespace GameDBLibraryUnity
{
    public class Vector3Accessor : DataAccessor<Vector3>
    {
        private readonly Vector3 m_value;

        public Vector3Accessor(object value)
        {
            m_value = ((GameDBLibrary.Vector3)value).ToUnityVector();
        }

        public override Vector3 GetValue()
        {
            return m_value;
        }
    }
}