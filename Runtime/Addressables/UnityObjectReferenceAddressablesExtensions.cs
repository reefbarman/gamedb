using GameDBLibrary;
using System;
using System.Threading;
using UnityEngine;

namespace GameDBLibraryAddressables
{
    public static class UnityObjectReferenceAddressablesExtensions
    {
        public static Awaitable<AddressableAssetLease<T>> LoadAddressableAsync<T>(
            this UnityObjectReference reference,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            return LoadAddressableAsync<T>(reference, cancellationToken,
                AddressablesLoadBackend.Instance);
        }

        internal static async Awaitable<AddressableAssetLease<T>> LoadAddressableAsync<T>(
            this UnityObjectReference reference, CancellationToken cancellationToken,
            IAddressableLoadBackend backend)
            where T : UnityEngine.Object
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (reference.IsEmpty)
            {
                return new AddressableAssetLease<T>(null, null);
            }

            IAddressableLoadOperation<T> operation;
            try
            {
                operation = backend.Start<T>(reference.Guid);
            }
            catch (Exception exception)
            {
                throw LoadException<T>(reference,
                    "The Addressables operation could not be started. Ensure the asset is Addressable and its group includes GUIDs in the catalog.",
                    exception);
            }

            if (operation == null)
            {
                throw LoadException<T>(reference,
                    "The Addressables backend returned no operation.");
            }

            var ownershipTransferred = false;
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
                    throw LoadException<T>(reference,
                        "The Addressables operation handle became invalid before ownership could be transferred.");
                }

                if (!operation.Succeeded || operation.Result == null)
                {
                    var cause = operation.OperationException;
                    throw LoadException<T>(reference,
                        "Ensure the asset is Addressable, its group has Include GUIDs in Catalog enabled, the Addressables content has been built, and the requested type matches the asset.",
                        cause);
                }

                var lease = new AddressableAssetLease<T>(operation.Result,
                    operation.Release);
                ownershipTransferred = true;
                return lease;
            }
            finally
            {
                if (!ownershipTransferred && operation.IsValid)
                {
                    operation.Release();
                }
            }
        }

        private static AddressableAssetLoadException LoadException<T>(
            UnityObjectReference reference, string detail,
            Exception innerException = null)
            where T : UnityEngine.Object
        {
            return new AddressableAssetLoadException(reference.Guid,
                reference.Path, typeof(T), detail, innerException);
        }
    }
}
