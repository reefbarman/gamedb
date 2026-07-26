using UnityEngine;

namespace GameDBLibraryUnity
{
    public class UnityObjectAccessor : GameDBLibrary.UnityObjectAccessor
    {
        public UnityObjectAccessor(object value)
            : base(value)
        {
        }

        public Object GetObject()
        {
            var path = GetResourcesPath();
            return path == null ? null : Resources.Load(path, typeof(Object));
        }
    }
}
