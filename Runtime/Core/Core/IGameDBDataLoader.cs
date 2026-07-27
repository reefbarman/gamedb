using System.Threading;
using UnityEngine;

namespace GameDBLibrary
{
    public interface IGameDBDataLoader
    {
        Awaitable<string> LoadAsync(string location,
            CancellationToken cancellationToken = default);
    }
}
