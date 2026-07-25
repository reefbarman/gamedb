using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                var document = GameDBDocument.Load(path.AssetPath);
                return ReadSuccess("inspect", path.AssetPath, "Database loaded.", document);
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
                var document = GameDBDocument.Load(path.AssetPath);
                var issues = document.Validate().ToList();
                return new GameDBAutomationResult
                {
                    Success = issues.Count == 0,
                    Operation = "validate",
                    DatabasePath = path.AssetPath,
                    DryRun = false,
                    Message = issues.Count == 0
                        ? "Database is valid."
                        : $"Database has {issues.Count} validation issue(s).",
                    Snapshot = document.CreateSnapshot(),
                    Issues = issues
                };
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
                GameDBDocument existingDocument = null;
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

                    existingDocument = GameDBDocument.Load(path.AssetPath);
                    revisionBefore = existingDocument.CurrentRevision;
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
                var document = exists
                    ? existingDocument.CreateReplacement(request.ScopeName, request.LocalizationDatabase)
                    : GameDBDocument.CreateNew(path.AssetPath,
                        request.ScopeName, request.LocalizationDatabase);

                return CompleteDocumentMutation("create", path, document, options.DryRun,
                    options.DryRun ? "Database creation validated; no files were written." : "Database created.",
                    revisionBefore);
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
                GameDBDocument existingDocument = null;
                string revisionBefore = null;
                if (exists)
                {
                    if (!File.Exists(path.AbsolutePath) || !File.Exists(path.SchemaAbsolutePath))
                    {
                        return Failure("save", path.AssetPath, "Database data and schema files must both exist before replacement.");
                    }

                    existingDocument = GameDBDocument.Load(path.AssetPath);
                    revisionBefore = existingDocument.CurrentRevision;
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

                GameDBDocument document;
                try
                {
                    document = exists
                        ? existingDocument.CreateReplacement(request.DataJson, request.SchemaJson)
                        : GameDBDocument.CreateNewReplacement(
                            path.AssetPath, request.DataJson, request.SchemaJson);
                }
                catch (FormatException)
                {
                    return Failure("save", path.AssetPath, "DataJson or SchemaJson could not be imported.");
                }

                return CompleteDocumentMutation("save", path, document, options.DryRun,
                    options.DryRun ? "Database replacement validated; no files were written." : "Database saved.",
                    revisionBefore);
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

            return ExecuteCommand("addTable", request.DatabasePath, request.Options,
                () => new AddTableCommand(request.TableName, request.KeyType, request.KeyTypeArgument));
        }

        public static GameDBAutomationResult RenameTable(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameTable", null, "Request is required.");
            }

            return ExecuteCommand("renameTable", request.DatabasePath, request.Options,
                () => new RenameTableCommand(request.CurrentName, request.NewName));
        }

        public static GameDBAutomationResult DeleteTable(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteTable", null, "Request is required.");
            }

            return ExecuteCommand("deleteTable", request.DatabasePath, request.Options,
                () => new DeleteTableCommand(request.Name));
        }

        public static GameDBAutomationResult AddField(GameDBFieldRequest request)
        {
            if (request == null)
            {
                return Failure("addField", null, "Request is required.");
            }

            return ExecuteCommand("addField", request.DatabasePath, request.Options,
                () => new AddFieldCommand(request.TableName, request.FieldName, CreateFieldTypeSpec(request)));
        }

        public static GameDBAutomationResult ReplaceField(GameDBFieldRequest request)
        {
            if (request == null)
            {
                return Failure("replaceField", null, "Request is required.");
            }

            return ExecuteCommand("replaceField", request.DatabasePath, request.Options,
                () => new ReplaceFieldCommand(request.TableName, request.FieldName, CreateFieldTypeSpec(request)));
        }

        public static GameDBAutomationResult RenameField(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameField", null, "Request is required.");
            }

            return ExecuteCommand("renameField", request.DatabasePath, request.Options,
                () => new RenameFieldCommand(request.TableName, request.CurrentName, request.NewName));
        }

        public static GameDBAutomationResult DeleteField(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteField", null, "Request is required.");
            }

            return ExecuteCommand("deleteField", request.DatabasePath, request.Options,
                () => new DeleteFieldCommand(request.TableName, request.Name));
        }

        public static GameDBAutomationResult AddRow(GameDBRowRequest request)
        {
            if (request == null)
            {
                return Failure("addRow", null, "Request is required.");
            }

            return ExecuteCommand("addRow", request.DatabasePath, request.Options,
                () => new AddRowCommand(request.TableName, request.RowKey, request.Values));
        }

        public static GameDBAutomationResult UpdateRow(GameDBRowRequest request)
        {
            if (request == null)
            {
                return Failure("updateRow", null, "Request is required.");
            }

            return ExecuteCommand("updateRow", request.DatabasePath, request.Options,
                () => new UpdateRowCommand(request.TableName, request.RowKey, request.Values));
        }

        public static GameDBAutomationResult SetValue(GameDBValueRequest request)
        {
            if (request == null)
            {
                return Failure("setValue", null, "Request is required.");
            }

            return ExecuteCommand("setValue", request.DatabasePath, request.Options,
                () => new SetValueCommand(request.TableName, request.RowKey, request.FieldName, request.Value));
        }

        public static GameDBAutomationResult RenameRow(GameDBRenameRequest request)
        {
            if (request == null)
            {
                return Failure("renameRow", null, "Request is required.");
            }

            return ExecuteCommand("renameRow", request.DatabasePath, request.Options,
                () => new RenameRowCommand(request.TableName, request.CurrentName, request.NewName));
        }

        public static GameDBAutomationResult DeleteRow(GameDBDeleteRequest request)
        {
            if (request == null)
            {
                return Failure("deleteRow", null, "Request is required.");
            }

            return ExecuteCommand("deleteRow", request.DatabasePath, request.Options,
                () => new DeleteRowCommand(request.TableName, request.Name));
        }

        public static GameDBExportResult ExportJson(string databasePath)
        {
            try
            {
                var path = ResolveDatabasePath(databasePath);
                var document = GameDBDocument.Load(path.AssetPath);
                var issues = document.Validate().ToList();
                var state = document.SerializeCurrent();
                return new GameDBExportResult
                {
                    Success = issues.Count == 0,
                    DatabasePath = path.AssetPath,
                    Message = issues.Count == 0 ? "JSON exported." : "JSON export contains validation issues.",
                    DataJson = state.DataJson,
                    SchemaJson = state.SchemaJson,
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
                var document = GameDBDocument.Load(path.AssetPath);
                var revision = document.CurrentRevision;
                var conflict = CheckRevision(options.ExpectedRevision, revision);
                if (conflict != null)
                {
                    return Failure("generateCSharp", path.AssetPath, conflict);
                }

                var gameDB = document.CreateDetachedModel();
                var outputScopePath = Path.Combine(output.AbsolutePath, gameDB.ScopeName);
                if (!options.DryRun && Directory.Exists(outputScopePath)
                    && Directory.EnumerateFileSystemEntries(outputScopePath).Any()
                    && !options.AllowDestructive)
                {
                    return DestructiveFailure("generateCSharp", path.AssetPath);
                }

                var issues = document.Validate().ToList();
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

                var result = new GameDBAutomationResult
                {
                    Success = issues.Count == 0,
                    Operation = "generateCSharp",
                    DatabasePath = path.AssetPath,
                    DryRun = options.DryRun,
                    Message = issues.Count == 0
                        ? options.DryRun
                            ? "Code generation validated; no files were written."
                            : "C# classes generated."
                        : $"Code generation blocked by {issues.Count} validation issue(s).",
                    Snapshot = document.CreateSnapshot(),
                    Issues = issues
                };
                if (issues.Count > 0)
                {
                    return result;
                }

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

        private static GameDBAutomationResult ExecuteCommand(string operation, string databasePath,
            GameDBOperationOptions options, Func<GameDBCommand> commandFactory)
        {
            try
            {
                options = options ?? new GameDBOperationOptions();
                var path = ResolveDatabasePath(databasePath);
                var command = commandFactory();
                if (command.IsDestructive && !options.AllowDestructive)
                {
                    return DestructiveFailure(operation, path.AssetPath);
                }

                var document = GameDBDocument.Load(path.AssetPath);
                var transaction = document.ApplyTransaction(new[] { command }, new GameDBTransactionOptions
                {
                    ExpectedRevision = options.ExpectedRevision,
                    AllowedDestructiveOperations = options.AllowDestructive
                        ? new[] { command.Kind }
                        : Array.Empty<GameDBCommandKind>()
                });
                if (!transaction.Success)
                {
                    if (transaction.FailureKind == GameDBTransactionFailureKind.ValidationFailed)
                    {
                        return TransactionValidationFailure(operation, path, options.DryRun, transaction);
                    }

                    if (transaction.FailureKind == GameDBTransactionFailureKind.RevisionConflict)
                    {
                        return Failure(operation, path.AssetPath,
                            CheckRevision(options.ExpectedRevision, transaction.RevisionBefore));
                    }

                    return Failure(operation, path.AssetPath, transaction.Message);
                }

                var result = TransactionSuccess(operation, path, options.DryRun, transaction);
                if (!options.DryRun)
                {
                    var save = document.Save(new GameDBSaveOptions { ForceWrite = true });
                    if (!save.Success)
                    {
                        return Failure(operation, path.AssetPath, "Database could not be saved.");
                    }
                }

                return result;
            }
            catch (Exception exception)
            {
                return Failure(operation, databasePath, exception.Message);
            }
        }

        private static GameDBAutomationResult TransactionSuccess(string operation, DatabasePath path,
            bool dryRun, GameDBTransactionResult transaction)
        {
            return new GameDBAutomationResult
            {
                Success = true,
                Operation = operation,
                DatabasePath = path.AssetPath,
                DryRun = dryRun,
                Message = dryRun ? "Mutation validated; no files were written." : "Mutation saved.",
                RevisionBefore = transaction.RevisionBefore,
                RevisionAfter = transaction.AttemptedRevision,
                Snapshot = transaction.AttemptedSnapshot,
                Issues = transaction.Issues.ToList(),
                ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath }
            };
        }

        private static GameDBAutomationResult TransactionValidationFailure(string operation, DatabasePath path,
            bool dryRun, GameDBTransactionResult transaction)
        {
            return new GameDBAutomationResult
            {
                Success = false,
                Operation = operation,
                DatabasePath = path.AssetPath,
                DryRun = dryRun,
                Message = $"Mutation blocked by {transaction.Issues.Count} validation issue(s).",
                RevisionBefore = transaction.RevisionBefore,
                RevisionAfter = transaction.AttemptedRevision,
                Snapshot = transaction.AttemptedSnapshot,
                Issues = transaction.Issues.ToList(),
                ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath }
            };
        }

        private static GameDBFieldTypeSpec CreateFieldTypeSpec(GameDBFieldRequest request)
        {
            GameDBDictionaryTypeSpec dictionaryType = null;
            if (request.DictionaryType != null)
            {
                dictionaryType = new GameDBDictionaryTypeSpec(
                    request.DictionaryType.KeyType,
                    request.DictionaryType.KeyTypeArgument,
                    request.DictionaryType.ValueType,
                    request.DictionaryType.ValueTypeArgument);
            }

            return new GameDBFieldTypeSpec(request.FieldType, request.IsArray,
                request.TypeArgument, dictionaryType);
        }


        private static GameDBAutomationResult CompleteDocumentMutation(string operation, DatabasePath path,
            GameDBDocument document, bool dryRun, string message, string revisionBefore)
        {
            var issues = document.Validate().ToList();
            var result = new GameDBAutomationResult
            {
                Success = true,
                Operation = operation,
                DatabasePath = path.AssetPath,
                DryRun = dryRun,
                Message = message,
                RevisionBefore = revisionBefore,
                RevisionAfter = document.CurrentRevision,
                Snapshot = document.CreateSnapshot(),
                Issues = issues,
                ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath }
            };
            if (issues.Count > 0)
            {
                result.Success = false;
                result.Message = $"Mutation blocked by {issues.Count} validation issue(s).";
                return result;
            }

            if (!dryRun)
            {
                var save = document.Save(new GameDBSaveOptions { ForceWrite = true });
                if (!save.Success)
                {
                    return Failure(operation, path.AssetPath, "Database could not be saved.");
                }
            }

            return result;
        }

        private static GameDBAutomationResult ReadSuccess(string operation, string databasePath,
            string message, GameDBDocument document)
        {
            return new GameDBAutomationResult
            {
                Success = true,
                Operation = operation,
                DatabasePath = databasePath,
                DryRun = false,
                Message = message,
                Snapshot = document.CreateSnapshot(),
                Issues = document.Validate().ToList()
            };
        }

        private static GameDBValidationIssue ToAutomationIssue(CSharpExporter.ValidationIssue issue)
        {
            return Issue(issue.Code, issue.Message, issue.TableName, issue.FieldName, issue.RowKey);
        }

        private static string CheckRevision(string expectedRevision, string actualRevision)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision) || string.Equals(expectedRevision, actualRevision, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"Revision conflict. Expected {expectedRevision}, but the database is {actualRevision}. Inspect it again before writing.";
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
