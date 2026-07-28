using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;

namespace GameDBEditorLibrary.Workspace
{
    internal enum GameDBPlayModeOperationStatus
    {
        Succeeded,
        InvalidRequest,
        StalePlaySession,
        TargetUnavailable,
        RuntimeImportFailed,
        DocumentChanged,
        DocumentImportFailed,
        RuntimeReloadFailed
    }

    internal sealed class GameDBPlayModeBinding
    {
        internal string TargetId { get; }
        internal long Epoch { get; }

        internal GameDBPlayModeBinding(string targetId, long epoch)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Runtime target identity is required.", nameof(targetId));
            }
            TargetId = targetId;
            Epoch = epoch;
        }
    }

    internal sealed class GameDBPlayModeOperationResult
    {
        internal GameDBPlayModeOperationStatus Status { get; }
        internal string Message { get; }
        internal GameDBPlayModeBinding Binding { get; }
        internal GameDBSnapshot Snapshot { get; }
        internal bool Success => Status == GameDBPlayModeOperationStatus.Succeeded;

        internal GameDBPlayModeOperationResult(GameDBPlayModeOperationStatus status,
            string message = null, GameDBPlayModeBinding binding = null,
            GameDBSnapshot snapshot = null)
        {
            Status = status;
            Message = message;
            Binding = binding;
            Snapshot = snapshot;
        }
    }

    internal sealed class GameDBPlayModeService
    {
        private readonly GameDBRuntimeRegistry m_registry;

        internal GameDBPlayModeService(GameDBRuntimeRegistry registry)
        {
            m_registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        internal GameDBRuntimeRegistrySnapshot GetTargets()
        {
            return m_registry.GetSnapshot();
        }

        internal GameDBPlayModeOperationResult LoadRuntimeData(GameDBAssetSession session,
            string targetId, long expectedEpoch, string expectedRevision)
        {
            if (session == null || session.IsDisposed || string.IsNullOrWhiteSpace(targetId)
                || string.IsNullOrWhiteSpace(expectedRevision))
            {
                return Failure(GameDBPlayModeOperationStatus.InvalidRequest,
                    "An active document, runtime target, and expected revision are required.");
            }

            var resolved = ResolveTarget(targetId, expectedEpoch, out var target);
            if (resolved != null)
            {
                return resolved;
            }

            GameDBSerializedState serialized;
            try
            {
                var current = session.SerializeCurrent();
                var runtimeModel = GameDB.CreateFromRuntimeDB(current.SchemaJson, target);
                serialized = GameDBModelCodec.Serialize(runtimeModel);
            }
            catch (Exception exception)
            {
                return Failure(GameDBPlayModeOperationStatus.RuntimeImportFailed,
                    exception.Message);
            }
            var replaced = session.ReplaceWorkingState(serialized.DataJson,
                serialized.SchemaJson, expectedRevision,
                GameDBDocumentChangeOrigin.RuntimeImport);
            if (!replaced.Success)
            {
                return Failure(replaced.FailureKind == GameDBWorkingStateFailureKind.RevisionConflict
                    ? GameDBPlayModeOperationStatus.DocumentChanged
                    : GameDBPlayModeOperationStatus.DocumentImportFailed,
                    replaced.Message, replaced.AttemptedSnapshot);
            }

            return new GameDBPlayModeOperationResult(GameDBPlayModeOperationStatus.Succeeded,
                binding: new GameDBPlayModeBinding(targetId, expectedEpoch),
                snapshot: replaced.AttemptedSnapshot ?? session.CreateSnapshot());
        }

        internal GameDBPlayModeOperationResult ReloadInGame(GameDBAssetSession session,
            GameDBPlayModeBinding binding, string expectedRevision)
        {
            if (session == null || session.IsDisposed || binding == null
                || string.IsNullOrWhiteSpace(expectedRevision))
            {
                return Failure(GameDBPlayModeOperationStatus.InvalidRequest,
                    "An active runtime-bound document and expected revision are required.");
            }

            var resolved = ResolveTarget(binding.TargetId, binding.Epoch, out var target);
            if (resolved != null)
            {
                return resolved;
            }

            var state = session.SerializeCurrent();
            if (!string.Equals(state.Revision, expectedRevision,
                StringComparison.OrdinalIgnoreCase))
            {
                return Failure(GameDBPlayModeOperationStatus.DocumentChanged,
                    $"Revision conflict. Expected {expectedRevision}, but the document is {state.Revision}.",
                    session.CreateSnapshot());
            }

            Exception error;
            try
            {
                error = target.ImportEditorData(state.DataJson);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            if (error != null)
            {
                return Failure(GameDBPlayModeOperationStatus.RuntimeReloadFailed,
                    error.Message, session.CreateSnapshot());
            }

            return new GameDBPlayModeOperationResult(GameDBPlayModeOperationStatus.Succeeded,
                binding: binding, snapshot: session.CreateSnapshot());
        }

        internal bool IsCurrent(GameDBPlayModeBinding binding)
        {
            if (binding == null)
            {
                return false;
            }
            var snapshot = m_registry.GetSnapshot();
            return snapshot.Epoch == binding.Epoch
                && m_registry.TryResolve(binding.TargetId, out _);
        }

        private GameDBPlayModeOperationResult ResolveTarget(string targetId,
            long expectedEpoch, out GameDBBase target)
        {
            target = null;
            var snapshot = m_registry.GetSnapshot();
            if (snapshot.Epoch != expectedEpoch)
            {
                return Failure(GameDBPlayModeOperationStatus.StalePlaySession,
                    "The Play Mode session changed. Select a runtime GameDB again.");
            }
            if (!m_registry.TryResolve(targetId, out target))
            {
                return Failure(GameDBPlayModeOperationStatus.TargetUnavailable,
                    "The selected runtime GameDB is no longer available.");
            }
            return null;
        }

        private static GameDBPlayModeOperationResult Failure(
            GameDBPlayModeOperationStatus status, string message,
            GameDBSnapshot snapshot = null)
        {
            return new GameDBPlayModeOperationResult(status, message, snapshot: snapshot);
        }
    }
}
