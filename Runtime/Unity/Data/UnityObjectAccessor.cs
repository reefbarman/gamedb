using GameDBLibrary;
using UnityEngine;


namespace GameDBLibraryUnity
{
    public class UnityObjectAccessor : DataAccessor<string>
    {
        private readonly string m_path;

        public UnityObjectAccessor(object val)
        {
            m_path = val as string;
        }

        public override string GetValue()
        {
            return m_path;
        }

        public Object GetObject()
        {
            var path = GetValue();

            return Resources.Load(path.Substring(path.IndexOf("Resources") + 10, path.LastIndexOf(".") - (path.IndexOf("Resources") + 10)), typeof(Object));
        }
    }
}
