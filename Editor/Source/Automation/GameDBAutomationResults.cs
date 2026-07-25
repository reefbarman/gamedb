using GameDBLibrary;
using System.Collections.Generic;

namespace GameDBEditorLibrary.Automation
{
    public enum GameDBBatchFailureKind
    {
        None,
        InvalidRequest,
        LoadFailed,
        RecoveryRequired,
        AuthorizationDenied,
        RevisionConflict,
        CommandFailed,
        TransactionFailed,
        ValidationFailed,
        CommitFailed
    }

    public enum GameDBBatchCommitStatus
    {
        NotAttempted,
        DryRun,
        Saved,
        NoChanges,
        SerializationFailed,
        ValidationFailed,
        Conflict,
        PersistenceFailed,
        PersistenceStateUnknown,
        PostSavePending
    }

    public sealed class GameDBBatchResult
    {
        public bool Success { get; internal set; }
        public bool DryRun { get; internal set; }
        public GameDBBatchFailureKind FailureKind { get; internal set; }
        public GameDBBatchCommitStatus CommitStatus { get; internal set; }
        public string Operation { get; internal set; }
        public string DatabasePath { get; internal set; }
        public string Message { get; internal set; }
        public int FailedOperationIndex { get; internal set; } = -1;
        public GameDBBatchOperationKind? DeniedOperationKind { get; internal set; }
        public string RevisionBefore { get; internal set; }
        public string RevisionAfter { get; internal set; }
        public GameDBSnapshot Snapshot { get; internal set; }
        public List<GameDBValidationIssue> Issues { get; internal set; } = new List<GameDBValidationIssue>();
        public List<string> ChangedPaths { get; internal set; } = new List<string>();
        public bool FilesCommitted { get; internal set; }
        public bool PostSavePending { get; internal set; }
        public List<string> PostSaveErrors { get; internal set; } = new List<string>();
        public List<string> RecoveryArtifacts { get; internal set; } = new List<string>();
    }

    public sealed class GameDBAutomationResult
    {
        public bool Success { get; internal set; }
        public bool DryRun { get; internal set; }
        public string Operation { get; internal set; }
        public string DatabasePath { get; internal set; }
        public string Message { get; internal set; }
        public string RevisionBefore { get; internal set; }
        public string RevisionAfter { get; internal set; }
        public GameDBSnapshot Snapshot { get; internal set; }
        public List<GameDBValidationIssue> Issues { get; internal set; } = new List<GameDBValidationIssue>();
        public List<string> ChangedPaths { get; internal set; } = new List<string>();
    }

    public sealed class GameDBExportResult
    {
        public bool Success { get; internal set; }
        public string DatabasePath { get; internal set; }
        public string Message { get; internal set; }
        public string DataJson { get; internal set; }
        public string SchemaJson { get; internal set; }
        public List<GameDBValidationIssue> Issues { get; internal set; } = new List<GameDBValidationIssue>();
    }

    public sealed class GameDBListResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public List<string> DatabasePaths { get; internal set; } = new List<string>();
    }

    public sealed class GameDBSnapshot
    {
        public string DatabasePath { get; internal set; }
        public string SchemaPath { get; internal set; }
        public string Revision { get; internal set; }
        public string ScopeName { get; internal set; }
        public bool LocalizationDatabase { get; internal set; }
        public List<GameDBTableSnapshot> Tables { get; internal set; } = new List<GameDBTableSnapshot>();
    }

    public sealed class GameDBTableSnapshot
    {
        public string Name { get; internal set; }
        public KeyType KeyType { get; internal set; }
        public string KeyTypeArgument { get; internal set; }
        public List<GameDBFieldSnapshot> Fields { get; internal set; } = new List<GameDBFieldSnapshot>();
        public List<GameDBRowSnapshot> Rows { get; internal set; } = new List<GameDBRowSnapshot>();
    }

    public sealed class GameDBFieldSnapshot
    {
        public string Name { get; internal set; }
        public FieldType FieldType { get; internal set; }
        public bool IsArray { get; internal set; }
        public string TypeArgument { get; internal set; }
        public GameDBDictionaryTypeDefinition DictionaryType { get; internal set; }
    }

    public sealed class GameDBRowSnapshot
    {
        public string Key { get; internal set; }
        public Dictionary<string, object> Values { get; internal set; } = new Dictionary<string, object>();
    }

    public sealed class GameDBValidationIssue
    {
        public string Code { get; internal set; }
        public string Message { get; internal set; }
        public string TableName { get; internal set; }
        public string FieldName { get; internal set; }
        public string RowKey { get; internal set; }
    }
}
