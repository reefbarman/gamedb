using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    public class DictionaryAccessor<T1, T2> : DataAccessor<Dictionary<T1, T2>>
    {
        private readonly GameDBBase m_gameDB;
        private readonly Type m_keyAccessorType;
        private readonly Type m_valueAccessorType;

        private readonly Dictionary<T1, T2> m_dict;

        public DictionaryAccessor(object val, GameDBBase gameDB, Type keyAccessorType, Type valueAccessorType)
        {
            m_gameDB = gameDB;
            m_keyAccessorType = keyAccessorType;
            m_valueAccessorType = valueAccessorType;

            m_dict = new Dictionary<T1, T2>();

            var dict = val as Dictionary<object, object>;

            foreach (var pair in dict)
            {
                m_dict.Add(GetKeyObject(pair.Key), GetValueObject(pair.Value));
            }
        }

        public override Dictionary<T1, T2> GetValue()
        {
            return m_dict;
        }

        private T1 GetKeyObject(object val)
        {
            return (T1)GetObject(m_keyAccessorType, typeof(T1), val);
        }

        private T2 GetValueObject(object val)
        {
            return (T2)GetObject(m_valueAccessorType, typeof(T2), val);
        }

        private object GetObject(Type accessorType, Type returnType, object val)
        {
            object accessor;

            if (accessorType.Name.StartsWith("TableReferenceAccessor"))
            {
                accessor = Activator.CreateInstance(accessorType, val, m_gameDB);
            }
            else
            {
                accessor = Activator.CreateInstance(accessorType, val);
            }

            if (accessorType == returnType)
            {
                return accessor;
            }
            else
            {
                var method = accessorType.GetMethod("GetValue");
                return method.Invoke(accessor, null);
            }
        }
    }
}
