using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary.Automation
{
    public static class GameDBAutomationService
    {
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;

        public static GameDBListResult ListDatabases(string searchDirectory = "Assets")
        {
            try
            {
                var directory = ResolveAssetDirectory(searchDirectory, false);
                if (!Directory.Exists(directory.AbsolutePath))
                {
                    return new GameDBListResult { Success = false, Message = $"Directory does not exist: {directory.AssetPath}" };
                }

                var paths = Directory.GetFiles(directory.AbsolutePath, "*.json", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
                    .Where(path => File.Exists(Path.ChangeExtension(path, ".schema.json")))
                    .Select(ToAssetPath)
                    .OrderBy(path => path, NameComparer)
                    .ToList();

                return new GameDBListResult
                {
                    Success = true,
                    Message = $"Found {paths.Count} GameDB database(s).",
                    DatabasePaths = paths
                };
            }
            catch (Exception exception)
            {
                return new GameDBListResult { Success = false, Message = exception.Message };
            }
        }

        public static GameDBAutomationResult Load(string databasePath)
        {
            return Inspect(databasePath);
        }

        public static GameDBAutomationResult Inspect(string databasePath)
        {
            try
            {
                var path = ResolveDatabasePath(databasePath);
                var gameDB = LoadDatabase(path);
                var issues = ValidateModel(gameDB);
                return Success("inspect", path.AssetPath, false, "Database loaded.", gameDB, issues);
            }
            catch (Exception exception)
            {
                return Failure("inspect", databasePath, exception.Message);
            }
        }

        public static GameDBAutomationResult Validate(string databasePath)
        {
            try
            {
                var path = ResolveDatabasePath(databasePath);
                var gameDB = LoadDatabase(path);
                var issues = ValidateModel(gameDB);
                var result = Success("validate", path.AssetPath, false,
                    issues.Count == 0 ? "Database is valid." : $"Database has {issues.Count} validation issue(s).",
                    gameDB, issues);
                result.Success = issues.Count == 0;
                return result;
            }
            catch (Exception exception)
            {
                return Failure("validate", databasePath, exception.Message);
            }
        }

        public static GameDBAutomationResult Create(GameDBCreateRequest request)
        {
            if (request == null)
            {
                return Failure("create", null, "Request is required.");
            }

            try
            {
                var options = request.Options ?? new GameDBOperationOptions();
                var path = ResolveDatabasePath(request.DatabasePath);
                var exists = File.Exists(path.AbsolutePath) || File.Exists(path.SchemaAbsolutePath);
                string revisionBefore = null;
                if (exists && !request.Overwrite)
                {
                    return Failure("create", path.AssetPath, "Database already exists. Set Overwrite and AllowDestructive to replace it.");
                }

                if (exists)
                {
                    if (!File.Exists(path.AbsolutePath) || !File.Exists(path.SchemaAbsolutePath))
                    {
                        return Failure("create", path.AssetPath, "Database data and schema files must both exist before overwrite.");
                    }

                    revisionBefore = ComputeRevision(LoadDatabase(path));
                    var conflict = CheckRevision(options.ExpectedRevision, revisionBefore);
                    if (conflict != null)
                    {
                        return Failure("create", path.AssetPath, conflict);
                    }
                }

                if (exists && !options.AllowDestructive)
                {
                    return DestructiveFailure("create", path.AssetPath);
                }

                RequireName(request.ScopeName, nameof(request.ScopeName));
                var gameDB = new GameDB();
                gameDB.CreateInMemory(path.RelativePath);
                gameDB.ScopeName = request.ScopeName;
                gameDB.LocalizationDB = request.LocalizationDatabase;

                return CompleteMutation("create", path, gameDB, options.DryRun,
                    options.DryRun ? "Database creation validated; no files were written." : "Database created.", revisionBefore);
            }
            catch (Exception exception)
            {
                return Failure("create", request.DatabasePath, exception.Message);
            }
        }

        public static GameDBAutomationResult Save(GameDBSaveRequest request)
        {
            if (request == null)
            {
                return Failure("save", null, "Request is required.");
            }

            try
            {
                var options = request.Options ?? new GameDBOperationOptions();
                var path = ResolveDatabasePath(request.DatabasePath);
                var exists = File.Exists(path.AbsolutePath) || File.Exists(path.SchemaAbsolutePath);
                string revisionBefore = null;
                if (exists)
                {
                    if (!File.Exists(path.AbsolutePath) || !File.Exists(path.SchemaAbsolutePath))
                    {
                        return Failure("save", path.AssetPath, "Database data and schema files must both exist before replacement.");
                    }

                    revisionBefore = ComputeRevision(LoadDatabase(path));
                    var conflict = CheckRevision(options.ExpectedRevision, revisionBefore);
                    if (conflict != null)
                    {
                        return Failure("save", path.AssetPath, conflict);
                    }
                }

                if (exists && !options.AllowDestructive)
                {
                    return DestructiveFailure("save", path.AssetPath);
                }

                if (string.IsNullOrWhiteSpace(request.DataJson) || string.IsNullOrWhiteSpace(request.SchemaJson))
                {
                    return Failure("save", path.AssetPath, "DataJson and SchemaJson are required.");
                }

                var gameDB = new GameDB();
                PrepareTypeResolution();
                if (!gameDB.Import(request.DataJson, request.SchemaJson))
                {
                    return Failure("save", path.AssetPath, "DataJson or SchemaJson could not be imported.");
                }

                gameDB.LoadedPath = path.RelativePath;
                return CompleteMutation("save", path, gameDB, options.DryRun,
                    options.DryRun ? "Database replacement validated; no files were written." : "Database saved.", revisionBefore);
            }
            catch (Exception exception)
            {
                return Failure("save", request.DatabasePath, exception.Message);
            }
        }

        public static GameDBAutomationResult AddTable(GameDBTableRequest request)
        {
            if (request == null)
            {
                return Failure("addTable", null, "Request is required.");
            }

            return Mutate("addTable", request.DatabasePath, request.Options, false, gameDB =>
            {
                RequireName(request.TableName, nameof(request.TableName));
                var typeArgument = ResolveKeyTypeArgument(request.KeyType, request.KeyTypeArgument);
                return gameDB.AddTable(request.TableName, request.KeyType, typeArgument)
                    ? null
                    : $"Table already exists: {request.TableName}";
            });
        }

        public static GameDBAutomationResult RenameTable(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameTable", null, "Request is required.");
            }

            return Mutate("renameTable", request.DatabasePath, request.Options, true, gameDB =>
            {
                RequireName(request.CurrentName, nameof(request.CurrentName));
                RequireName(request.NewName, nameof(request.NewName));
                if (!gameDB.RenameTable(request.CurrentName, request.NewName))
                {
                    return $"Table does not exist or the new name is already used: {request.CurrentName}";
                }

                RenameTableReferences(gameDB, request.CurrentName, request.NewName);
                return null;
            });
        }

        public static GameDBAutomationResult DeleteTable(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteTable", null, "Request is required.");
            }

            return Mutate("deleteTable", request.DatabasePath, request.Options, true, gameDB =>
            {
                RequireName(request.Name, nameof(request.Name));
                var references = FindTableReferences(gameDB, request.Name);
                if (references.Count > 0)
                {
                    return $"Table is referenced by: {string.Join(", ", references)}. Remove those fields before deleting it.";
                }

                return gameDB.RemoveTable(request.Name) ? null : $"Table does not exist: {request.Name}";
            });
        }

        public static GameDBAutomationResult AddField(GameDBFieldRequest request)
        {
            return ChangeField("addField", request, false, (table, typeArgument) =>
                table.AddField(request.FieldName, request.FieldType, request.IsArray, typeArgument));
        }

        public static GameDBAutomationResult ReplaceField(GameDBFieldRequest request)
        {
            return ChangeField("replaceField", request, true, (table, typeArgument) =>
                table.ReplaceField(request.FieldName, request.FieldType, request.IsArray, typeArgument));
        }

        public static GameDBAutomationResult RenameField(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameField", null, "Request is required.");
            }

            return Mutate("renameField", request.DatabasePath, request.Options, true, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                RequireName(request.CurrentName, nameof(request.CurrentName));
                RequireName(request.NewName, nameof(request.NewName));
                return table.RenameField(request.CurrentName, request.NewName)
                    ? null
                    : $"Field does not exist or the new name is already used: {request.CurrentName}";
            });
        }

        public static GameDBAutomationResult DeleteField(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteField", null, "Request is required.");
            }

            return Mutate("deleteField", request.DatabasePath, request.Options, true, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                RequireName(request.Name, nameof(request.Name));
                return table.RemoveField(request.Name) ? null : $"Field does not exist: {request.Name}";
            });
        }

        public static GameDBAutomationResult AddRow(GameDBRowRequest request)
        {
            if (request == null)
            {
                return Failure("addRow", null, "Request is required.");
            }

            return Mutate("addRow", request.DatabasePath, request.Options, false, gameDB =>
            {
                RequireName(request.RowKey, nameof(request.RowKey));
                var table = GetTable(gameDB, request.TableName);
                var values = request.Values ?? new Dictionary<string, object>();
                var error = ValidateValues(table, values);
                if (error != null)
                {
                    return error;
                }

                if (!table.AddKey(request.RowKey))
                {
                    return $"Row already exists or the key is empty: {request.RowKey}";
                }

                foreach (var pair in values)
                {
                    if (!table.SetValue(request.RowKey, pair.Key, pair.Value))
                    {
                        return $"Value could not be applied to field: {pair.Key}";
                    }
                }

                return null;
            });
        }

        public static GameDBAutomationResult UpdateRow(GameDBRowRequest request)
        {
            if (request == null)
            {
                return Failure("updateRow", null, "Request is required.");
            }

            return Mutate("updateRow", request.DatabasePath, request.Options, false, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                if (!table.Data.ContainsKey(request.RowKey))
                {
                    return $"Row does not exist: {request.RowKey}";
                }

                var values = request.Values ?? new Dictionary<string, object>();
                var error = ValidateValues(table, values);
                if (error != null)
                {
                    return error;
                }

                foreach (var pair in values)
                {
                    if (!table.SetValue(request.RowKey, pair.Key, pair.Value))
                    {
                        return $"Value could not be applied to field: {pair.Key}";
                    }
                }

                return null;
            });
        }

        public static GameDBAutomationResult SetValue(GameDBValueRequest request)
        {
            if (request == null)
            {
                return Failure("setValue", null, "Request is required.");
            }

            return Mutate("setValue", request.DatabasePath, request.Options, false, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                if (!table.Fields.TryGetValue(request.FieldName, out var field))
                {
                    return $"Field does not exist: {request.FieldName}";
                }

                if (!IsWireValueValid(field, request.Value))
                {
                    return $"Value is invalid for {request.FieldName}; expected {field.Type}{(field.IsArray ? "[]" : string.Empty)}.";
                }

                return table.SetValue(request.RowKey, request.FieldName, request.Value)
                    ? null
                    : $"Row does not exist: {request.RowKey}";
            });
        }

        public static GameDBAutomationResult RenameRow(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameRow", null, "Request is required.");
            }

            return Mutate("renameRow", request.DatabasePath, request.Options, true, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                RequireName(request.CurrentName, nameof(request.CurrentName));
                RequireName(request.NewName, nameof(request.NewName));
                if (!table.RenameKey(request.CurrentName, request.NewName))
                {
                    return $"Row does not exist or the new key is already used: {request.CurrentName}";
                }

                RenameRowReferences(gameDB, request.TableName, request.CurrentName, request.NewName);
                return null;
            });
        }

        public static GameDBAutomationResult DeleteRow(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteRow", null, "Request is required.");
            }

            return Mutate("deleteRow", request.DatabasePath, request.Options, true, gameDB =>
            {
                var table = GetTable(gameDB, request.TableName);
                var references = FindRowReferences(gameDB, request.TableName, request.Name);
                if (references.Count > 0)
                {
                    return $"Row is referenced by: {string.Join(", ", references)}. Update those values before deleting it.";
                }

                return table.RemoveKey(request.Name) ? null : $"Row does not exist: {request.Name}";
            });
        }

        public static GameDBExportResult ExportJson(string databasePath)
        {
            try
            {
                var path = ResolveDatabasePath(databasePath);
                var gameDB = LoadDatabase(path);
                var issues = ValidateModel(gameDB);
                return new GameDBExportResult
                {
                    Success = issues.Count == 0,
                    DatabasePath = path.AssetPath,
                    Message = issues.Count == 0 ? "JSON exported." : "JSON export contains validation issues.",
                    DataJson = gameDB.SerializeData(),
                    SchemaJson = gameDB.SerializeSchema(),
                    Issues = issues
                };
            }
            catch (Exception exception)
            {
                return new GameDBExportResult { Success = false, DatabasePath = databasePath, Message = exception.Message };
            }
        }

        public static GameDBAutomationResult GenerateCSharp(GameDBGenerateRequest request)
        {
            if (request == null)
            {
                return Failure("generateCSharp", null, "Request is required.");
            }

            try
            {
                var options = request.Options ?? new GameDBOperationOptions();
                var path = ResolveDatabasePath(request.DatabasePath);
                var output = ResolveAssetDirectory(request.OutputDirectory, true);
                var gameDB = LoadDatabase(path);
                var revision = ComputeRevision(gameDB);
                var conflict = CheckRevision(options.ExpectedRevision, revision);
                if (conflict != null)
                {
                    return Failure("generateCSharp", path.AssetPath, conflict);
                }

                var outputScopePath = Path.Combine(output.AbsolutePath, gameDB.ScopeName);
                if (!options.DryRun && Directory.Exists(outputScopePath)
                    && Directory.EnumerateFileSystemEntries(outputScopePath).Any()
                    && !options.AllowDestructive)
                {
                    return DestructiveFailure("generateCSharp", path.AssetPath);
                }

                var issues = ValidateModel(gameDB);
                foreach (var exporterIssue in CSharpExporter.Validate(gameDB, request.IncludeUnityLoader).Select(ToAutomationIssue))
                {
                    if (!issues.Any(issue => issue.Code == exporterIssue.Code
                        && issue.TableName == exporterIssue.TableName
                        && issue.FieldName == exporterIssue.FieldName
                        && issue.RowKey == exporterIssue.RowKey))
                    {
                        issues.Add(exporterIssue);
                    }
                }
                if (issues.Count > 0)
                {
                    var invalid = Success("generateCSharp", path.AssetPath, options.DryRun,
                        $"Code generation blocked by {issues.Count} validation issue(s).", gameDB, issues);
                    invalid.Success = false;
                    return invalid;
                }

                var result = Success("generateCSharp", path.AssetPath, options.DryRun,
                    options.DryRun ? "Code generation validated; no files were written." : "C# classes generated.", gameDB, issues);
                result.RevisionBefore = revision;
                result.RevisionAfter = revision;
                result.ChangedPaths.Add(output.AssetPath.TrimEnd('/') + "/" + gameDB.ScopeName);
                if (!options.DryRun)
                {
                    Directory.CreateDirectory(output.AbsolutePath);
                    new CSharpExporter().Export(output.RelativePath, gameDB, request.IncludeUnityLoader);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }

                return result;
            }
            catch (Exception exception)
            {
                return Failure("generateCSharp", request.DatabasePath, exception.Message);
            }
        }

        private static GameDBAutomationResult ChangeField(string operation, GameDBFieldRequest request, bool destructive,
            Func<TableModel, object, bool> change)
        {
            if (request == null)
            {
                return Failure(operation, null, "Request is required.");
            }

            return Mutate(operation, request.DatabasePath, request.Options, destructive, gameDB =>
            {
                RequireName(request.FieldName, nameof(request.FieldName));
                var table = GetTable(gameDB, request.TableName);
                var typeArgument = ResolveFieldTypeArgument(gameDB, request);
                return change(table, typeArgument)
                    ? null
                    : $"Field does not exist or already exists: {request.FieldName}";
            });
        }

        private static GameDBAutomationResult Mutate(string operation, string databasePath, GameDBOperationOptions options,
            bool destructive, Func<GameDB, string> mutation)
        {
            try
            {
                options = options ?? new GameDBOperationOptions();
                var path = ResolveDatabasePath(databasePath);
                if (destructive && !options.AllowDestructive)
                {
                    return DestructiveFailure(operation, path.AssetPath);
                }

                var gameDB = LoadDatabase(path);
                var revisionBefore = ComputeRevision(gameDB);
                var conflict = CheckRevision(options.ExpectedRevision, revisionBefore);
                if (conflict != null)
                {
                    return Failure(operation, path.AssetPath, conflict);
                }

                var error = mutation(gameDB);
                if (error != null)
                {
                    return Failure(operation, path.AssetPath, error);
                }

                return CompleteMutation(operation, path, gameDB, options.DryRun,
                    options.DryRun ? "Mutation validated; no files were written." : "Mutation saved.", revisionBefore);
            }
            catch (Exception exception)
            {
                return Failure(operation, databasePath, exception.Message);
            }
        }

        private static GameDBAutomationResult CompleteMutation(string operation, DatabasePath path, GameDB gameDB,
            bool dryRun, string message, string revisionBefore)
        {
            var issues = ValidateModel(gameDB);
            var result = Success(operation, path.AssetPath, dryRun, message, gameDB, issues);
            result.RevisionBefore = revisionBefore;
            result.RevisionAfter = ComputeRevision(gameDB);
            result.ChangedPaths.Add(path.AssetPath);
            result.ChangedPaths.Add(path.SchemaAssetPath);
            if (issues.Count > 0)
            {
                result.Success = false;
                result.Message = $"Mutation blocked by {issues.Count} validation issue(s).";
                return result;
            }

            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path.AbsolutePath));
                gameDB.LoadedPath = path.RelativePath;
                if (!gameDB.Save())
                {
                    return Failure(operation, path.AssetPath, "Database could not be saved.");
                }
            }

            return result;
        }

        private static GameDB LoadDatabase(DatabasePath path)
        {
            if (!File.Exists(path.AbsolutePath))
            {
                throw new FileNotFoundException("Database file does not exist.", path.AssetPath);
            }

            if (!File.Exists(path.SchemaAbsolutePath))
            {
                throw new FileNotFoundException("Database schema file does not exist.", path.SchemaAssetPath);
            }

            PrepareTypeResolution();
            var gameDB = new GameDB();
            if (!gameDB.Import(File.ReadAllText(path.AbsolutePath), File.ReadAllText(path.SchemaAbsolutePath)))
            {
                throw new FormatException("Database data or schema could not be imported.");
            }

            gameDB.LoadedPath = path.RelativePath;
            return gameDB;
        }

        private static void PrepareTypeResolution()
        {
            AssemblyExplorer.Instance.Load();
        }

        private static TableModel GetTable(GameDB gameDB, string tableName)
        {
            RequireName(tableName, nameof(tableName));
            if (!gameDB.Tables.TryGetValue(tableName, out var table))
            {
                throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Table does not exist.");
            }

            return (TableModel)table;
        }

        private static string ValidateValues(TableModel table, IDictionary<string, object> values)
        {
            foreach (var pair in values)
            {
                if (!table.Fields.TryGetValue(pair.Key, out var field))
                {
                    return $"Field does not exist: {pair.Key}";
                }

                if (!IsWireValueValid(field, pair.Value))
                {
                    return $"Value is invalid for {pair.Key}; expected {field.Type}{(field.IsArray ? "[]" : string.Empty)}.";
                }
            }

            return null;
        }

        private static bool IsWireValueValid(FieldBase field, object value)
        {
            if (field.Type == FieldType.dictionary)
            {
                return !field.IsArray && IsWireDictionaryValueValid(field.GetTypeArg<DictionaryType>(), value);
            }

            if (field.IsArray)
            {
                if (!(value is List<object> values))
                {
                    return false;
                }

                return values.All(item => IsWireScalarValueValid(field.Type, field.GetTypeArg<object>(), item));
            }

            return IsWireScalarValueValid(field.Type, field.GetTypeArg<object>(), value);
        }

        private static bool IsStoredValueValid(FieldBase field, object value)
        {
            if (field.Type == FieldType.dictionary)
            {
                return !field.IsArray && IsStoredDictionaryValueValid(field.GetTypeArg<DictionaryType>(), value);
            }

            if (field.IsArray)
            {
                if (!(value is IEnumerable values) || value is string || value is IDictionary)
                {
                    return false;
                }

                return values.Cast<object>().All(item => IsStoredScalarValueValid(field.Type, field.GetTypeArg<object>(), item));
            }

            return IsStoredScalarValueValid(field.Type, field.GetTypeArg<object>(), value);
        }

        private static bool IsWireDictionaryValueValid(DictionaryType dictionaryType, object value)
        {
            if (!(value is IDictionary<string, object> dictionary))
            {
                return false;
            }

            return dictionary.All(entry => IsWireDictionaryKeyValid(dictionaryType, entry.Key)
                && IsWireScalarValueValid(dictionaryType.ValueType, dictionaryType.ValueTypeArg, entry.Value));
        }

        private static bool IsStoredDictionaryValueValid(DictionaryType dictionaryType, object value)
        {
            if (!(value is IDictionary dictionary))
            {
                return false;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (!IsStoredDictionaryKeyValid(dictionaryType, entry.Key)
                    || !IsStoredScalarValueValid(dictionaryType.ValueType, dictionaryType.ValueTypeArg, entry.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWireDictionaryKeyValid(DictionaryType dictionaryType, string value)
        {
            return dictionaryType.KeyType == KeyType.@string
                || dictionaryType.KeyType == KeyType.@enum && IsWireEnumValueValid((Type)dictionaryType.KeyTypeArg, value);
        }

        private static bool IsStoredDictionaryKeyValid(DictionaryType dictionaryType, object value)
        {
            return dictionaryType.KeyType == KeyType.@string && value is string
                || dictionaryType.KeyType == KeyType.@enum && IsStoredEnumValueValid((Type)dictionaryType.KeyTypeArg, value);
        }

        private static bool IsWireScalarValueValid(FieldType type, object typeArgument, object value)
        {
            if (value == null)
            {
                return type == FieldType.tableRef;
            }

            switch (type)
            {
                case FieldType.@bool:
                    return value is bool;
                case FieldType.@int:
                    return IsInt32Value(value);
                case FieldType.@float:
                    return IsFiniteNumber(value);
                case FieldType.@string:
                case FieldType.tableRef:
                case FieldType.unityObject:
                case FieldType.color:
                case FieldType.vector2:
                case FieldType.vector3:
                case FieldType.vector4:
                    return value is string;
                case FieldType.@enum:
                    return value is string name && IsWireEnumValueValid((Type)typeArgument, name);
                default:
                    return false;
            }
        }

        private static bool IsStoredScalarValueValid(FieldType type, object typeArgument, object value)
        {
            if (value == null)
            {
                return type == FieldType.tableRef;
            }

            switch (type)
            {
                case FieldType.@bool:
                    return value is bool;
                case FieldType.@int:
                    return IsInt32Value(value);
                case FieldType.@float:
                    return IsFiniteNumber(value);
                case FieldType.@string:
                case FieldType.tableRef:
                case FieldType.unityObject:
                    return value is string;
                case FieldType.@enum:
                    return IsStoredEnumValueValid((Type)typeArgument, value);
                case FieldType.color:
                    return value is GameDBLibrary.Color;
                case FieldType.vector2:
                    return value is GameDBLibrary.Vector2;
                case FieldType.vector3:
                    return value is GameDBLibrary.Vector3;
                case FieldType.vector4:
                    return value is GameDBLibrary.Vector4;
                default:
                    return false;
            }
        }

        private static bool IsWireEnumValueValid(Type enumType, string value)
        {
            return enumType != null && Enum.GetNames(enumType).Contains(value);
        }

        private static bool IsStoredEnumValueValid(Type enumType, object value)
        {
            return enumType != null && value != null && value.GetType() == enumType && Enum.IsDefined(enumType, value);
        }

        private static bool IsInt32Value(object value)
        {
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                var converted = Convert.ToInt64(value);
                return converted >= int.MinValue && converted <= int.MaxValue && Convert.ToDecimal(value) == converted;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsFiniteNumber(object value)
        {
            if (!IsNumber(value))
            {
                return false;
            }

            try
            {
                var converted = Convert.ToDouble(value);
                return !double.IsNaN(converted) && !double.IsInfinity(converted)
                    && converted <= float.MaxValue && converted >= -float.MaxValue;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static object ResolveFieldTypeArgument(GameDB gameDB, GameDBFieldRequest request)
        {
            if (request.FieldType == FieldType.dictionary)
            {
                if (request.IsArray)
                {
                    throw new ArgumentException("Dictionary fields cannot be arrays.");
                }

                if (request.DictionaryType == null)
                {
                    throw new ArgumentException("DictionaryType is required for dictionary fields.");
                }

                var definition = request.DictionaryType;
                if (definition.ValueType == FieldType.dictionary)
                {
                    throw new ArgumentException("Nested dictionary fields are not supported.");
                }

                var keyArgument = ResolveKeyTypeArgument(definition.KeyType, definition.KeyTypeArgument);
                var valueArgument = ResolveSimpleFieldTypeArgument(gameDB, definition.ValueType, definition.ValueTypeArgument);
                return new DictionaryType(definition.KeyType, keyArgument, definition.ValueType, valueArgument);
            }

            return ResolveSimpleFieldTypeArgument(gameDB, request.FieldType, request.TypeArgument);
        }

        private static object ResolveSimpleFieldTypeArgument(GameDB gameDB, FieldType type, string typeArgument)
        {
            switch (type)
            {
                case FieldType.@enum:
                    return ResolveEnumType(typeArgument);
                case FieldType.tableRef:
                    RequireName(typeArgument, nameof(typeArgument));
                    if (!gameDB.Tables.ContainsKey(typeArgument))
                    {
                        throw new ArgumentOutOfRangeException(nameof(typeArgument), typeArgument, "Referenced table does not exist.");
                    }
                    return typeArgument;
                case FieldType.dictionary:
                    throw new ArgumentException("Use DictionaryType to describe dictionary fields.");
                default:
                    if (!string.IsNullOrWhiteSpace(typeArgument))
                    {
                        throw new ArgumentException($"{type} fields do not accept a type argument.");
                    }
                    return null;
            }
        }

        private static object ResolveKeyTypeArgument(KeyType type, string typeArgument)
        {
            if (type == KeyType.@enum)
            {
                return ResolveEnumType(typeArgument);
            }

            if (!string.IsNullOrWhiteSpace(typeArgument))
            {
                throw new ArgumentException("String keys do not accept a type argument.");
            }

            return null;
        }

        private static Type ResolveEnumType(string typeName)
        {
            RequireName(typeName, nameof(typeName));
            PrepareTypeResolution();
            var type = AssemblyExplorer.Instance.GetType(typeName);
            if (type == null || !type.IsEnum)
            {
                throw new ArgumentException($"Public project enum type was not found: {typeName}");
            }

            return type;
        }

        private static List<GameDBValidationIssue> ValidateModel(GameDB gameDB)
        {
            var issues = new List<GameDBValidationIssue>();
            if (string.IsNullOrWhiteSpace(gameDB.ScopeName))
            {
                issues.Add(Issue("scope.empty", "ScopeName is required."));
            }

            foreach (var tablePair in gameDB.Tables.OrderBy(pair => pair.Key, NameComparer))
            {
                var table = (TableModel)tablePair.Value;
                if (string.IsNullOrWhiteSpace(tablePair.Key))
                {
                    issues.Add(Issue("table.name.empty", "Table name is required.", tablePair.Key));
                }

                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, NameComparer))
                {
                    var field = fieldPair.Value;
                    if (string.IsNullOrWhiteSpace(fieldPair.Key))
                    {
                        issues.Add(Issue("field.name.empty", "Field name is required.", tablePair.Key, fieldPair.Key));
                    }

                    foreach (var rowPair in table.Data.OrderBy(pair => pair.Key, NameComparer))
                    {
                        if (!rowPair.Value.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            issues.Add(Issue("field.value.missing", "Row is missing the field value.", tablePair.Key, fieldPair.Key, rowPair.Key));
                        }
                        else if (!IsStoredValueValid(field, value))
                        {
                            issues.Add(Issue("field.value.invalid", $"Value is invalid for {field.Type}{(field.IsArray ? "[]" : string.Empty)}.",
                                tablePair.Key, fieldPair.Key, rowPair.Key));
                        }
                    }

                    ValidateFieldReferences(gameDB, tablePair.Key, fieldPair.Key, field, issues);
                }
            }

            return issues;
        }

        private static void ValidateFieldReferences(GameDB gameDB, string tableName, string fieldName, FieldBase field,
            List<GameDBValidationIssue> issues)
        {
            if (field.Type == FieldType.tableRef)
            {
                ValidateTableReferenceField(gameDB, tableName, fieldName, field.GetTypeArg<string>(), field, issues);
            }
            else if (field.Type == FieldType.dictionary)
            {
                var dictionaryType = field.GetTypeArg<DictionaryType>();
                if (dictionaryType.ValueType == FieldType.tableRef)
                {
                    ValidateDictionaryTableReferences(gameDB, tableName, fieldName,
                        dictionaryType.ValueTypeArg as string, field, issues);
                }
            }
        }

        private static void ValidateTableReferenceField(GameDB gameDB, string tableName, string fieldName,
            string referencedTableName, FieldBase field, List<GameDBValidationIssue> issues)
        {
            if (!gameDB.Tables.TryGetValue(referencedTableName ?? string.Empty, out var referencedTable))
            {
                issues.Add(Issue("tableRef.table.missing", $"Referenced table does not exist: {referencedTableName}", tableName, fieldName));
                return;
            }

            var table = (TableModel)gameDB.Tables[tableName];
            foreach (var rowPair in table.Data)
            {
                if (!rowPair.Value.Data.TryGetValue(fieldName, out var value))
                {
                    continue;
                }

                if (field.IsArray && value is IEnumerable values && !(value is string))
                {
                    foreach (var item in values)
                    {
                        ValidateReferenceValue(referencedTable, item as string, tableName, fieldName, rowPair.Key, issues);
                    }
                }
                else
                {
                    ValidateReferenceValue(referencedTable, value as string, tableName, fieldName, rowPair.Key, issues);
                }
            }
        }

        private static void ValidateDictionaryTableReferences(GameDB gameDB, string tableName, string fieldName,
            string referencedTableName, FieldBase field, List<GameDBValidationIssue> issues)
        {
            if (!gameDB.Tables.TryGetValue(referencedTableName ?? string.Empty, out var referencedTable))
            {
                issues.Add(Issue("tableRef.table.missing", $"Referenced table does not exist: {referencedTableName}", tableName, fieldName));
                return;
            }

            var table = (TableModel)gameDB.Tables[tableName];
            foreach (var rowPair in table.Data)
            {
                if (!rowPair.Value.Data.TryGetValue(fieldName, out var value) || !(value is IDictionary dictionary))
                {
                    continue;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    ValidateReferenceValue(referencedTable, entry.Value as string, tableName, fieldName, rowPair.Key, issues);
                }
            }
        }

        private static void ValidateReferenceValue(TableBase referencedTable, string value, string tableName,
            string fieldName, string rowKey, List<GameDBValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(value) || value == FieldBase.NullRefToken)
            {
                return;
            }

            if (!referencedTable.Data.ContainsKey(value))
            {
                issues.Add(Issue("tableRef.row.missing", $"Referenced row does not exist: {value}", tableName, fieldName, rowKey));
            }
        }

        private static GameDBValidationIssue ToAutomationIssue(CSharpExporter.ValidationIssue issue)
        {
            return Issue(issue.Code, issue.Message, issue.TableName, issue.FieldName, issue.RowKey);
        }

        private static void RenameTableReferences(GameDB gameDB, string oldName, string newName)
        {
            foreach (var table in gameDB.Tables.Values.Cast<TableModel>())
            {
                foreach (var field in table.Fields.Values.Cast<Field>())
                {
                    if (field.Type == FieldType.tableRef && field.GetTypeArg<string>() == oldName)
                    {
                        field.SetTypeArgument(newName);
                    }
                    else if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        if (dictionary.ValueType == FieldType.tableRef && (string)dictionary.ValueTypeArg == oldName)
                        {
                            field.SetTypeArgument(new DictionaryType(dictionary.KeyType, dictionary.KeyTypeArg,
                                dictionary.ValueType, newName));
                        }
                    }
                }
            }
        }

        private static void RenameRowReferences(GameDB gameDB, string referencedTableName, string oldKey, string newKey)
        {
            foreach (var table in gameDB.Tables.Values.Cast<TableModel>())
            {
                foreach (var fieldPair in table.Fields)
                {
                    var field = fieldPair.Value;
                    var directReference = field.Type == FieldType.tableRef && field.GetTypeArg<string>() == referencedTableName;
                    var dictionaryReference = field.Type == FieldType.dictionary
                        && field.GetTypeArg<DictionaryType>().ValueType == FieldType.tableRef
                        && (string)field.GetTypeArg<DictionaryType>().ValueTypeArg == referencedTableName;
                    if (!directReference && !dictionaryReference)
                    {
                        continue;
                    }

                    foreach (var row in table.Data.Values.Cast<RowModel>())
                    {
                        if (!row.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            continue;
                        }

                        if (dictionaryReference && value is IDictionary dictionary)
                        {
                            var keysToUpdate = new List<object>();
                            foreach (DictionaryEntry entry in dictionary)
                            {
                                if (Equals(entry.Value, oldKey))
                                {
                                    keysToUpdate.Add(entry.Key);
                                }
                            }

                            foreach (var key in keysToUpdate)
                            {
                                dictionary[key] = newKey;
                            }
                        }
                        else if (field.IsArray && value is IList values)
                        {
                            for (var index = 0; index < values.Count; index++)
                            {
                                if (Equals(values[index], oldKey))
                                {
                                    values[index] = newKey;
                                }
                            }
                        }
                        else if (Equals(value, oldKey))
                        {
                            row.SetValue(fieldPair.Key, newKey);
                        }
                    }
                }
            }
        }

        private static List<string> FindTableReferences(GameDB gameDB, string tableName)
        {
            var references = new List<string>();
            foreach (var tablePair in gameDB.Tables)
            {
                foreach (var fieldPair in tablePair.Value.Fields)
                {
                    var field = fieldPair.Value;
                    if (field.Type == FieldType.tableRef && field.GetTypeArg<string>() == tableName)
                    {
                        references.Add($"{tablePair.Key}.{fieldPair.Key}");
                    }
                    else if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        if (dictionary.ValueType == FieldType.tableRef && (string)dictionary.ValueTypeArg == tableName)
                        {
                            references.Add($"{tablePair.Key}.{fieldPair.Key}");
                        }
                    }
                }
            }

            return references;
        }

        private static List<string> FindRowReferences(GameDB gameDB, string tableName, string rowKey)
        {
            var references = new List<string>();
            foreach (var tablePair in gameDB.Tables)
            {
                var table = (TableModel)tablePair.Value;
                foreach (var fieldPair in table.Fields)
                {
                    var field = fieldPair.Value;
                    var directReference = field.Type == FieldType.tableRef && field.GetTypeArg<string>() == tableName;
                    var dictionaryReference = field.Type == FieldType.dictionary
                        && field.GetTypeArg<DictionaryType>().ValueType == FieldType.tableRef
                        && (string)field.GetTypeArg<DictionaryType>().ValueTypeArg == tableName;
                    if (!directReference && !dictionaryReference)
                    {
                        continue;
                    }

                    foreach (var dataPair in table.Data)
                    {
                        if (!dataPair.Value.Data.TryGetValue(fieldPair.Key, out var value))
                        {
                            continue;
                        }

                        var found = dictionaryReference && value is IDictionary dictionary
                            ? dictionary.Values.Cast<object>().Any(item => Equals(item, rowKey))
                            : field.IsArray && value is IEnumerable values && !(value is string)
                                ? values.Cast<object>().Any(item => Equals(item, rowKey))
                                : Equals(value, rowKey);
                        if (found)
                        {
                            references.Add($"{tablePair.Key}[{dataPair.Key}].{fieldPair.Key}");
                        }
                    }
                }
            }

            return references;
        }

        private static GameDBSnapshot Snapshot(DatabasePath path, GameDB gameDB)
        {
            var snapshot = new GameDBSnapshot
            {
                DatabasePath = path.AssetPath,
                SchemaPath = path.SchemaAssetPath,
                Revision = ComputeRevision(gameDB),
                ScopeName = gameDB.ScopeName,
                LocalizationDatabase = gameDB.LocalizationDB
            };

            foreach (var tablePair in gameDB.Tables.OrderBy(pair => pair.Key, NameComparer))
            {
                var table = (TableModel)tablePair.Value;
                var tableSnapshot = new GameDBTableSnapshot
                {
                    Name = tablePair.Key,
                    KeyType = table.TableKeyType.KeyType,
                    KeyTypeArgument = TypeArgumentName(table.TableKeyType.TypeArg)
                };

                foreach (var fieldPair in table.Fields.OrderBy(pair => pair.Key, NameComparer))
                {
                    var field = fieldPair.Value;
                    var fieldSnapshot = new GameDBFieldSnapshot
                    {
                        Name = fieldPair.Key,
                        FieldType = field.Type,
                        IsArray = field.IsArray
                    };

                    if (field.Type == FieldType.dictionary)
                    {
                        var dictionary = field.GetTypeArg<DictionaryType>();
                        fieldSnapshot.DictionaryType = new GameDBDictionaryTypeDefinition
                        {
                            KeyType = dictionary.KeyType,
                            KeyTypeArgument = TypeArgumentName(dictionary.KeyTypeArg),
                            ValueType = dictionary.ValueType,
                            ValueTypeArgument = TypeArgumentName(dictionary.ValueTypeArg)
                        };
                    }
                    else
                    {
                        fieldSnapshot.TypeArgument = TypeArgumentName(field.GetTypeArg<object>());
                    }

                    tableSnapshot.Fields.Add(fieldSnapshot);
                }

                foreach (var rowPair in table.Data.OrderBy(pair => pair.Key, NameComparer))
                {
                    tableSnapshot.Rows.Add(new GameDBRowSnapshot
                    {
                        Key = rowPair.Key,
                        Values = new Dictionary<string, object>(rowPair.Value.Data)
                    });
                }

                snapshot.Tables.Add(tableSnapshot);
            }

            return snapshot;
        }

        private static string ComputeRevision(GameDB gameDB)
        {
            return ComputeRevision(gameDB.SerializeSchema(), gameDB.SerializeData());
        }

        private static string ComputeRevision(string schemaJson, string dataJson)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(schemaJson + "\n" + dataJson);
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static string CheckRevision(string expectedRevision, string actualRevision)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision) || string.Equals(expectedRevision, actualRevision, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"Revision conflict. Expected {expectedRevision}, but the database is {actualRevision}. Inspect it again before writing.";
        }

        private static string TypeArgumentName(object value)
        {
            return value is Type type ? type.FullName : value?.ToString();
        }

        private static GameDBValidationIssue Issue(string code, string message, string tableName = null,
            string fieldName = null, string rowKey = null)
        {
            return new GameDBValidationIssue
            {
                Code = code,
                Message = message,
                TableName = tableName,
                FieldName = fieldName,
                RowKey = rowKey
            };
        }

        private static GameDBAutomationResult Success(string operation, string databasePath, bool dryRun,
            string message, GameDB gameDB, List<GameDBValidationIssue> issues)
        {
            var path = ResolveDatabasePath(databasePath);
            return new GameDBAutomationResult
            {
                Success = true,
                DryRun = dryRun,
                Operation = operation,
                DatabasePath = path.AssetPath,
                Message = message,
                Snapshot = Snapshot(path, gameDB),
                Issues = issues ?? new List<GameDBValidationIssue>()
            };
        }

        private static GameDBAutomationResult Failure(string operation, string databasePath, string message)
        {
            return new GameDBAutomationResult
            {
                Success = false,
                Operation = operation,
                DatabasePath = databasePath,
                Message = message
            };
        }

        private static GameDBAutomationResult DestructiveFailure(string operation, string databasePath)
        {
            return Failure(operation, databasePath, "This operation can discard or rename data. Set Options.AllowDestructive to true.");
        }

        private static void RequireName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }
        }

        private static DatabasePath ResolveDatabasePath(string assetPath)
        {
            RequireName(assetPath, nameof(assetPath));
            var normalized = assetPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("DatabasePath must be an Assets-relative path beginning with 'Assets/'.");
            }

            if (!normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("DatabasePath must identify a data .json file, not a schema file.");
            }

            var relative = normalized.Substring("Assets/".Length);
            var absolute = EnsureInsideAssets(Path.Combine(Application.dataPath, relative));
            var schemaAbsolute = Path.ChangeExtension(absolute, ".schema.json");
            return new DatabasePath
            {
                AssetPath = "Assets/" + relative,
                RelativePath = relative,
                AbsolutePath = absolute,
                SchemaAbsolutePath = schemaAbsolute,
                SchemaAssetPath = ToAssetPath(schemaAbsolute)
            };
        }

        private static AssetDirectory ResolveAssetDirectory(string assetPath, bool allowCreate)
        {
            RequireName(assetPath, nameof(assetPath));
            var normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (!normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Directory must be Assets or a child of Assets.");
            }

            var relative = normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized.Substring("Assets/".Length);
            var absolute = EnsureInsideAssets(Path.Combine(Application.dataPath, relative), true);
            if (!allowCreate && !Directory.Exists(absolute))
            {
                return new AssetDirectory { AssetPath = normalized, RelativePath = relative, AbsolutePath = absolute };
            }

            return new AssetDirectory { AssetPath = normalized, RelativePath = relative, AbsolutePath = absolute };
        }

        private static string EnsureInsideAssets(string path, bool allowAssetsRoot = false)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (allowAssetsRoot && string.Equals(fullPath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            var prefix = assetsRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Path resolves outside the project's Assets directory.");
            }

            return fullPath;
        }

        private static string ToAssetPath(string absolutePath)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = EnsureInsideAssets(absolutePath);
            return "Assets/" + fullPath.Substring(assetsRoot.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
        }

        private sealed class DatabasePath
        {
            public string AssetPath;
            public string RelativePath;
            public string AbsolutePath;
            public string SchemaAbsolutePath;
            public string SchemaAssetPath;
        }

        private sealed class AssetDirectory
        {
            public string AssetPath;
            public string RelativePath;
            public string AbsolutePath;
        }
    }
}
