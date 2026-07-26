using System;
using UnityEngine;

namespace GameDBLibraryAddressables
{
    public sealed class AddressableAssetLease<T> : IDisposable
        where T : UnityEngine.Object
    {
        private readonly T m_asset;
        private Action m_release;

        public T Asset
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return m_asset;
            }
        }

        public bool IsDisposed { get; private set; }

        internal AddressableAssetLease(T asset, Action release)
        {
            m_asset = asset;
            m_release = release;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            var release = m_release;
            m_release = null;
            release?.Invoke();
        }
    }
}
