using GameDBLibrary;
using System.Collections.Generic;

namespace GameDBEditorLibrary.Automation
{
    public sealed class GameDBOperationOptions
    {
        public bool DryRun { get; set; }
        public bool AllowDestructive { get; set; }
        public string ExpectedRevision { get; set; }
    }

    public enum GameDBBatchOperationKind
    {
        Unspecified,
        AddTable,
        RenameTable,
        DeleteTable,
        AddField,
        ReplaceField,
        RenameField,
        DeleteField,
        AddRow,
        UpdateRow,
        SetValue,
        RenameRow,
        DeleteRow
    }

    public sealed class GameDBBatchOptions
    {
        public bool DryRun { get; set; }
        public string ExpectedRevision { get; set; }
        public List<GameDBBatchOperationKind> AllowedDestructiveOperations { get; set; }
            = new List<GameDBBatchOperationKind>();
    }

    public sealed class GameDBBatchRequest
    {
        public string DatabasePath { get; set; }
        public List<GameDBBatchOperation> Operations { get; set; } = new List<GameDBBatchOperation>();
        public GameDBBatchOptions Options { get; set; } = new GameDBBatchOptions();
    }

    public sealed class GameDBBatchOperation
    {
        public GameDBBatchOperationKind Kind { get; set; }
        public GameDBBatchTableOperation Table { get; set; }
        public GameDBBatchRenameOperation Rename { get; set; }
        public GameDBBatchDeleteOperation Delete { get; set; }
        public GameDBBatchFieldOperation Field { get; set; }
        public GameDBBatchRowOperation Row { get; set; }
        public GameDBBatchValueOperation Value { get; set; }
    }

    public sealed class GameDBBatchTableOperation
    {
        public string TableName { get; set; }
        public KeyType KeyType { get; set; } = KeyType.@string;
        public string KeyTypeArgument { get; set; }
    }

    public sealed class GameDBBatchRenameOperation
    {
        public string TableName { get; set; }
        public string CurrentName { get; set; }
        public string NewName { get; set; }
    }

    public sealed class GameDBBatchDeleteOperation
    {
        public string TableName { get; set; }
        public string Name { get; set; }
    }

    public sealed class GameDBBatchFieldOperation
    {
        public string TableName { get; set; }
        public string FieldName { get; set; }
        public FieldType FieldType { get; set; } = FieldType.@string;
        public bool IsArray { get; set; }
        public string TypeArgument { get; set; }
        public GameDBDictionaryTypeDefinition DictionaryType { get; set; }
    }

    public sealed class GameDBBatchRowOperation
    {
        public string TableName { get; set; }
        public string RowKey { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
    }

    public sealed class GameDBBatchValueOperation
    {
        public string TableName { get; set; }
        public string RowKey { get; set; }
        public string FieldName { get; set; }
        public object Value { get; set; }
    }

    public enum GameDBCsvImportMode
    {
        Unspecified,
        Replace,
        Upsert
    }

    public sealed class GameDBCsvExportRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
    }

    public sealed class GameDBCsvImportRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string CsvText { get; set; }
        public GameDBCsvImportMode Mode { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public enum GameDBQueryPredicateKind
    {
        Unspecified,
        Equals,
        Contains,
        NumericRange,
        ReferencesRow
    }

    public sealed class GameDBQueryRequest
    {
        public string DatabasePath { get; set; }
        public List<GameDBQueryTableProjection> Tables { get; set; }
            = new List<GameDBQueryTableProjection>();
        public int Limit { get; set; } = 100;
        public string Cursor { get; set; }
    }

    public sealed class GameDBQueryTableProjection
    {
        public string TableName { get; set; }
        public List<string> RowKeys { get; set; } = new List<string>();
        public List<string> FieldNames { get; set; } = new List<string>();
        public List<GameDBQueryPredicate> Predicates { get; set; }
            = new List<GameDBQueryPredicate>();
    }

    public sealed class GameDBQueryPredicate
    {
        public GameDBQueryPredicateKind Kind { get; set; }
        public string FieldName { get; set; }
        public object Value { get; set; }
        public object Minimum { get; set; }
        public object Maximum { get; set; }
    }

    public sealed class GameDBCreateRequest
    {
        public string DatabasePath { get; set; }
        public string ScopeName { get; set; }
        public bool LocalizationDatabase { get; set; }
        public bool Overwrite { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBSaveRequest
    {
        public string DatabasePath { get; set; }
        public string DataJson { get; set; }
        public string SchemaJson { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBTableRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public KeyType KeyType { get; set; } = KeyType.@string;
        public string KeyTypeArgument { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBRenameRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string CurrentName { get; set; }
        public string NewName { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBDeleteRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string Name { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBFieldRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string FieldName { get; set; }
        public FieldType FieldType { get; set; } = FieldType.@string;
        public bool IsArray { get; set; }
        public string TypeArgument { get; set; }
        public GameDBDictionaryTypeDefinition DictionaryType { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBDictionaryTypeDefinition
    {
        public KeyType KeyType { get; set; } = KeyType.@string;
        public string KeyTypeArgument { get; set; }
        public FieldType ValueType { get; set; } = FieldType.@string;
        public string ValueTypeArgument { get; set; }
    }

    public sealed class GameDBRowRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string RowKey { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBValueRequest
    {
        public string DatabasePath { get; set; }
        public string TableName { get; set; }
        public string RowKey { get; set; }
        public string FieldName { get; set; }
        public object Value { get; set; }
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }

    public sealed class GameDBGenerateRequest
    {
        public string DatabasePath { get; set; }
        public string OutputDirectory { get; set; }
        public bool IncludeUnityLoader { get; set; } = true;
        public GameDBOperationOptions Options { get; set; } = new GameDBOperationOptions();
    }
}
