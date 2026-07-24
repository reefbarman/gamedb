using System;

namespace GameDBLibrary
{
    public class TableReferenceAccessor<T1, T2> : DataAccessor<T2>
    {
        private bool m_keySet = false;
        private readonly T1 m_referenceKey;
        private readonly T2 m_reference;

        public TableReferenceAccessor(object val, GameDBBase gameDB)
        {
            var referenceKey = val as string;

            if (!string.IsNullOrEmpty(referenceKey))
            {
                if (typeof(T1).IsEnum)
                {
                    m_referenceKey = (T1) Enum.Parse(typeof(T1), referenceKey);
                }
                else
                {
                    m_referenceKey = (T1) val;
                }

                m_keySet = true;
            }

            if (m_keySet)
            {
                var table = gameDB.GetType().GetProperty($"{typeof(T2).Name}Table").GetValue(gameDB, null);
                m_reference = (T2) table.GetType().GetMethod("GetByKeyRaw").Invoke(table, new object[] {referenceKey});
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