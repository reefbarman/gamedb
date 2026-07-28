using GameDBEditorLibrary.Automation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDBEditorLibrary.Documents
{
    internal enum GameDBSaveStatus
    {
        Saved,
        NoChanges,
        SerializationFailed,
        ValidationFailed,
        Conflict,
        PersistenceFailed,
        PersistenceStateUnknown,
        PostSavePending,
        SaveInProgress
    }

    internal sealed class GameDBSaveOptions
    {
        internal bool ForceWrite { get; set; }
    }

    internal enum GameDBDocumentStateChangeOrigin
    {
        Transaction,
        Undo,
        Redo,
        Recovery,
        RuntimeImport,
        Save
    }

    internal sealed class GameDBDocumentSessionState : IEquatable<GameDBDocumentSessionState>
    {
        internal string DocumentId { get; }
        internal string CurrentRevision { get; }
        internal string BaselineRevision { get; }
        internal GameDBDiskToken BaselineDiskToken { get; }
        internal bool HasPendingPostSaveWork { get; }
        internal bool PersistenceStateUnknown { get; }
        internal bool IsDirty => !string.Equals(CurrentRevision, BaselineRevision,
            StringComparison.OrdinalIgnoreCase);

        internal GameDBDocumentSessionState(string documentId, string currentRevision,
            string baselineRevision, GameDBDiskToken baselineDiskToken,
            bool hasPendingPostSaveWork, bool persistenceStateUnknown)
        {
            DocumentId = documentId;
            CurrentRevision = currentRevision;
            BaselineRevision = baselineRevision;
            BaselineDiskToken = baselineDiskToken;
            HasPendingPostSaveWork = hasPendingPostSaveWork;
            PersistenceStateUnknown = persistenceStateUnknown;
        }

        public bool Equals(GameDBDocumentSessionState other)
        {
            return other != null
                && string.Equals(DocumentId, other.DocumentId, StringComparison.Ordinal)
                && string.Equals(CurrentRevision, other.CurrentRevision,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(BaselineRevision, other.BaselineRevision,
                    StringComparison.OrdinalIgnoreCase)
                && BaselineDiskToken == other.BaselineDiskToken
                && HasPendingPostSaveWork == other.HasPendingPostSaveWork
                && PersistenceStateUnknown == other.PersistenceStateUnknown;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameDBDocumentSessionState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DocumentId?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (CurrentRevision?.ToUpperInvariant().GetHashCode() ?? 0);
                hash = (hash * 397) ^ (BaselineRevision?.ToUpperInvariant().GetHashCode() ?? 0);
                hash = (hash * 397) ^ BaselineDiskToken.GetHashCode();
                hash = (hash * 397) ^ (HasPendingPostSaveWork ? 1 : 0);
                hash = (hash * 397) ^ (PersistenceStateUnknown ? 1 : 0);
                return hash;
            }
        }
    }

    internal sealed class GameDBDocumentStateChange
    {
        internal GameDBDocumentStateChangeOrigin Origin { get; }
        internal GameDBDocumentSessionState Previous { get; }
        internal GameDBDocumentSessionState Current { get; }
        internal GameDBSaveStatus? SaveStatus { get; }
        internal string Message { get; }
        internal bool FilesCommitted { get; }
        internal IReadOnlyList<string> RecoveryArtifacts { get; }

        internal GameDBDocumentStateChange(GameDBDocumentStateChangeOrigin origin,
            GameDBDocumentSessionState previous, GameDBDocumentSessionState current,
            GameDBSaveOutcome saveOutcome = null)
        {
            Origin = origin;
            Previous = previous;
            Current = current;
            SaveStatus = saveOutcome?.Status;
            Message = saveOutcome?.Message;
            FilesCommitted = saveOutcome?.FilesCommitted ?? false;
            RecoveryArtifacts = saveOutcome?.RecoveryArtifacts == null
                ? Array.Empty<string>()
                : new List<string>(saveOutcome.RecoveryArtifacts).AsReadOnly();
        }
    }

    internal enum GameDBDiskRefreshStatus
    {
        Refreshed,
        NoChange,
        Conflict,
        RevisionConflict,
        MissingOrIncomplete,
        RecoveryRequired,
        ReadFailed,
        InvalidContent,
        PendingPostSave
    }

    internal sealed class GameDBDiskRefreshResult
    {
        internal GameDBDiskRefreshStatus Status { get; set; }
        internal bool Success { get; set; }
        internal string Message { get; set; }
        internal GameDBSnapshot Snapshot { get; set; }
        internal GameDBDiskToken? ObservedToken { get; set; }
        internal IReadOnlyList<string> RecoveryArtifacts { get; set; }
            = Array.Empty<string>();
        internal IReadOnlyList<string> NotificationErrors { get; set; }
            = Array.Empty<string>();
    }

    internal enum GameDBDiskState
    {
        Unchanged,
        Modified,
        MissingOrIncomplete,
        RecoveryRequired,
        ReadFailed
    }

    internal sealed class GameDBDiskStateResult
    {
        internal GameDBDiskState State { get; set; }
        internal string Message { get; set; }
        internal GameDBDiskToken BaselineToken { get; set; }
        internal GameDBDiskToken? ObservedToken { get; set; }
        internal IReadOnlyList<string> RecoveryArtifacts { get; set; } = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class GameDBPostSaveState
    {
        [SerializeField] internal bool DataImportPending;
        [SerializeField] internal bool SchemaImportPending;
        [SerializeField] internal bool CallbackPending;
        [SerializeField] internal string ScopeName;

        internal bool HasPendingWork => DataImportPending || SchemaImportPending || CallbackPending;

        internal GameDBPostSaveState Copy()
        {
            return new GameDBPostSaveState
            {
                DataImportPending = DataImportPending,
                SchemaImportPending = SchemaImportPending,
                CallbackPending = CallbackPending,
                ScopeName = ScopeName
            };
        }
    }

    internal sealed class GameDBSaveOutcome
    {
        internal GameDBSaveStatus Status { get; set; }
        internal bool Success { get; set; }
        internal bool FilesCommitted { get; set; }
        internal bool PostSavePending { get; set; }
        internal string Message { get; set; }
        internal string RevisionBefore { get; set; }
        internal string RevisionSaved { get; set; }
        internal string RevisionCurrent { get; set; }
        internal GameDBDiskToken DiskTokenBefore { get; set; }
        internal GameDBDiskToken DiskTokenAfter { get; set; }
        internal IReadOnlyList<string> ChangedPaths { get; set; } = Array.Empty<string>();
        internal IReadOnlyList<string> PostSaveErrors { get; set; } = Array.Empty<string>();
        internal IReadOnlyList<string> RecoveryArtifacts { get; set; } = Array.Empty<string>();
        internal IReadOnlyList<string> NotificationErrors { get; set; } = Array.Empty<string>();
        internal bool NotificationErrorsDeferred { get; set; }
    }

    [Serializable]
    internal sealed class GameDBDocumentState
    {
        internal const int CurrentVersion = 1;

        [SerializeField] internal int Version = CurrentVersion;
        [SerializeField] internal string DocumentId;
        [SerializeField] internal string AssetPath;
        [SerializeField] internal string DataJson;
        [SerializeField] internal string SchemaJson;
        [SerializeField] internal string BaselineRevision;
        [SerializeField] internal GameDBDiskToken BaselineDiskToken;
        [SerializeField] internal bool DataImportPending;
        [SerializeField] internal bool SchemaImportPending;
        [SerializeField] internal bool CallbackPending;
        [SerializeField] internal string PendingScopeName;
        [SerializeField] internal bool PersistenceStateUnknown;
        [SerializeField] internal bool WasDirty;
    }
}
