using GameDBLibrary;
using System;
using System.Threading;
using UnityEngine;

namespace GameDBLibraryAddressables
{
    public sealed class AddressablesGameDBDataLoader : IGameDBDataLoader
    {
        private readonly IAddressableLoadBackend m_backend;

        public static AddressablesGameDBDataLoader Instance { get; }
            = new AddressablesGameDBDataLoader(AddressablesLoadBackend.Instance);

        internal AddressablesGameDBDataLoader(IAddressableLoadBackend backend)
        {
            m_backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public async Awaitable<string> LoadAsync(string location,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var operation = m_backend.Start<TextAsset>(location);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    "The Addressables backend returned no operation for the GameDB data load.");
            }

            if (!operation.IsValid)
            {
                throw new InvalidOperationException(
                    "The Addressables GameDB data operation was invalid when acquired.");
            }

            try
            {
                while (operation.IsValid && !operation.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!operation.IsValid)
                {
                    throw new InvalidOperationException(
                        "The Addressables GameDB data operation became invalid before completion.");
                }

                if (!operation.Succeeded || operation.Result == null)
                {
                    throw new InvalidOperationException(
                        $"Addressables could not load GameDB JSON at key '{location}' as a TextAsset.",
                        operation.OperationException);
                }

                return operation.Result.text;
            }
            finally
            {
                operation.Release();
            }
        }
    }
}
