using System.Collections.Generic;

namespace GameDBLibrary
{
    public interface IGameDB
    {
        Dictionary<string, TableBase> Tables { get; }

        bool Load(string path);
    }
}
