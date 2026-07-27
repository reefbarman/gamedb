using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;

public class Row : RowBase
{
    private readonly Dictionary<string, IDataAccessor> m_dataAccessors;
    private readonly Dictionary<string, ICollection> m_listAccessors;

    public Row(string name) : base(name)
    {
        m_dataAccessors = new Dictionary<string, IDataAccessor>();
        m_listAccessors = new Dictionary<string, ICollection>();
    }

    protected T GetCacheOrCreateAccessor<T>(string accessorName, Func<IDataAccessor> createAccessor)
    {
        IDataAccessor accessor = null;

        if (m_dataAccessors.ContainsKey(accessorName))
        {
            accessor = m_dataAccessors[accessorName];
        }
        else
        {
            accessor = createAccessor();
            m_dataAccessors.Add(accessorName, accessor);
        }

        return (T)accessor;
    }

    protected T GetCacheOrCreateListAccessor<T>(string accessorName, Func<ICollection> createAccessor)
    {
        ICollection accessor = null;

        if (m_listAccessors.ContainsKey(accessorName))
        {
            accessor = m_listAccessors[accessorName];
        }
        else
        {
            accessor = createAccessor();
            m_listAccessors.Add(accessorName, accessor);
        }

        return (T)accessor;
    }
}
