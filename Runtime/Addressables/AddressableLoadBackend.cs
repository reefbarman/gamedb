using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameDBLibraryAddressables
{
    internal interface IAddressableLoadBackend
    {
        IAddressableLoadOperation<T> Start<T>(string key)
            where T : UnityEngine.Object;
    }

    internal interface IAddressableLoadOperation<T>
        where T : UnityEngine.Object
    {
        bool IsValid { get; }
        bool IsDone { get; }
        bool Succeeded { get; }
        T Result { get; }
        Exception OperationException { get; }
        void Release();
    }

    internal sealed class AddressablesLoadBackend : IAddressableLoadBackend
    {
        internal static readonly AddressablesLoadBackend Instance =
            new AddressablesLoadBackend();

        private AddressablesLoadBackend()
        {
        }

        public IAddressableLoadOperation<T> Start<T>(string key)
            where T : UnityEngine.Object
        {
            return new AddressablesLoadOperation<T>(
                Addressables.LoadAssetAsync<T>(key));
        }
    }

    internal sealed class AddressablesLoadOperation<T> : IAddressableLoadOperation<T>
        where T : UnityEngine.Object
    {
        private AsyncOperationHandle<T> m_handle;
        private bool m_released;

        internal AddressablesLoadOperation(AsyncOperationHandle<T> handle)
        {
            m_handle = handle;
        }

        public bool IsValid => !m_released && m_handle.IsValid();
        public bool IsDone => IsValid && m_handle.IsDone;
        public bool Succeeded => IsValid
            && m_handle.Status == AsyncOperationStatus.Succeeded;
        public T Result => IsValid ? m_handle.Result : null;
        public Exception OperationException => IsValid
            ? m_handle.OperationException
            : null;

        public void Release()
        {
            if (m_released)
            {
                return;
            }

            m_released = true;
            if (m_handle.IsValid())
            {
                Addressables.Release(m_handle);
            }
        }
    }
}
