namespace GameDBHelpers
{
    public static class TypeHelpers
    {
        public static UnityEngine.Color ToUnityColor(this GameDBLibrary.Color color)
        {
            return new UnityEngine.Color32(color.r, color.g, color.b, color.a);
        }

        public static UnityEngine.Vector2 ToUnityVector(this GameDBLibrary.Vector2 vec)
        {
            return new UnityEngine.Vector2(vec.x, vec.y);
        }

        public static UnityEngine.Vector3 ToUnityVector(this GameDBLibrary.Vector3 vec)
        {
            return new UnityEngine.Vector3(vec.x, vec.y, vec.z);
        }

        public static UnityEngine.Vector4 ToUnityVector(this GameDBLibrary.Vector4 vec)
        {
            return new UnityEngine.Vector4(vec.x, vec.y, vec.z, vec.w);
        }
    }
}