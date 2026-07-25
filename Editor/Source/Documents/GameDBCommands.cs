using GameDBLibrary;
using System.Collections.Generic;

namespace GameDBEditorLibrary.Documents
{
    internal enum GameDBCommandKind
    {
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

    internal sealed class GameDBCommandExecution
    {
        internal bool Success { get; }
        internal string Message { get; }

        private GameDBCommandExecution(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        internal static GameDBCommandExecution Succeeded()
        {
            return new GameDBCommandExecution(true, null);
        }

        internal static GameDBCommandExecution Failed(string message)
        {
            return new GameDBCommandExecution(false, message);
        }
    }

    internal sealed class GameDBCommandContext
    {
        internal GameDB Model { get; }

        internal GameDBCommandContext(GameDB model)
        {
            Model = model;
        }
    }

    internal abstract class GameDBCommand
    {
        internal abstract GameDBCommandKind Kind { get; }
        internal abstract bool IsDestructive { get; }
        internal abstract GameDBCommandExecution Execute(GameDBCommandContext context);
    }

    internal sealed class AddTableCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly KeyType m_keyType;
        private readonly string m_keyTypeArgument;

        internal override GameDBCommandKind Kind => GameDBCommandKind.AddTable;
        internal override bool IsDestructive => false;

        internal AddTableCommand(string tableName, KeyType keyType, string keyTypeArgument)
        {
            m_tableName = tableName;
            m_keyType = keyType;
            m_keyTypeArgument = keyTypeArgument;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            GameDBModelOperations.RequireName(m_tableName, nameof(m_tableName));
            var typeArgument = GameDBModelOperations.ResolveKeyTypeArgument(m_keyType, m_keyTypeArgument);
            return context.Model.AddTable(m_tableName, m_keyType, typeArgument)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Table already exists: {m_tableName}");
        }
    }

    internal sealed class RenameTableCommand : GameDBCommand
    {
        private readonly string m_currentName;
        private readonly string m_newName;

        internal override GameDBCommandKind Kind => GameDBCommandKind.RenameTable;
        internal override bool IsDestructive => true;

        internal RenameTableCommand(string currentName, string newName)
        {
            m_currentName = currentName;
            m_newName = newName;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            GameDBModelOperations.RequireName(m_currentName, nameof(m_currentName));
            GameDBModelOperations.RequireName(m_newName, nameof(m_newName));
            if (!context.Model.RenameTable(m_currentName, m_newName))
            {
                return GameDBCommandExecution.Failed(
                    $"Table does not exist or the new name is already used: {m_currentName}");
            }

            GameDBModelOperations.RenameTableReferences(context.Model, m_currentName, m_newName);
            return GameDBCommandExecution.Succeeded();
        }
    }

    internal sealed class DeleteTableCommand : GameDBCommand
    {
        private readonly string m_tableName;

        internal override GameDBCommandKind Kind => GameDBCommandKind.DeleteTable;
        internal override bool IsDestructive => true;

        internal DeleteTableCommand(string tableName)
        {
            m_tableName = tableName;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            GameDBModelOperations.RequireName(m_tableName, nameof(m_tableName));
            var references = GameDBModelOperations.FindTableReferences(context.Model, m_tableName);
            if (references.Count > 0)
            {
                return GameDBCommandExecution.Failed(
                    $"Table is referenced by: {string.Join(", ", references)}. Remove those fields before deleting it.");
            }

            return context.Model.RemoveTable(m_tableName)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Table does not exist: {m_tableName}");
        }
    }

    internal abstract class FieldCommand : GameDBCommand
    {
        protected readonly string TableName;
        protected readonly string FieldName;
        protected readonly GameDBFieldTypeSpec TypeSpec;

        protected FieldCommand(string tableName, string fieldName, GameDBFieldTypeSpec typeSpec)
        {
            TableName = tableName;
            FieldName = fieldName;
            TypeSpec = typeSpec;
        }

