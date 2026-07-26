using System;

namespace GameDBLibrary
{
    public class UnityObjectAccessor : DataAccessor<UnityObjectReference>
    {
        private readonly UnityObjectReference m_reference;

        public UnityObjectAccessor(object value)
        {
            m_reference = value as UnityObjectReference
                ?? throw new ArgumentException("Unity object accessor requires a UnityObjectReference.", nameof(value));
        }

        public override UnityObjectReference GetValue()
        {
            return m_reference;
        }

        public string GetGuid()
        {
            return m_reference.Guid;
        }

        public string GetPath()
        {
            return m_reference.Path;
        }

        protected string GetResourcesPath()
        {
            if (m_reference.IsEmpty)
            {
                return null;
            }

            if (!UnityObjectReference.TryGetResourcesPath(m_reference.Path, out var path))
            {
                throw new InvalidOperationException("Unity object reference does not contain a valid Resources asset path.");
            }

            return path;
        }
    }
}
