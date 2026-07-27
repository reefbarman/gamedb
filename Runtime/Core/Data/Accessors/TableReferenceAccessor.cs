using System;

namespace GameDBLibrary
{
    public class TableReferenceAccessor<T1, T2> : DataAccessor<T2>
    {
        private bool m_keySet = false;
        private readonly T1 m_referenceKey;
        private readonly T2 m_reference;

        public TableReferenceAccessor(object val, RowBase owner, string tableName)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Referenced table name is required.",
                    nameof(tableName));
            }

            var referenceKey = val as string;

            if (!string.IsNullOrEmpty(referenceKey))
            {
                if (typeof(T1).IsEnum)
                {
                    m_referenceKey = (T1)Enum.Parse(typeof(T1), referenceKey);
                }
                else
                {
                    m_referenceKey = (T1)val;
                }

                m_keySet = true;
            }

            if (m_keySet)
            {
                m_reference = owner.ResolveReference<T2>(tableName, referenceKey);
            }
        }

        public bool IsSet()
        {
            return m_keySet;
        }

        public T1 GetKey()
        {
            return m_referenceKey;
        }

        public override T2 GetValue()
        {
            return m_reference;
        }
    }
}