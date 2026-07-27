using GameDBLibrary;
using System;
using System.Threading;
using UnityEngine;

namespace GameDBLibraryUnity
{
    public sealed class ResourcesGameDBDataLoader : IGameDBDataLoader
    {
        private readonly IResourcesGameDBDataLoadBackend m_backend;

        public static ResourcesGameDBDataLoader Instance { get; }
            = new ResourcesGameDBDataLoader(ResourcesGameDBDataLoadBackend.Instance);

        private ResourcesGameDBDataLoader(IResourcesGameDBDataLoadBackend backend)
        {
            m_backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        internal static ResourcesGameDBDataLoader CreateForTests(
            IResourcesGameDBDataLoadBackend backend)
        {
            return new ResourcesGameDBDataLoader(backend);
        }

        public async Awaitable<string> LoadAsync(string location,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return await m_backend.LoadAsync(location, cancellationToken);
        }
    }

    internal interface IResourcesGameDBDataLoadBackend
    {
        Awaitable<string> LoadAsync(string location,
            CancellationToken cancellationToken);
    }

    internal sealed class ResourcesGameDBDataLoadBackend
        : IResourcesGameDBDataLoadBackend
    {
        internal static readonly ResourcesGameDBDataLoadBackend Instance
            = new ResourcesGameDBDataLoadBackend();

        private ResourcesGameDBDataLoadBackend()
        {
        }

        public async Awaitable<string> LoadAsync(string location,
            CancellationToken cancellationToken)
        {
            var request = Resources.LoadAsync<TextAsset>(location);
            await Awaitable.FromAsyncOperation(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!(request.asset is TextAsset textAsset))
            {
                throw new ArgumentException(
                    $"No GameDB TextAsset was found at Resources path '{location}'.",
                    nameof(location));
            }

            return textAsset.text;
        }
    }
}
