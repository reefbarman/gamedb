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