        protected GameDBCommandExecution ExecuteChange(GameDBCommandContext context, bool replace)
        {
            GameDBModelOperations.RequireName(FieldName, nameof(FieldName));
            var table = GameDBModelOperations.GetTable(context.Model, TableName);
            var typeArgument = GameDBModelOperations.ResolveFieldTypeArgument(context.Model, TypeSpec);
            var success = replace
                ? table.ReplaceField(FieldName, TypeSpec.FieldType, TypeSpec.IsArray, typeArgument)
                : table.AddField(FieldName, TypeSpec.FieldType, TypeSpec.IsArray, typeArgument);
            return success
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Field does not exist or already exists: {FieldName}");
        }
    }

    internal sealed class AddFieldCommand : FieldCommand
    {
        internal override GameDBCommandKind Kind => GameDBCommandKind.AddField;
        internal override bool IsDestructive => false;

        internal AddFieldCommand(string tableName, string fieldName, GameDBFieldTypeSpec typeSpec)
            : base(tableName, fieldName, typeSpec)
        {
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            return ExecuteChange(context, false);
        }
    }

    internal sealed class ReplaceFieldCommand : FieldCommand
    {
        internal override GameDBCommandKind Kind => GameDBCommandKind.ReplaceField;
        internal override bool IsDestructive => true;

        internal ReplaceFieldCommand(string tableName, string fieldName, GameDBFieldTypeSpec typeSpec)
            : base(tableName, fieldName, typeSpec)
        {
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            return ExecuteChange(context, true);
        }
    }

    internal sealed class RenameFieldCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_currentName;
        private readonly string m_newName;

        internal override GameDBCommandKind Kind => GameDBCommandKind.RenameField;
        internal override bool IsDestructive => true;

        internal RenameFieldCommand(string tableName, string currentName, string newName)
        {
            m_tableName = tableName;
            m_currentName = currentName;
            m_newName = newName;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            GameDBModelOperations.RequireName(m_currentName, nameof(m_currentName));
            GameDBModelOperations.RequireName(m_newName, nameof(m_newName));
            return table.RenameField(m_currentName, m_newName)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed(
                    $"Field does not exist or the new name is already used: {m_currentName}");
        }
    }

    internal sealed class DeleteFieldCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_fieldName;

        internal override GameDBCommandKind Kind => GameDBCommandKind.DeleteField;
        internal override bool IsDestructive => true;

