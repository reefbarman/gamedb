using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameDBEditorLibrary.UI
{
    internal enum GameDBInspectorContextKind
    {
        Database,
        Table,
        Field
    }

    internal enum GameDBInspectorTaskKind
    {
        EditDatabase,
        CreateTable,
        RenameTable,
        CreateField,
        RenameField,
        ChangeFieldType
    }

    internal enum GameDBInspectorPendingIntentKind
    {
        SelectTable,
        SelectField,
        ActivateTab,
        CreateOrOpenDatabase,
        CloseTab,
        CloseInspector,
        OpenSettingsOrModal,
        ReloadOrRevert,
        EnterPlayMode
    }

    internal interface IGameDBInspectorDraft
    {
    }

    internal sealed class GameDBInspectorDatabaseDraft : IGameDBInspectorDraft
    {
        internal string ScopeName { get; set; }
        internal bool LocalizationDatabase { get; set; }

        internal GameDBInspectorDatabaseDraft(string scopeName, bool localizationDatabase)
        {
            ScopeName = scopeName ?? string.Empty;
            LocalizationDatabase = localizationDatabase;
        }
    }

    internal sealed class GameDBInspectorTableDraft : IGameDBInspectorDraft
    {
        internal string Name { get; set; }
        internal KeyType KeyType { get; set; }
        internal string KeyTypeArgument { get; set; }

        internal GameDBInspectorTableDraft(string name, KeyType keyType,
            string keyTypeArgument)
        {
            Name = name ?? string.Empty;
            KeyType = keyType;
            KeyTypeArgument = keyTypeArgument;
        }
    }

    internal sealed class GameDBInspectorFieldNameDraft : IGameDBInspectorDraft
    {
        internal string Name { get; set; }

        internal GameDBInspectorFieldNameDraft(string name)
        {
            Name = name ?? string.Empty;
        }
    }

    internal enum GameDBFieldShape
    {
        Scalar,
        Array,
        Dictionary
    }

    internal sealed class GameDBInspectorFieldTypeDraft : IGameDBInspectorDraft
    {
        internal GameDBFieldShape Shape { get; set; }
        internal FieldType FieldType { get; set; }
        internal string TypeArgument { get; set; }
        internal KeyType DictionaryKeyType { get; set; }
        internal string DictionaryKeyTypeArgument { get; set; }
        internal FieldType DictionaryValueType { get; set; }
        internal string DictionaryValueTypeArgument { get; set; }

        internal GameDBInspectorFieldTypeDraft(FieldType fieldType, bool isArray,
            string typeArgument, GameDBDictionaryTypeDefinition dictionaryType = null)
        {
            Shape = fieldType == FieldType.dictionary
                ? GameDBFieldShape.Dictionary
                : isArray ? GameDBFieldShape.Array : GameDBFieldShape.Scalar;
            FieldType = fieldType == FieldType.dictionary
                ? FieldType.@string : fieldType;
            TypeArgument = typeArgument;
            DictionaryKeyType = dictionaryType?.KeyType ?? KeyType.@string;
            DictionaryKeyTypeArgument = dictionaryType?.KeyTypeArgument;
            DictionaryValueType = dictionaryType?.ValueType ?? FieldType.@string;
            DictionaryValueTypeArgument = dictionaryType?.ValueTypeArgument;
        }
    }

    internal sealed class GameDBInspectorFieldDraft : IGameDBInspectorDraft
    {
        internal string Name { get; set; }
        internal GameDBInspectorFieldTypeDraft Type { get; }

        internal GameDBInspectorFieldDraft(string name,
            GameDBInspectorFieldTypeDraft type)
        {
            Name = name ?? string.Empty;
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }
    }

    internal sealed class GameDBInspectorContext : IEquatable<GameDBInspectorContext>
    {
        internal GameDBInspectorContextKind Kind { get; }
        internal string TabId { get; }
        internal string DocumentId { get; }
        internal string TableName { get; }
        internal string FieldName { get; }

        private GameDBInspectorContext(GameDBInspectorContextKind kind, string tabId,
            string documentId, string tableName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(tabId))
            {
                throw new ArgumentException("Inspector context requires a tab identity.",
                    nameof(tabId));
            }
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Inspector context requires a document identity.",
                    nameof(documentId));
            }
            if (kind != GameDBInspectorContextKind.Database
                && string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table and field contexts require a table identity.",
                    nameof(tableName));
            }
            if (kind == GameDBInspectorContextKind.Field
                && string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException("Field context requires a field identity.",
                    nameof(fieldName));
            }

            Kind = kind;
            TabId = tabId;
            DocumentId = documentId;
            TableName = tableName;
            FieldName = fieldName;
        }

        internal static GameDBInspectorContext Database(string tabId, string documentId)
        {
            return new GameDBInspectorContext(GameDBInspectorContextKind.Database,
                tabId, documentId, null, null);
        }

        internal static GameDBInspectorContext Table(string tabId, string documentId,
            string tableName)
        {
            return new GameDBInspectorContext(GameDBInspectorContextKind.Table,
                tabId, documentId, tableName, null);
        }

        internal static GameDBInspectorContext Field(string tabId, string documentId,
            string tableName, string fieldName)
        {
            return new GameDBInspectorContext(GameDBInspectorContextKind.Field,
                tabId, documentId, tableName, fieldName);
        }

        public bool Equals(GameDBInspectorContext other)
        {
            return other != null && Kind == other.Kind
                && string.Equals(TabId, other.TabId, StringComparison.Ordinal)
                && string.Equals(DocumentId, other.DocumentId, StringComparison.Ordinal)
                && string.Equals(TableName, other.TableName, StringComparison.Ordinal)
                && string.Equals(FieldName, other.FieldName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameDBInspectorContext);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(TabId);
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(DocumentId);
                hash = hash * 397 ^ (TableName == null
                    ? 0 : StringComparer.Ordinal.GetHashCode(TableName));
                hash = hash * 397 ^ (FieldName == null
                    ? 0 : StringComparer.Ordinal.GetHashCode(FieldName));
                return hash;
            }
        }
    }

    internal sealed class GameDBInspectorPendingIntent
    {
        internal GameDBInspectorPendingIntentKind Kind { get; }
        internal GameDBInspectorContext TargetContext { get; }
        internal string TabId { get; }

        internal GameDBInspectorPendingIntent(GameDBInspectorPendingIntentKind kind,
            GameDBInspectorContext targetContext = null, string tabId = null)
        {
            if (targetContext != null && tabId != null
                && !string.Equals(tabId, targetContext.TabId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Pending intent tab identity must match its target context.",
                    nameof(tabId));
            }
            switch (kind)
            {
                case GameDBInspectorPendingIntentKind.SelectTable:
                    RequireTargetContext(targetContext,
                        GameDBInspectorContextKind.Table, kind);
                    break;
                case GameDBInspectorPendingIntentKind.SelectField:
                    RequireTargetContext(targetContext,
                        GameDBInspectorContextKind.Field, kind);
                    break;
                case GameDBInspectorPendingIntentKind.ActivateTab:
                case GameDBInspectorPendingIntentKind.CloseTab:
                    if (string.IsNullOrWhiteSpace(tabId ?? targetContext?.TabId))
                    {
                        throw new ArgumentException(
                            $"Pending intent '{kind}' requires a tab identity.",
                            nameof(tabId));
                    }
                    break;
            }
            Kind = kind;
            TargetContext = targetContext;
            TabId = tabId ?? targetContext?.TabId;
        }

        private static void RequireTargetContext(GameDBInspectorContext context,
            GameDBInspectorContextKind expectedKind,
            GameDBInspectorPendingIntentKind intentKind)
        {
            if (context == null || context.Kind != expectedKind)
            {
                throw new ArgumentException(
                    $"Pending intent '{intentKind}' requires a {expectedKind} context.",
                    nameof(context));
            }
        }
    }

    internal sealed class GameDBInspectorSchemaFingerprint
        : IEquatable<GameDBInspectorSchemaFingerprint>
    {
        internal string Value { get; }

        private GameDBInspectorSchemaFingerprint(string value)
        {
            Value = value;
        }

        internal static GameDBInspectorSchemaFingerprint Capture(GameDBSnapshot snapshot,
            GameDBInspectorTaskKind taskKind, GameDBInspectorContext context)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var canonical = new StringBuilder();
            Append(canonical, "GameDBInspectorSchema/v1");
            Append(canonical, ((int)taskKind).ToString());
            switch (taskKind)
            {
                case GameDBInspectorTaskKind.EditDatabase:
                    AppendDatabaseSchema(canonical, snapshot);
                    break;
                case GameDBInspectorTaskKind.CreateTable:
                case GameDBInspectorTaskKind.RenameTable:
                    AppendTableCatalog(canonical, snapshot);
                    break;
                case GameDBInspectorTaskKind.CreateField:
                case GameDBInspectorTaskKind.RenameField:
                    AppendFieldCatalog(canonical, snapshot, context.TableName);
                    break;
                case GameDBInspectorTaskKind.ChangeFieldType:
                    Append(canonical, snapshot.LocalizationDatabase ? "1" : "0");
                    AppendTableNames(canonical, snapshot);
                    AppendField(canonical, FindField(snapshot, context.TableName,
                        context.FieldName));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(taskKind), taskKind, null);
            }

            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical.ToString()));
                return new GameDBInspectorSchemaFingerprint(string.Concat(
                    bytes.Select(value => value.ToString("x2"))));
            }
        }

        public bool Equals(GameDBInspectorSchemaFingerprint other)
        {
            return other != null
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameDBInspectorSchemaFingerprint);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        private static void AppendDatabaseSchema(StringBuilder canonical,
            GameDBSnapshot snapshot)
        {
            Append(canonical, "database-metadata");
            Append(canonical, snapshot.ScopeName);
            Append(canonical, snapshot.LocalizationDatabase ? "1" : "0");
            AppendLocalizationCompatibility(canonical, snapshot);
        }

        private static void AppendLocalizationCompatibility(StringBuilder canonical,
            GameDBSnapshot snapshot)
        {
            var compatible = OrderedTables(snapshot).SelectMany(table =>
                    table.Fields ?? new List<GameDBFieldSnapshot>())
                .All(field => field.FieldType == FieldType.@string
                    && !field.IsArray && field.DictionaryType == null);
            Append(canonical, compatible ? "1" : "0");
        }

        private static void AppendTableCatalog(StringBuilder canonical,
            GameDBSnapshot snapshot)
        {
            Append(canonical, "table-catalog");
            var tables = OrderedTables(snapshot).ToArray();
            Append(canonical, tables.Length.ToString());
            foreach (var table in tables)
            {
                Append(canonical, "table-identity");
                Append(canonical, table.Name);
                Append(canonical, ((int)table.KeyType).ToString());
                Append(canonical, table.KeyTypeArgument);
            }
        }

        private static void AppendFieldCatalog(StringBuilder canonical,
            GameDBSnapshot snapshot, string tableName)
        {
            Append(canonical, "field-catalog");
            Append(canonical, snapshot.LocalizationDatabase ? "1" : "0");
            AppendTableNames(canonical, snapshot);
            var table = OrderedTables(snapshot).FirstOrDefault(candidate =>
                string.Equals(candidate.Name, tableName, StringComparison.Ordinal));
            Append(canonical, table?.Name);
            var fields = (table?.Fields ?? new List<GameDBFieldSnapshot>())
                .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
            Append(canonical, fields.Length.ToString());
            foreach (var field in fields)
            {
                Append(canonical, field.Name);
            }
        }

        private static void AppendTableNames(StringBuilder canonical,
            GameDBSnapshot snapshot)
        {
            Append(canonical, "table-names");
            var tables = OrderedTables(snapshot).ToArray();
            Append(canonical, tables.Length.ToString());
            foreach (var table in tables)
            {
                Append(canonical, table.Name);
            }
        }


        private static void AppendField(StringBuilder canonical,
            GameDBFieldSnapshot field)
        {
            if (field == null)
            {
                Append(canonical, null);
                return;
            }

            Append(canonical, "field");
            Append(canonical, field.Name);
            Append(canonical, ((int)field.FieldType).ToString());
            Append(canonical, field.IsArray ? "1" : "0");
            Append(canonical, field.TypeArgument);
            var dictionary = field.DictionaryType;
            if (dictionary == null)
            {
                Append(canonical, "no-dictionary");
                return;
            }
            Append(canonical, "dictionary");
            Append(canonical, ((int)dictionary.KeyType).ToString());
            Append(canonical, dictionary.KeyTypeArgument);
            Append(canonical, ((int)dictionary.ValueType).ToString());
            Append(canonical, dictionary.ValueTypeArgument);
        }

        private static GameDBFieldSnapshot FindField(GameDBSnapshot snapshot,
            string tableName, string fieldName)
        {
            var table = OrderedTables(snapshot).FirstOrDefault(candidate =>
                string.Equals(candidate.Name, tableName, StringComparison.Ordinal));
            return (table?.Fields ?? new List<GameDBFieldSnapshot>())
                .FirstOrDefault(field =>
                    string.Equals(field.Name, fieldName, StringComparison.Ordinal));
        }

        private static IEnumerable<GameDBTableSnapshot> OrderedTables(
            GameDBSnapshot snapshot)
        {
            return (snapshot.Tables ?? new List<GameDBTableSnapshot>())
                .OrderBy(table => table.Name, StringComparer.Ordinal);
        }

        private static void Append(StringBuilder canonical, string value)
        {
            if (value == null)
            {
                canonical.Append("-1:");
                return;
            }
            canonical.Append(value.Length).Append(':').Append(value);
        }
    }

    internal sealed class GameDBInspectorTaskState
    {
        internal GameDBInspectorTaskKind Kind { get; }
        internal GameDBInspectorContext Context { get; }
        internal IGameDBInspectorDraft Draft { get; }
        internal GameDBInspectorSchemaFingerprint OpeningFingerprint { get; }
        internal bool IsDirty { get; private set; }
        internal bool IsStale { get; private set; }

        internal GameDBInspectorTaskState(GameDBInspectorTaskKind kind,
            GameDBInspectorContext context, IGameDBInspectorDraft draft,
            GameDBSnapshot openingSnapshot)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            ValidateTask(kind, context, draft);
            Kind = kind;
            OpeningFingerprint = GameDBInspectorSchemaFingerprint.Capture(
                openingSnapshot, kind, context);
        }

        private static void ValidateTask(GameDBInspectorTaskKind kind,
            GameDBInspectorContext context, IGameDBInspectorDraft draft)
        {
            var valid = kind == GameDBInspectorTaskKind.EditDatabase
                    && context.Kind == GameDBInspectorContextKind.Database
                    && draft is GameDBInspectorDatabaseDraft
                || kind == GameDBInspectorTaskKind.CreateTable
                    && context.Kind == GameDBInspectorContextKind.Database
                    && draft is GameDBInspectorTableDraft
                || kind == GameDBInspectorTaskKind.RenameTable
                    && context.Kind == GameDBInspectorContextKind.Table
                    && draft is GameDBInspectorTableDraft
                || kind == GameDBInspectorTaskKind.CreateField
                    && context.Kind == GameDBInspectorContextKind.Table
                    && draft is GameDBInspectorFieldDraft
                || kind == GameDBInspectorTaskKind.RenameField
                    && context.Kind == GameDBInspectorContextKind.Field
                    && draft is GameDBInspectorFieldNameDraft
                || kind == GameDBInspectorTaskKind.ChangeFieldType
                    && context.Kind == GameDBInspectorContextKind.Field
                    && draft is GameDBInspectorFieldTypeDraft;
            if (!valid)
            {
                throw new ArgumentException(
                    $"Inspector task '{kind}' has an incompatible context or draft.");
            }
        }

        internal void MarkDirty()
        {
            IsDirty = true;
        }

        internal bool RecheckStaleness(GameDBSnapshot snapshot)
        {
            IsStale = IsStale || snapshot == null || !OpeningFingerprint.Equals(
                GameDBInspectorSchemaFingerprint.Capture(snapshot, Kind, Context));
            return IsStale;
        }
    }

    internal sealed class GameDBInspectorState
    {
        internal GameDBInspectorContext Context { get; private set; }
        internal GameDBInspectorTaskState Task { get; private set; }
        internal GameDBInspectorPendingIntent PendingIntent { get; private set; }

        internal void SetContext(GameDBInspectorContext context)
        {
            if (Task != null)
            {
                throw new InvalidOperationException(
                    "Cannot replace Inspector context while a task is active.");
            }
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void BeginTask(GameDBInspectorTaskState task)
        {
            if (Task != null)
            {
                throw new InvalidOperationException(
                    "Cannot replace an active Inspector task.");
            }
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Context = task.Context;
            PendingIntent = null;
        }

        internal bool TrySetPendingIntent(GameDBInspectorPendingIntent intent)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }
            if (Task == null)
            {
                throw new InvalidOperationException(
                    "Pending Inspector intents require an active task.");
            }
            if (PendingIntent != null)
            {
                return false;
            }
            PendingIntent = intent;
            return true;
        }

        internal GameDBInspectorPendingIntent TakePendingIntent()
        {
            var intent = PendingIntent;
            PendingIntent = null;
            return intent;
        }

        internal void CancelTask()
        {
            Task = null;
            PendingIntent = null;
        }

        internal void CompleteTask(GameDBInspectorContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Task = null;
            PendingIntent = null;
        }

        internal void Reset()
        {
            Context = null;
            Task = null;
            PendingIntent = null;
        }
    }
}
