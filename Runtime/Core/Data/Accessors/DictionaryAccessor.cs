using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameDBLibrary
{
    public class DictionaryAccessor<T1, T2> : DataAccessor<IReadOnlyDictionary<T1, T2>>
    {
        private readonly RowBase m_owner;
        private readonly string m_referencedTable;
        private readonly Type m_keyAccessorType;
        private readonly Type m_valueAccessorType;

        private readonly IReadOnlyDictionary<T1, T2> m_dict;

        public DictionaryAccessor(object val, RowBase owner, string referencedTable,
            Type keyAccessorType, Type valueAccessorType)
        {
            m_owner = owner ?? throw new ArgumentNullException(nameof(owner));
            m_referencedTable = referencedTable;
            m_keyAccessorType = keyAccessorType;
            m_valueAccessorType = valueAccessorType;

            var result = new Dictionary<T1, T2>();
            var dict = val as IDictionary<object, object>;

            foreach (var pair in dict)
            {
                result.Add(GetKeyObject(pair.Key), GetValueObject(pair.Value));
            }

            m_dict = new ReadOnlyDictionary<T1, T2>(result);
        }

        public override IReadOnlyDictionary<T1, T2> GetValue()
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
                accessor = Activator.CreateInstance(accessorType, val, m_owner,
                    m_referencedTable);
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
