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
        PostSavePending
    }

    internal sealed class GameDBSaveOptions
    {
        internal bool ForceWrite { get; set; }
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
