using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GameDBEditorLibrary.Workspace
{
    internal enum GameDBAssetSessionOpenStatus
    {
        Opened,
        Busy
    }

    internal sealed class GameDBAssetSessionOpenResult
    {
        internal GameDBAssetSessionOpenStatus Status { get; }
        internal GameDBAssetSession Session { get; }
        internal string CanonicalAssetPath { get; }
        internal string ExistingSessionId { get; }

        internal GameDBAssetSessionOpenResult(GameDBAssetSessionOpenStatus status,
            GameDBAssetSession session, string canonicalAssetPath, string existingSessionId)
        {
            Status = status;
            Session = session;
            CanonicalAssetPath = canonicalAssetPath;
            ExistingSessionId = existingSessionId;
        }
    }

    internal sealed class GameDBAssetSession : IDisposable
    {
        private readonly object m_gate = new object();
        private readonly Dictionary<int, int> m_activityDepthByThread
            = new Dictionary<int, int>();
        private GameDBDocumentLease m_lease;
        private GameDBDocument m_document;
        private Action<GameDBDocumentChange> m_changed;
        private Action<GameDBDocumentStateChange> m_stateChanged;
        private IReadOnlyCollection<GameDBCommandKind> m_allowedOperations;
        private int m_activeActivities;
        private bool m_disposeRequested;
        private bool m_cleanupStarted;
        private bool m_cleanupCompleted;

        internal string SessionId { get; }
        internal string AssetPath { get; }
        internal bool IsDisposed
        {
            get
            {
                lock (m_gate)
                {
                    return m_disposeRequested;
                }
            }
        }

        internal string DocumentId => UseDocument(document => document.DocumentId);

        internal event Action<GameDBDocumentChange> Changed
        {
            add
            {
                lock (m_gate)
                {
                    ThrowIfDisposedLocked();
                    m_changed += value;
                }
            }
            remove
            {
                lock (m_gate)
                {
                    m_changed -= value;
                }
            }
        }

        internal event Action<GameDBDocumentStateChange> StateChanged
        {
            add
            {
                lock (m_gate)
                {
                    ThrowIfDisposedLocked();
                    m_stateChanged += value;
                }
            }
            remove
            {
                lock (m_gate)
                {
                    m_stateChanged -= value;
                }
            }
        }

        private GameDBAssetSession(string sessionId, GameDBDocumentLease lease,
            GameDBDocument document)
        {
            SessionId = sessionId;
            AssetPath = lease.CanonicalAssetPath;
            m_lease = lease;
            m_document = document;
            m_document.EnableHistory();
            m_document.Changed += OnDocumentChanged;
            m_document.StateChanged += OnDocumentStateChanged;
        }

        internal static GameDBAssetSessionOpenResult TryOpen(
            GameDBDocumentLeaseRegistry registry, string assetPath,
            string sessionId = null)
        {
            return TryConstruct(registry, assetPath, sessionId,
                path => GameDBDocument.Load(path, registry.PairStore,
                    GameDBUnityPostSaveActions.Instance));
        }

        internal static GameDBAssetSessionOpenResult TryCreateNew(
            GameDBDocumentLeaseRegistry registry, string assetPath,
            string scopeName, bool localization, string sessionId = null)
        {
            return TryConstruct(registry, assetPath, sessionId,
                path => GameDBDocument.CreateNew(path, scopeName, localization,
                    registry.PairStore, GameDBUnityPostSaveActions.Instance));
        }

        internal static GameDBAssetSessionOpenResult TryRestore(
            GameDBDocumentLeaseRegistry registry, GameDBDocumentState state,
            string sessionId = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TryConstruct(registry, state.AssetPath, sessionId,
                _ => GameDBDocument.RestoreState(state, registry.PairStore,
                    GameDBUnityPostSaveActions.Instance));
        }

        private static GameDBAssetSessionOpenResult TryConstruct(
            GameDBDocumentLeaseRegistry registry, string assetPath, string sessionId,
            Func<string, GameDBDocument> construct)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            sessionId = string.IsNullOrEmpty(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session identity is required.", nameof(sessionId));
            }

            var acquired = registry.TryAcquire(assetPath, sessionId);
            if (acquired.Status == GameDBDocumentLeaseAcquireStatus.Busy)
            {
                return new GameDBAssetSessionOpenResult(GameDBAssetSessionOpenStatus.Busy,
                    null, acquired.CanonicalAssetPath, acquired.ExistingOwnerId);
            }

            try
            {
                var document = construct(acquired.CanonicalAssetPath);
                var session = new GameDBAssetSession(sessionId, acquired.Lease, document);
                return new GameDBAssetSessionOpenResult(GameDBAssetSessionOpenStatus.Opened,
                    session, acquired.CanonicalAssetPath, null);
            }
            catch
            {
                acquired.Lease.Dispose();
                throw;
            }
        }

        internal GameDBDocumentSessionState GetState()
        {
            return UseDocument(document => document.GetSessionState());
        }

        internal GameDBSnapshot CreateSnapshot()
        {
            return UseDocument(document => document.CreateSnapshot());
        }

        internal GameDBSerializedState SerializeCurrent()
        {
            return UseDocument(document => document.SerializeCurrent());
        }

        internal GameDBDocumentState CaptureState()
        {
            return UseDocument(document => document.CaptureState());
        }

        internal GameDBTransactionResult ApplyTransaction(
            IReadOnlyList<GameDBCommand> commands, GameDBTransactionOptions options = null)
        {
            IReadOnlyCollection<GameDBCommandKind> sessionAllowed;
            lock (m_gate)
            {
                ThrowIfDisposedLocked();
                sessionAllowed = m_allowedOperations?.ToArray();
            }
            var requestedAllowed = options?.AllowedOperations;
            var effectiveAllowed = sessionAllowed == null
                ? requestedAllowed
                : requestedAllowed == null
                    ? sessionAllowed
                    : sessionAllowed.Intersect(requestedAllowed).ToArray();
            var effectiveOptions = new GameDBTransactionOptions
            {
                ExpectedRevision = options?.ExpectedRevision,
                AllowedOperations = effectiveAllowed,
                AllowedDestructiveOperations = options?.AllowedDestructiveOperations
                    ?? Array.Empty<GameDBCommandKind>()
            };
            return UseDocument(document => document.ApplyTransaction(commands, effectiveOptions));
        }

        internal void SetAllowedOperations(
            IReadOnlyCollection<GameDBCommandKind> allowedOperations)
        {
            lock (m_gate)
            {
                ThrowIfDisposedLocked();
                m_allowedOperations = allowedOperations?.ToArray();
            }
        }

        internal GameDBWorkingStateResult ReplaceWorkingState(string dataJson,
            string schemaJson, string expectedRevision, GameDBDocumentChangeOrigin origin)
        {
            return UseDocument(document => document.ReplaceWorkingState(
                dataJson, schemaJson, expectedRevision, origin));
        }

        internal void ResetHistory()
        {
            UseDocument(document =>
            {
                document.ResetHistory();
                return true;
            });
        }

        internal GameDBHistoryState GetHistoryState()
        {
            return UseDocument(document => document.GetHistoryState());
        }

        internal GameDBHistoryResult Undo()
        {
            return UseDocument(document => document.Undo());
        }

        internal GameDBHistoryResult Redo()
        {
            return UseDocument(document => document.Redo());
        }

        internal GameDBDiskRefreshResult RefreshFromDisk(string expectedRevision,
            bool discardWorkingCopy = false)
        {
            return UseDocument(document => document.RefreshFromDisk(
                expectedRevision, discardWorkingCopy));
        }

        internal GameDBSaveOutcome Save(GameDBSaveOptions options = null)
        {
            return UseDocument(document => document.Save(options));
        }

        internal GameDBDiskStateResult ProbeDiskState()
        {
            return UseDocument(document => document.ProbeDiskState());
        }

        public void Dispose()
        {
            GameDBDocument document = null;
            GameDBDocumentLease lease = null;
            var threadId = Thread.CurrentThread.ManagedThreadId;
            lock (m_gate)
            {
                if (m_cleanupCompleted)
                {
                    return;
                }

                m_disposeRequested = true;
                m_changed = null;
                m_stateChanged = null;
                var selfActive = m_activityDepthByThread.ContainsKey(threadId);
                if (m_activeActivities == 0)
                {
                    BeginCleanupLocked(out document, out lease);
                }
                else if (selfActive)
                {
                    return;
                }
                else
                {
                    while (!m_cleanupCompleted)
                    {
                        Monitor.Wait(m_gate);
                    }

                    return;
                }
            }

            FinishCleanup(document, lease);
        }

        private T UseDocument<T>(Func<GameDBDocument, T> operation)
        {
            GameDBDocument document;
            lock (m_gate)
            {
                ThrowIfDisposedLocked();
                BeginActivityLocked();
                document = m_document;
            }

            try
            {
                return operation(document);
            }
            finally
            {
                CompleteActivity();
            }
        }

        private void OnDocumentChanged(GameDBDocumentChange change)
        {
            Dispatch(change, change.DocumentId, () => m_changed);
        }

        private void OnDocumentStateChanged(GameDBDocumentStateChange change)
        {
            Dispatch(change, change.Current.DocumentId, () => m_stateChanged);
        }

        private void Dispatch<T>(T change, string documentId,
            Func<Action<T>> getSubscribers)
        {
            Action<T> snapshot;
            lock (m_gate)
            {
                if (m_disposeRequested || m_document == null
                    || documentId != m_document.DocumentId)
                {
                    return;
                }

                snapshot = getSubscribers();
                if (snapshot == null)
                {
                    return;
                }

                BeginActivityLocked();
            }

            var errors = new List<string>();
            try
            {
                foreach (Action<T> subscriber in snapshot.GetInvocationList())
                {
                    try
                    {
                        subscriber(change);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception.Message);
                    }
                }
            }
            finally
            {
                CompleteActivity();
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }
        }

        private void BeginActivityLocked()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            m_activeActivities++;
            m_activityDepthByThread.TryGetValue(threadId, out var depth);
            m_activityDepthByThread[threadId] = depth + 1;
        }

        private void CompleteActivity()
        {
            GameDBDocument document = null;
            GameDBDocumentLease lease = null;
            lock (m_gate)
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var depth = m_activityDepthByThread[threadId] - 1;
                if (depth == 0)
                {
                    m_activityDepthByThread.Remove(threadId);
                }
                else
                {
                    m_activityDepthByThread[threadId] = depth;
                }

                m_activeActivities--;
                if (m_activeActivities == 0 && m_disposeRequested)
                {
                    BeginCleanupLocked(out document, out lease);
                }
            }

            FinishCleanup(document, lease);
        }

        private void BeginCleanupLocked(out GameDBDocument document,
            out GameDBDocumentLease lease)
        {
            if (m_cleanupStarted)
            {
                document = null;
                lease = null;
                return;
            }

            m_cleanupStarted = true;
            document = m_document;
            lease = m_lease;
            m_document = null;
            m_lease = null;
        }

        private void FinishCleanup(GameDBDocument document, GameDBDocumentLease lease)
        {
            if (document == null)
            {
                return;
            }

            try
            {
                document.Changed -= OnDocumentChanged;
                document.StateChanged -= OnDocumentStateChanged;
                lease.Dispose();
            }
            finally
            {
                lock (m_gate)
                {
                    m_cleanupCompleted = true;
                    Monitor.PulseAll(m_gate);
                }
            }
        }

        private void ThrowIfDisposedLocked()
        {
            if (m_disposeRequested)
            {
                throw new ObjectDisposedException(nameof(GameDBAssetSession));
            }
        }
    }
}
