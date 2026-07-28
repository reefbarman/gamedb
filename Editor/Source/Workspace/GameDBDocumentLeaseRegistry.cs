using GameDBEditorLibrary.Documents;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary.Workspace
{
    internal enum GameDBDocumentLeaseAcquireStatus
    {
        Acquired,
        Busy
    }

    internal sealed class GameDBDocumentLeaseAcquireResult
    {
        internal GameDBDocumentLeaseAcquireStatus Status { get; }
        internal GameDBDocumentLease Lease { get; }
        internal string CanonicalAssetPath { get; }
        internal string ExistingOwnerId { get; }

        internal GameDBDocumentLeaseAcquireResult(GameDBDocumentLeaseAcquireStatus status,
            GameDBDocumentLease lease, string canonicalAssetPath, string existingOwnerId)
        {
            Status = status;
            Lease = lease;
            CanonicalAssetPath = canonicalAssetPath;
            ExistingOwnerId = existingOwnerId;
        }
    }

    internal sealed class GameDBDocumentLease : IDisposable
    {
        private readonly object m_gate = new object();
        private Action m_release;

        internal string CanonicalAssetPath { get; }
        internal string OwnerId { get; }
        internal bool IsDisposed
        {
            get
            {
                lock (m_gate)
                {
                    return m_release == null;
                }
            }
        }

        internal GameDBDocumentLease(string canonicalAssetPath, string ownerId, Action release)
        {
            CanonicalAssetPath = canonicalAssetPath;
            OwnerId = ownerId;
            m_release = release;
        }

        public void Dispose()
        {
            Action release;
            lock (m_gate)
            {
                release = m_release;
                m_release = null;
            }

            release?.Invoke();
        }
    }

    internal sealed class GameDBDocumentLeaseRegistry
    {
        private readonly object m_gate = new object();
        private readonly IGameDBPairStore m_pairStore;
        private readonly Dictionary<string, Entry> m_entries;
        private long m_nextGeneration;

        internal static GameDBDocumentLeaseRegistry Domain { get; }
            = new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance);

        internal IGameDBPairStore PairStore => m_pairStore;

        internal GameDBDocumentLeaseRegistry(IGameDBPairStore pairStore)
        {
            m_pairStore = pairStore ?? throw new ArgumentNullException(nameof(pairStore));
            m_entries = new Dictionary<string, Entry>(m_pairStore.LockKeyComparer);
        }

        internal GameDBDocumentLeaseAcquireResult TryAcquire(string assetPath, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new ArgumentException("Lease owner identity is required.", nameof(ownerId));
            }

            var path = m_pairStore.Resolve(assetPath);
            lock (m_gate)
            {
                if (m_entries.TryGetValue(path.LockKey, out var existing))
                {
                    return new GameDBDocumentLeaseAcquireResult(
                        GameDBDocumentLeaseAcquireStatus.Busy, null,
                        existing.CanonicalAssetPath, existing.OwnerId);
                }

                var generation = ++m_nextGeneration;
                var entry = new Entry(ownerId, path.AssetPath, generation);
                m_entries.Add(path.LockKey, entry);
                var lease = new GameDBDocumentLease(path.AssetPath, ownerId,
                    () => Release(path.LockKey, generation));
                return new GameDBDocumentLeaseAcquireResult(
                    GameDBDocumentLeaseAcquireStatus.Acquired, lease,
                    path.AssetPath, null);
            }
        }

        internal bool RefersToSameAsset(string firstAssetPath, string secondAssetPath)
        {
            var first = m_pairStore.Resolve(firstAssetPath);
            var second = m_pairStore.Resolve(secondAssetPath);
            return m_pairStore.LockKeyComparer.Equals(first.LockKey, second.LockKey);
        }

        private void Release(string lockKey, long generation)
        {
            lock (m_gate)
            {
                if (m_entries.TryGetValue(lockKey, out var entry)
                    && entry.Generation == generation)
                {
                    m_entries.Remove(lockKey);
                }
            }
        }

        private sealed class Entry
        {
            internal string OwnerId { get; }
            internal string CanonicalAssetPath { get; }
            internal long Generation { get; }

            internal Entry(string ownerId, string canonicalAssetPath, long generation)
            {
                OwnerId = ownerId;
                CanonicalAssetPath = canonicalAssetPath;
                Generation = generation;
            }
        }
    }
}