        internal DeleteFieldCommand(string tableName, string fieldName)
        {
            m_tableName = tableName;
            m_fieldName = fieldName;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            GameDBModelOperations.RequireName(m_fieldName, nameof(m_fieldName));
            return table.RemoveField(m_fieldName)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Field does not exist: {m_fieldName}");
        }
    }

    internal sealed class AddRowCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_rowKey;
        private readonly Dictionary<string, object> m_values;

        internal override GameDBCommandKind Kind => GameDBCommandKind.AddRow;
        internal override bool IsDestructive => false;

        internal AddRowCommand(string tableName, string rowKey, IDictionary<string, object> values)
        {
            m_tableName = tableName;
            m_rowKey = rowKey;
            m_values = GameDBModelOperations.CopyWireValues(values);
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            GameDBModelOperations.RequireRowKey(m_rowKey, nameof(m_rowKey));
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            var error = GameDBModelOperations.ValidateValues(table, m_values);
            if (error != null)
            {
                return GameDBCommandExecution.Failed(error);
            }

            if (!table.AddKey(m_rowKey))
            {
                return GameDBCommandExecution.Failed($"Row already exists or the key is empty: {m_rowKey}");
            }

            foreach (var pair in m_values)
            {
                if (!table.SetValue(m_rowKey, pair.Key, pair.Value))
                {
                    return GameDBCommandExecution.Failed($"Value could not be applied to field: {pair.Key}");
                }
            }

            return GameDBCommandExecution.Succeeded();
        }
    }

    internal sealed class UpdateRowCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_rowKey;
        private readonly Dictionary<string, object> m_values;

        internal override GameDBCommandKind Kind => GameDBCommandKind.UpdateRow;
        internal override bool IsDestructive => false;

        internal UpdateRowCommand(string tableName, string rowKey, IDictionary<string, object> values)
        {
            m_tableName = tableName;
            m_rowKey = rowKey;
            m_values = GameDBModelOperations.CopyWireValues(values);
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            if (!table.Data.ContainsKey(m_rowKey))
            {
                return GameDBCommandExecution.Failed($"Row does not exist: {m_rowKey}");
            }

            var error = GameDBModelOperations.ValidateValues(table, m_values);
            if (error != null)
            {
                return GameDBCommandExecution.Failed(error);
            }

            foreach (var pair in m_values)
            {
                if (!table.SetValue(m_rowKey, pair.Key, pair.Value))
                {
                    return GameDBCommandExecution.Failed($"Value could not be applied to field: {pair.Key}");
                }
            }

            return GameDBCommandExecution.Succeeded();
        }
    }

    internal sealed class SetValueCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_rowKey;
        private readonly string m_fieldName;
        private readonly object m_value;

        internal override GameDBCommandKind Kind => GameDBCommandKind.SetValue;
        internal override bool IsDestructive => false;

        internal SetValueCommand(string tableName, string rowKey, string fieldName, object value)
        {
            m_tableName = tableName;
            m_rowKey = rowKey;
            m_fieldName = fieldName;
            m_value = GameDBModelOperations.CopyWireValue(value);
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            if (!table.Fields.TryGetValue(m_fieldName, out var field))
            {
                return GameDBCommandExecution.Failed($"Field does not exist: {m_fieldName}");
            }

            if (!GameDBModelOperations.IsWireValueValid(field, m_value))
            {
                return GameDBCommandExecution.Failed(
                    $"Value is invalid for {m_fieldName}; expected {field.Type}{(field.IsArray ? "[]" : string.Empty)}.");
            }

            return table.SetValue(m_rowKey, m_fieldName, m_value)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Row does not exist: {m_rowKey}");
        }
    }

    internal sealed class RenameRowCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_currentName;
        private readonly string m_newName;

        internal override GameDBCommandKind Kind => GameDBCommandKind.RenameRow;
        internal override bool IsDestructive => true;

        internal RenameRowCommand(string tableName, string currentName, string newName)
        {
            m_tableName = tableName;
            m_currentName = currentName;
            m_newName = newName;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            GameDBModelOperations.RequireRowKey(m_currentName, nameof(m_currentName));
            GameDBModelOperations.RequireRowKey(m_newName, nameof(m_newName));
            if (!table.RenameKey(m_currentName, m_newName))
            {
                return GameDBCommandExecution.Failed(
                    $"Row does not exist or the new key is already used: {m_currentName}");
            }

            GameDBModelOperations.RenameRowReferences(context.Model,
                m_tableName, m_currentName, m_newName);
            return GameDBCommandExecution.Succeeded();
        }
    }

    internal sealed class DeleteRowCommand : GameDBCommand
    {
        private readonly string m_tableName;
        private readonly string m_rowKey;

        internal override GameDBCommandKind Kind => GameDBCommandKind.DeleteRow;
        internal override bool IsDestructive => true;

        internal DeleteRowCommand(string tableName, string rowKey)
        {
            m_tableName = tableName;
            m_rowKey = rowKey;
        }

        internal override GameDBCommandExecution Execute(GameDBCommandContext context)
        {
            var table = GameDBModelOperations.GetTable(context.Model, m_tableName);
            var references = GameDBModelOperations.FindRowReferences(context.Model, m_tableName, m_rowKey);
            if (references.Count > 0)
            {
                return GameDBCommandExecution.Failed(
                    $"Row is referenced by: {string.Join(", ", references)}. Update those values before deleting it.");
            }

            return table.RemoveKey(m_rowKey)
                ? GameDBCommandExecution.Succeeded()
                : GameDBCommandExecution.Failed($"Row does not exist: {m_rowKey}");
        }
    }
}
