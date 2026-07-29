using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;
using WorkspaceProjectSettingsResult
    = GameDBEditorLibrary.Workspace.GameDBProjectSettingsResult;
using WorkspaceProjectSettingsSnapshot
    = GameDBEditorLibrary.Workspace.GameDBProjectSettingsSnapshot;

namespace GameDBEditorLibrary.Automation
{
    public static class GameDBAutomationService
    {
        private const string ProjectSettingsPath = "ProjectSettings/GameDBSettings.json";
        private static readonly StringComparer NameComparer = StringComparer.Ordinal;

        public static GameDBProjectSettingsResult InspectProjectSettings()
        {
            try
            {
                return InspectProjectSettings(GameDBEditorDomainServices.ProjectSettings);
            }
            catch (Exception exception)
            {
                return ProjectSettingsFailure(false, exception.Message);
            }
        }

        internal static GameDBProjectSettingsResult InspectProjectSettings(
            GameDBProjectSettingsService service)
        {
            var loaded = service.Refresh();
            return ProjectSettingsResult(loaded, false, null);
        }

        public static GameDBProjectSettingsResult UpdateProjectSettings(
            GameDBProjectSettingsRequest request)
        {
            try
            {
                return UpdateProjectSettings(request,
                    GameDBEditorDomainServices.ProjectSettings);
            }
            catch (Exception exception)
            {
                return ProjectSettingsFailure(request?.Options?.DryRun ?? false,
                    exception.Message);
            }
        }

        internal static GameDBProjectSettingsResult UpdateProjectSettings(
            GameDBProjectSettingsRequest request, GameDBProjectSettingsService service)
        {
            if (request == null)
            {
                return ProjectSettingsFailure(false, "Request is required.");
            }

            var options = request.Options ?? new GameDBProjectSettingsOptions();
            if (request.RegisteredDatabasePaths == null
                || request.ImportedEnumTypeNames == null
                || request.ExportPath == null
                || request.BuildPath == null)
            {
                return ProjectSettingsFailure(options.DryRun,
                    "RegisteredDatabasePaths, ImportedEnumTypeNames, ExportPath, and BuildPath are required. Use empty collections or strings to clear values.");
            }

            var loaded = service.Refresh();
            if (!loaded.Success)
            {
                return ProjectSettingsResult(loaded, options.DryRun, null);
            }
            if (!string.IsNullOrWhiteSpace(options.ExpectedRevision)
                && !string.Equals(options.ExpectedRevision, loaded.Snapshot.Revision,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new GameDBProjectSettingsResult
                {
                    Success = false,
                    DryRun = options.DryRun,
                    CommitStatus = GameDBCommitStatus.Conflict,
                    Message = $"Revision conflict. Expected '{options.ExpectedRevision}', current revision is '{loaded.Snapshot.Revision}'.",
                    RevisionBefore = loaded.Snapshot.Revision,
                    RevisionAfter = loaded.Snapshot.Revision,
                    Snapshot = ToAutomationSnapshot(loaded.Snapshot),
                    Issues = ToAutomationIssues(loaded.Snapshot)
                };
            }

            var registeredPaths = new List<string>();
            foreach (var registeredPath in request.RegisteredDatabasePaths)
            {
                try
                {
                    registeredPaths.Add(ResolveDatabasePath(registeredPath).RelativePath);
                }
                catch (Exception exception)
                {
                    return ProjectSettingsValidationFailure(options.DryRun, loaded.Snapshot,
                        GameDBProjectSettingsIssueKind.InvalidDatabasePath,
                        registeredPath, exception.Message);
                }
            }

            string exportPath;
            try
            {
                exportPath = ResolveOptionalAssetDirectory(request.ExportPath);
            }
            catch (Exception exception)
            {
                return ProjectSettingsValidationFailure(options.DryRun, loaded.Snapshot,
                    GameDBProjectSettingsIssueKind.InvalidExportPath,
                    request.ExportPath, exception.Message);
            }

            string buildPath;
            try
            {
                buildPath = ResolveOptionalAssetDirectory(request.BuildPath);
            }
            catch (Exception exception)
            {
                return ProjectSettingsValidationFailure(options.DryRun, loaded.Snapshot,
                    GameDBProjectSettingsIssueKind.InvalidBuildPath,
                    request.BuildPath, exception.Message);
            }

            var updated = service.Update(registeredPaths,
                request.ImportedEnumTypeNames ?? new List<string>(), exportPath, buildPath,
                options.DryRun, options.ExpectedRevision, options.RequireValid);
            return ProjectSettingsResult(updated, options.DryRun, loaded.Snapshot.Revision);
        }

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

        public static GameDBQueryResult Query(GameDBQueryRequest request)
        {
            if (request == null)
            {
                return GameDBQueryEngine.Failure(null, GameDBQueryFailureKind.InvalidRequest,
                    "request.required", "Request is required.");
            }

            DatabasePath path;
            try
            {
                path = ResolveDatabasePath(request.DatabasePath);
            }
            catch (Exception exception)
            {
                return GameDBQueryEngine.Failure(request.DatabasePath,
                    GameDBQueryFailureKind.InvalidPath, "path.invalid", exception.Message);
            }

            var preflight = GameDBQueryEngine.Preflight(path.AssetPath, request);
            if (preflight != null)
            {
                return preflight;
            }

            GameDBDocument document;
            try
            {
                document = GameDBDocument.Load(path.AssetPath);
            }
            catch (GameDBRecoveryRequiredException exception)
            {
                var result = GameDBQueryEngine.Failure(path.AssetPath,
                    GameDBQueryFailureKind.RecoveryRequired, "database.recoveryRequired",
                    exception.Message);
                result.RecoveryArtifacts = exception.Artifacts.ToList();
                return result;
            }
            catch (Exception exception)
            {
                return GameDBQueryEngine.Failure(path.AssetPath,
                    GameDBQueryFailureKind.LoadFailed, "database.loadFailed", exception.Message);
            }

            GameDBSnapshot snapshot;
            try
            {
                snapshot = document.CreateSnapshot();
            }
            catch (Exception exception)
            {
                return GameDBQueryEngine.Failure(path.AssetPath,
                    GameDBQueryFailureKind.EvaluationFailed, "query.snapshotFailed", exception.Message,
                    document.BaselineRevision);
            }

            return GameDBQueryEngine.Execute(path.AssetPath, snapshot, request);
        }

        public static GameDBCsvExportResult ExportCsv(GameDBCsvExportRequest request)
        {
            if (request == null)
            {
                return CsvExportFailure(null, null, GameDBCsvFailureKind.InvalidRequest,
                    "csv.requestRequired", "Request is required.");
            }

            DatabasePath path;
            try
            {
                path = ResolveDatabasePath(request.DatabasePath);
            }
            catch (Exception exception)
            {
                return CsvExportFailure(request.DatabasePath, request.TableName,
                    GameDBCsvFailureKind.InvalidPath, "csv.pathInvalid", exception.Message);
            }

            if (string.IsNullOrWhiteSpace(request.TableName))
            {
                return CsvExportFailure(path.AssetPath, request.TableName,
                    GameDBCsvFailureKind.InvalidRequest, "csv.tableRequired",
                    "TableName is required.");
            }

            try
            {
                var document = GameDBDocument.Load(path.AssetPath);
                return GameDBCsvEngine.Export(path.AssetPath, document.CreateSnapshot(),
                    request.TableName, document.Validate());
            }
            catch (GameDBRecoveryRequiredException exception)
            {
                var result = CsvExportFailure(path.AssetPath, request.TableName,
                    GameDBCsvFailureKind.RecoveryRequired, "csv.recoveryRequired",
                    exception.Message);
                result.RecoveryArtifacts = exception.Artifacts.ToList();
                return result;
            }
            catch (Exception exception)
            {
                return CsvExportFailure(path.AssetPath, request.TableName,
                    GameDBCsvFailureKind.LoadFailed, "csv.loadFailed", exception.Message);
            }
        }

        public static GameDBCsvImportResult ImportCsv(GameDBCsvImportRequest request)
        {
            if (request == null)
            {
                return CsvImportFailure(null, null, GameDBCsvImportMode.Unspecified, false,
                    GameDBCsvFailureKind.InvalidRequest, "csv.requestRequired",
                    "Request is required.");
            }

            var options = request.Options ?? new GameDBOperationOptions();
            DatabasePath path;
            try
            {
                path = ResolveDatabasePath(request.DatabasePath);
            }
            catch (Exception exception)
            {
                return CsvImportFailure(request.DatabasePath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.InvalidPath,
                    "csv.pathInvalid", exception.Message);
            }

            if (string.IsNullOrWhiteSpace(request.TableName))
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.InvalidRequest,
                    "csv.tableRequired", "TableName is required.");
            }
            if (request.CsvText == null)
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.InvalidRequest,
                    "csv.textRequired", "CsvText is required.");
            }
            if (!Enum.IsDefined(typeof(GameDBCsvImportMode), request.Mode)
                || request.Mode == GameDBCsvImportMode.Unspecified)
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.InvalidRequest,
                    "csv.modeInvalid", $"Unsupported CSV import mode: {request.Mode}.");
            }
            if (request.Mode == GameDBCsvImportMode.Replace && !options.AllowDestructive)
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.AuthorizationDenied,
                    "csv.destructiveDenied",
                    "Replace mode can discard rows. Set Options.AllowDestructive to true.");
            }

            GameDBDocument document;
            try
            {
                document = GameDBDocument.Load(path.AssetPath);
            }
            catch (GameDBRecoveryRequiredException exception)
            {
                var recovery = CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.RecoveryRequired,
                    "csv.recoveryRequired", exception.Message);
                recovery.RecoveryArtifacts = exception.Artifacts.ToList();
                return recovery;
            }
            catch (Exception exception)
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.LoadFailed,
                    "csv.loadFailed", exception.Message);
            }

            GameDBSnapshot snapshot;
            try
            {
                snapshot = document.CreateSnapshot();
            }
            catch (Exception exception)
            {
                return CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, GameDBCsvFailureKind.LoadFailed,
                    "csv.snapshotFailed", exception.Message);
            }

            var plan = GameDBCsvEngine.PrepareImport(snapshot, request);
            if (!plan.Success)
            {
                var failure = CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                    options.DryRun, plan.FailureKind, null, plan.Message);
                failure.Errors = plan.Errors;
                failure.RevisionBefore = snapshot.Revision;
                return failure;
            }

            GameDBCommand command = request.Mode == GameDBCsvImportMode.Replace
                ? (GameDBCommand)new ReplaceTableRowsCommand(request.TableName, plan.Rows)
                : new UpsertTableRowsCommand(request.TableName, plan.Rows);
            var transaction = document.ApplyTransaction(new[] { command },
                new GameDBTransactionOptions
                {
                    ExpectedRevision = options.ExpectedRevision,
                    AllowedDestructiveOperations = request.Mode == GameDBCsvImportMode.Replace
                        ? new[] { GameDBCommandKind.ReplaceTableRows }
                        : Array.Empty<GameDBCommandKind>()
                });
            if (!transaction.Success)
            {
                return CsvTransactionFailure(path, request, options.DryRun,
                    plan, transaction);
            }

            var result = new GameDBCsvImportResult
            {
                Success = true,
                DryRun = options.DryRun,
                FailureKind = GameDBCsvFailureKind.None,
                CommitStatus = options.DryRun
                    ? GameDBCsvCommitStatus.DryRun
                    : GameDBCsvCommitStatus.NotAttempted,
                DatabasePath = path.AssetPath,
                TableName = request.TableName,
                Message = options.DryRun
                    ? "CSV import validated; no files were written."
                    : "CSV imported.",
                Mode = request.Mode,
                RevisionBefore = transaction.RevisionBefore,
                RevisionAfter = transaction.AttemptedRevision,
                Snapshot = transaction.AttemptedSnapshot,
                ImportedRowCount = plan.Rows.Count,
                Issues = transaction.Issues.ToList(),
                ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath }
            };
            if (options.DryRun)
            {
                return result;
            }

            GameDBSaveOutcome save;
            try
            {
                save = document.Save();
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.FailureKind = GameDBCsvFailureKind.CommitFailed;
                result.Message = exception.Message;
                return result;
            }

            result.CommitStatus = ToCsvCommitStatus(save.Status);
            result.FilesCommitted = save.FilesCommitted;
            result.PostSavePending = save.PostSavePending;
            result.PostSaveErrors = save.PostSaveErrors.ToList();
            result.RecoveryArtifacts = save.RecoveryArtifacts.ToList();
            result.ChangedPaths = save.ChangedPaths.ToList();
            result.Message = save.Message;
            if (!save.Success)
            {
                result.Success = false;
                result.FailureKind = GameDBCsvFailureKind.CommitFailed;
            }
            return result;
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
                catch (GameDBSchemaFormatException exception)
                {
                    return Failure("save", path.AssetPath, exception.Message);
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

        public static GameDBBatchResult ApplyBatch(GameDBBatchRequest request)
        {
            if (request == null)
            {
                return BatchFailure(null, false, GameDBBatchFailureKind.InvalidRequest,
                    "Request is required.");
            }

            var options = request.Options ?? new GameDBBatchOptions();
            try
            {
                var path = ResolveDatabasePath(request.DatabasePath);
                if (request.Operations == null || request.Operations.Count == 0)
                {
                    return BatchFailure(path.AssetPath, options.DryRun,
                        GameDBBatchFailureKind.InvalidRequest,
                        "At least one batch operation is required.");
                }

                var operations = request.Operations.ToArray();
                var allowed = options.AllowedDestructiveOperations == null
                    ? new HashSet<GameDBBatchOperationKind>()
                    : new HashSet<GameDBBatchOperationKind>(options.AllowedDestructiveOperations);
                foreach (var allowedKind in allowed)
                {
                    if (!Enum.IsDefined(typeof(GameDBBatchOperationKind), allowedKind)
                        || allowedKind == GameDBBatchOperationKind.Unspecified)
                    {
                        return BatchFailure(path.AssetPath, options.DryRun,
                            GameDBBatchFailureKind.InvalidRequest,
                            $"Unsupported destructive batch operation kind: {allowedKind}.");
                    }
                }

                for (var index = 0; index < operations.Length; index++)
                {
                    var error = ValidateBatchOperation(operations[index]);
                    if (error != null)
                    {
                        return BatchFailure(path.AssetPath, options.DryRun,
                            GameDBBatchFailureKind.InvalidRequest, error, index);
                    }

                    if (IsDestructiveBatchOperation(operations[index].Kind)
                        && !allowed.Contains(operations[index].Kind))
                    {
                        return BatchFailure(path.AssetPath, options.DryRun,
                            GameDBBatchFailureKind.AuthorizationDenied,
                            $"Destructive batch operation is not authorized: {operations[index].Kind}.",
                            index, operations[index].Kind);
                    }
                }

                var commands = new GameDBCommand[operations.Length];
                for (var index = 0; index < operations.Length; index++)
                {
                    try
                    {
                        commands[index] = CreateBatchCommand(operations[index]);
                    }
                    catch (Exception exception)
                    {
                        return BatchFailure(path.AssetPath, options.DryRun,
                            GameDBBatchFailureKind.InvalidRequest, exception.Message, index);
                    }
                }

                GameDBDocument document;
                try
                {
                    document = GameDBDocument.Load(path.AssetPath);
                }
                catch (GameDBRecoveryRequiredException exception)
                {
                    var recovery = BatchFailure(path.AssetPath, options.DryRun,
                        GameDBBatchFailureKind.RecoveryRequired, exception.Message);
                    recovery.RecoveryArtifacts = exception.Artifacts.ToList();
                    return recovery;
                }
                catch (Exception exception)
                {
                    return BatchFailure(path.AssetPath, options.DryRun,
                        GameDBBatchFailureKind.LoadFailed, exception.Message);
                }

                var transaction = document.ApplyTransaction(commands, new GameDBTransactionOptions
                {
                    ExpectedRevision = options.ExpectedRevision,
                    AllowedDestructiveOperations = allowed.Select(ToCommandKind).ToArray()
                });
                if (!transaction.Success)
                {
                    return BatchTransactionFailure(path, options.DryRun, transaction);
                }

                var result = new GameDBBatchResult
                {
                    Success = true,
                    DryRun = options.DryRun,
                    FailureKind = GameDBBatchFailureKind.None,
                    CommitStatus = options.DryRun
                        ? GameDBBatchCommitStatus.DryRun
                        : GameDBBatchCommitStatus.NotAttempted,
                    Operation = "applyBatch",
                    DatabasePath = path.AssetPath,
                    Message = options.DryRun
                        ? "Batch validated; no files were written."
                        : "Batch applied.",
                    RevisionBefore = transaction.RevisionBefore,
                    RevisionAfter = transaction.AttemptedRevision,
                    Snapshot = transaction.AttemptedSnapshot,
                    Issues = transaction.Issues.ToList(),
                    ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath }
                };
                if (options.DryRun)
                {
                    return result;
                }

                GameDBSaveOutcome save;
                try
                {
                    save = document.Save();
                }
                catch (Exception exception)
                {
                    result.Success = false;
                    result.FailureKind = GameDBBatchFailureKind.CommitFailed;
                    result.Message = exception.Message;
                    return result;
                }

                result.CommitStatus = ToBatchCommitStatus(save.Status);
                result.FilesCommitted = save.FilesCommitted;
                result.PostSavePending = save.PostSavePending;
                result.PostSaveErrors = save.PostSaveErrors.ToList();
                result.RecoveryArtifacts = save.RecoveryArtifacts.ToList();
                result.ChangedPaths = save.ChangedPaths.ToList();
                result.Message = save.Message;
                if (!save.Success)
                {
                    result.Success = false;
                    result.FailureKind = GameDBBatchFailureKind.CommitFailed;
                }

                return result;
            }
            catch (Exception exception)
            {
                return BatchFailure(request.DatabasePath, options.DryRun,
                    GameDBBatchFailureKind.InvalidRequest, exception.Message);
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

        private static string ValidateBatchOperation(GameDBBatchOperation operation)
        {
            if (operation == null)
            {
                return "Batch operations cannot contain null entries.";
            }

            if (!Enum.IsDefined(typeof(GameDBBatchOperationKind), operation.Kind)
                || operation.Kind == GameDBBatchOperationKind.Unspecified)
            {
                return $"Unsupported batch operation kind: {operation.Kind}.";
            }

            var payloadCount = (operation.Table != null ? 1 : 0)
                + (operation.Rename != null ? 1 : 0)
                + (operation.Delete != null ? 1 : 0)
                + (operation.Field != null ? 1 : 0)
                + (operation.Row != null ? 1 : 0)
                + (operation.Value != null ? 1 : 0);
            if (payloadCount != 1)
            {
                return "Each batch operation must contain exactly one payload.";
            }

            var expectedPayloadPresent = operation.Kind == GameDBBatchOperationKind.AddTable
                ? operation.Table != null
                : operation.Kind == GameDBBatchOperationKind.RenameTable
                    || operation.Kind == GameDBBatchOperationKind.RenameField
                    || operation.Kind == GameDBBatchOperationKind.RenameRow
                    ? operation.Rename != null
                    : operation.Kind == GameDBBatchOperationKind.DeleteTable
                        || operation.Kind == GameDBBatchOperationKind.DeleteField
                        || operation.Kind == GameDBBatchOperationKind.DeleteRow
                        ? operation.Delete != null
                        : operation.Kind == GameDBBatchOperationKind.AddField
                            || operation.Kind == GameDBBatchOperationKind.ReplaceField
                            ? operation.Field != null
                            : operation.Kind == GameDBBatchOperationKind.AddRow
                                || operation.Kind == GameDBBatchOperationKind.UpdateRow
                                ? operation.Row != null
                                : operation.Value != null;
            return expectedPayloadPresent
                ? null
                : $"Batch operation {operation.Kind} has the wrong payload type.";
        }

        private static bool IsDestructiveBatchOperation(GameDBBatchOperationKind kind)
        {
            switch (kind)
            {
                case GameDBBatchOperationKind.RenameTable:
                case GameDBBatchOperationKind.DeleteTable:
                case GameDBBatchOperationKind.ReplaceField:
                case GameDBBatchOperationKind.RenameField:
                case GameDBBatchOperationKind.DeleteField:
                case GameDBBatchOperationKind.RenameRow:
                case GameDBBatchOperationKind.DeleteRow:
                    return true;
                default:
                    return false;
            }
        }

        private static GameDBCommand CreateBatchCommand(GameDBBatchOperation operation)
        {
            switch (operation.Kind)
            {
                case GameDBBatchOperationKind.AddTable:
                    return new AddTableCommand(operation.Table.TableName,
                        operation.Table.KeyType, operation.Table.KeyTypeArgument);
                case GameDBBatchOperationKind.RenameTable:
                    return new RenameTableCommand(operation.Rename.CurrentName, operation.Rename.NewName);
                case GameDBBatchOperationKind.DeleteTable:
                    return new DeleteTableCommand(operation.Delete.Name);
                case GameDBBatchOperationKind.AddField:
                    return new AddFieldCommand(operation.Field.TableName,
                        operation.Field.FieldName, CreateFieldTypeSpec(operation.Field));
                case GameDBBatchOperationKind.ReplaceField:
                    return new ReplaceFieldCommand(operation.Field.TableName,
                        operation.Field.FieldName, CreateFieldTypeSpec(operation.Field));
                case GameDBBatchOperationKind.RenameField:
                    return new RenameFieldCommand(operation.Rename.TableName,
                        operation.Rename.CurrentName, operation.Rename.NewName);
                case GameDBBatchOperationKind.DeleteField:
                    return new DeleteFieldCommand(operation.Delete.TableName, operation.Delete.Name);
                case GameDBBatchOperationKind.AddRow:
                    return new AddRowCommand(operation.Row.TableName,
                        operation.Row.RowKey, operation.Row.Values);
                case GameDBBatchOperationKind.UpdateRow:
                    return new UpdateRowCommand(operation.Row.TableName,
                        operation.Row.RowKey, operation.Row.Values);
                case GameDBBatchOperationKind.SetValue:
                    return new SetValueCommand(operation.Value.TableName,
                        operation.Value.RowKey, operation.Value.FieldName, operation.Value.Value);
                case GameDBBatchOperationKind.RenameRow:
                    return new RenameRowCommand(operation.Rename.TableName,
                        operation.Rename.CurrentName, operation.Rename.NewName);
                case GameDBBatchOperationKind.DeleteRow:
                    return new DeleteRowCommand(operation.Delete.TableName, operation.Delete.Name);
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation.Kind));
            }
        }

        private static GameDBCommandKind ToCommandKind(GameDBBatchOperationKind kind)
        {
            switch (kind)
            {
                case GameDBBatchOperationKind.AddTable: return GameDBCommandKind.AddTable;
                case GameDBBatchOperationKind.RenameTable: return GameDBCommandKind.RenameTable;
                case GameDBBatchOperationKind.DeleteTable: return GameDBCommandKind.DeleteTable;
                case GameDBBatchOperationKind.AddField: return GameDBCommandKind.AddField;
                case GameDBBatchOperationKind.ReplaceField: return GameDBCommandKind.ReplaceField;
                case GameDBBatchOperationKind.RenameField: return GameDBCommandKind.RenameField;
                case GameDBBatchOperationKind.DeleteField: return GameDBCommandKind.DeleteField;
                case GameDBBatchOperationKind.AddRow: return GameDBCommandKind.AddRow;
                case GameDBBatchOperationKind.UpdateRow: return GameDBCommandKind.UpdateRow;
                case GameDBBatchOperationKind.SetValue: return GameDBCommandKind.SetValue;
                case GameDBBatchOperationKind.RenameRow: return GameDBCommandKind.RenameRow;
                case GameDBBatchOperationKind.DeleteRow: return GameDBCommandKind.DeleteRow;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static GameDBBatchOperationKind ToBatchOperationKind(GameDBCommandKind kind)
        {
            switch (kind)
            {
                case GameDBCommandKind.AddTable: return GameDBBatchOperationKind.AddTable;
                case GameDBCommandKind.RenameTable: return GameDBBatchOperationKind.RenameTable;
                case GameDBCommandKind.DeleteTable: return GameDBBatchOperationKind.DeleteTable;
                case GameDBCommandKind.AddField: return GameDBBatchOperationKind.AddField;
                case GameDBCommandKind.ReplaceField: return GameDBBatchOperationKind.ReplaceField;
                case GameDBCommandKind.RenameField: return GameDBBatchOperationKind.RenameField;
                case GameDBCommandKind.DeleteField: return GameDBBatchOperationKind.DeleteField;
                case GameDBCommandKind.AddRow: return GameDBBatchOperationKind.AddRow;
                case GameDBCommandKind.UpdateRow: return GameDBBatchOperationKind.UpdateRow;
                case GameDBCommandKind.SetValue: return GameDBBatchOperationKind.SetValue;
                case GameDBCommandKind.RenameRow: return GameDBBatchOperationKind.RenameRow;
                case GameDBCommandKind.DeleteRow: return GameDBBatchOperationKind.DeleteRow;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static GameDBCsvImportResult CsvTransactionFailure(DatabasePath path,
            GameDBCsvImportRequest request, bool dryRun, GameDBCsvImportPlan plan,
            GameDBTransactionResult transaction)
        {
            var failureKind = GameDBCsvFailureKind.TransactionFailed;
            if (transaction.FailureKind == GameDBTransactionFailureKind.AuthorizationDenied)
            {
                failureKind = GameDBCsvFailureKind.AuthorizationDenied;
            }
            else if (transaction.FailureKind == GameDBTransactionFailureKind.RevisionConflict)
            {
                failureKind = GameDBCsvFailureKind.RevisionConflict;
            }
            else if (transaction.FailureKind == GameDBTransactionFailureKind.ValidationFailed)
            {
                failureKind = GameDBCsvFailureKind.ValidationFailed;
            }

            var result = CsvImportFailure(path.AssetPath, request.TableName, request.Mode,
                dryRun, failureKind, null, transaction.Message);
            result.RevisionBefore = transaction.RevisionBefore;
            result.RevisionAfter = transaction.AttemptedRevision;
            result.Snapshot = transaction.AttemptedSnapshot;
            result.ImportedRowCount = plan.Rows.Count;
            result.Issues = transaction.Issues.ToList();
            if (transaction.FailureKind == GameDBTransactionFailureKind.ValidationFailed)
            {
                result.Errors = GameDBCsvEngine.MapValidationIssues(
                    plan, transaction.Issues, request.TableName);
                result.ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath };
            }
            return result;
        }

        private static GameDBCsvExportResult CsvExportFailure(string databasePath,
            string tableName, GameDBCsvFailureKind failureKind, string code, string message)
        {
            var result = new GameDBCsvExportResult
            {
                Success = false,
                FailureKind = failureKind,
                DatabasePath = databasePath,
                TableName = tableName,
                Message = message
            };
            if (!string.IsNullOrEmpty(code))
            {
                result.Errors.Add(new GameDBCsvError { Code = code, Message = message });
            }
            return result;
        }

        private static GameDBCsvImportResult CsvImportFailure(string databasePath,
            string tableName, GameDBCsvImportMode mode, bool dryRun,
            GameDBCsvFailureKind failureKind, string code, string message)
        {
            var result = new GameDBCsvImportResult
            {
                Success = false,
                DryRun = dryRun,
                FailureKind = failureKind,
                CommitStatus = GameDBCsvCommitStatus.NotAttempted,
                DatabasePath = databasePath,
                TableName = tableName,
                Message = message,
                Mode = mode
            };
            if (!string.IsNullOrEmpty(code))
            {
                result.Errors.Add(new GameDBCsvError { Code = code, Message = message });
            }
            return result;
        }

        private static GameDBCsvCommitStatus ToCsvCommitStatus(GameDBSaveStatus status)
        {
            switch (status)
            {
                case GameDBSaveStatus.Saved: return GameDBCsvCommitStatus.Saved;
                case GameDBSaveStatus.NoChanges: return GameDBCsvCommitStatus.NoChanges;
                case GameDBSaveStatus.SerializationFailed: return GameDBCsvCommitStatus.SerializationFailed;
                case GameDBSaveStatus.ValidationFailed: return GameDBCsvCommitStatus.ValidationFailed;
                case GameDBSaveStatus.Conflict: return GameDBCsvCommitStatus.Conflict;
                case GameDBSaveStatus.PersistenceFailed: return GameDBCsvCommitStatus.PersistenceFailed;
                case GameDBSaveStatus.PersistenceStateUnknown: return GameDBCsvCommitStatus.PersistenceStateUnknown;
                case GameDBSaveStatus.PostSavePending: return GameDBCsvCommitStatus.PostSavePending;
                case GameDBSaveStatus.SaveInProgress: return GameDBCsvCommitStatus.PersistenceFailed;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static GameDBBatchResult BatchTransactionFailure(DatabasePath path, bool dryRun,
            GameDBTransactionResult transaction)
        {
            var failedIndex = transaction.FailedCommandIndex >= 0
                ? transaction.FailedCommandIndex
                : transaction.DeniedCommandIndex;
            var failureKind = GameDBBatchFailureKind.TransactionFailed;
            if (transaction.FailureKind == GameDBTransactionFailureKind.AuthorizationDenied)
            {
                failureKind = GameDBBatchFailureKind.AuthorizationDenied;
            }
            else if (transaction.FailureKind == GameDBTransactionFailureKind.RevisionConflict)
            {
                failureKind = GameDBBatchFailureKind.RevisionConflict;
            }
            else if (transaction.FailureKind == GameDBTransactionFailureKind.CommandFailed
                || transaction.FailureKind == GameDBTransactionFailureKind.CommandThrew)
            {
                failureKind = GameDBBatchFailureKind.CommandFailed;
            }
            else if (transaction.FailureKind == GameDBTransactionFailureKind.ValidationFailed)
            {
                failureKind = GameDBBatchFailureKind.ValidationFailed;
            }

            var result = BatchFailure(path.AssetPath, dryRun, failureKind,
                transaction.Message, failedIndex,
                transaction.DeniedCommandKind.HasValue
                    ? (GameDBBatchOperationKind?)ToBatchOperationKind(transaction.DeniedCommandKind.Value)
                    : null);
            result.RevisionBefore = transaction.RevisionBefore;
            result.RevisionAfter = transaction.AttemptedRevision;
            result.Snapshot = transaction.AttemptedSnapshot;
            result.Issues = transaction.Issues.ToList();
            if (transaction.FailureKind == GameDBTransactionFailureKind.ValidationFailed)
            {
                result.ChangedPaths = new List<string> { path.AssetPath, path.SchemaAssetPath };
            }

            return result;
        }

        private static GameDBBatchResult BatchFailure(string databasePath, bool dryRun,
            GameDBBatchFailureKind failureKind, string message, int failedOperationIndex = -1,
            GameDBBatchOperationKind? deniedOperationKind = null)
        {
            return new GameDBBatchResult
            {
                Success = false,
                DryRun = dryRun,
                FailureKind = failureKind,
                CommitStatus = GameDBBatchCommitStatus.NotAttempted,
                Operation = "applyBatch",
                DatabasePath = databasePath,
                Message = message,
                FailedOperationIndex = failedOperationIndex,
                DeniedOperationKind = deniedOperationKind
            };
        }

        private static GameDBBatchCommitStatus ToBatchCommitStatus(GameDBSaveStatus status)
        {
            switch (status)
            {
                case GameDBSaveStatus.Saved: return GameDBBatchCommitStatus.Saved;
                case GameDBSaveStatus.NoChanges: return GameDBBatchCommitStatus.NoChanges;
                case GameDBSaveStatus.SerializationFailed: return GameDBBatchCommitStatus.SerializationFailed;
                case GameDBSaveStatus.ValidationFailed: return GameDBBatchCommitStatus.ValidationFailed;
                case GameDBSaveStatus.Conflict: return GameDBBatchCommitStatus.Conflict;
                case GameDBSaveStatus.PersistenceFailed: return GameDBBatchCommitStatus.PersistenceFailed;
                case GameDBSaveStatus.PersistenceStateUnknown: return GameDBBatchCommitStatus.PersistenceStateUnknown;
                case GameDBSaveStatus.PostSavePending: return GameDBBatchCommitStatus.PostSavePending;
                case GameDBSaveStatus.SaveInProgress: return GameDBBatchCommitStatus.PersistenceFailed;
                default: throw new ArgumentOutOfRangeException(nameof(status));
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
                    ApplySaveOutcome(result,
                        document.Save(new GameDBSaveOptions { ForceWrite = true }));
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
                CommitStatus = dryRun ? GameDBCommitStatus.DryRun : GameDBCommitStatus.NotAttempted,
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
            return CreateFieldTypeSpec(request.FieldType, request.IsArray,
                request.TypeArgument, request.DictionaryType);
        }

        private static GameDBFieldTypeSpec CreateFieldTypeSpec(GameDBBatchFieldOperation operation)
        {
            return CreateFieldTypeSpec(operation.FieldType, operation.IsArray,
                operation.TypeArgument, operation.DictionaryType);
        }

        private static GameDBFieldTypeSpec CreateFieldTypeSpec(FieldType fieldType, bool isArray,
            string typeArgument, GameDBDictionaryTypeDefinition dictionaryDefinition)
        {
            GameDBDictionaryTypeSpec dictionaryType = null;
            if (dictionaryDefinition != null)
            {
                dictionaryType = new GameDBDictionaryTypeSpec(
                    dictionaryDefinition.KeyType,
                    dictionaryDefinition.KeyTypeArgument,
                    dictionaryDefinition.ValueType,
                    dictionaryDefinition.ValueTypeArgument);
            }

            return new GameDBFieldTypeSpec(fieldType, isArray, typeArgument, dictionaryType);
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
                CommitStatus = dryRun ? GameDBCommitStatus.DryRun : GameDBCommitStatus.NotAttempted,
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
                ApplySaveOutcome(result,
                    document.Save(new GameDBSaveOptions { ForceWrite = true }));
            }

            return result;
        }

        private static void ApplySaveOutcome(GameDBAutomationResult result, GameDBSaveOutcome save)
        {
            result.Success = save.Success;
            result.CommitStatus = ToCommitStatus(save.Status);
            if (!save.Success)
            {
                result.Message = save.Message;
            }
            result.FilesCommitted = save.FilesCommitted;
            result.PostSavePending = save.PostSavePending;
            result.PostSaveErrors = save.PostSaveErrors.ToList();
            result.RecoveryArtifacts = save.RecoveryArtifacts.ToList();
            result.ChangedPaths = save.ChangedPaths.ToList();
        }

        private static GameDBCommitStatus ToCommitStatus(GameDBSaveStatus status)
        {
            switch (status)
            {
                case GameDBSaveStatus.Saved: return GameDBCommitStatus.Saved;
                case GameDBSaveStatus.NoChanges: return GameDBCommitStatus.NoChanges;
                case GameDBSaveStatus.SerializationFailed: return GameDBCommitStatus.SerializationFailed;
                case GameDBSaveStatus.ValidationFailed: return GameDBCommitStatus.ValidationFailed;
                case GameDBSaveStatus.Conflict: return GameDBCommitStatus.Conflict;
                case GameDBSaveStatus.PersistenceFailed: return GameDBCommitStatus.PersistenceFailed;
                case GameDBSaveStatus.PersistenceStateUnknown: return GameDBCommitStatus.PersistenceStateUnknown;
                case GameDBSaveStatus.PostSavePending: return GameDBCommitStatus.PostSavePending;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
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

        private static GameDBProjectSettingsResult ProjectSettingsResult(
            WorkspaceProjectSettingsResult source, bool dryRun, string revisionBefore)
        {
            var snapshot = ToAutomationSnapshot(source.Snapshot);
            var committed = source.Changed
                && source.CommitStatus == GameDBProjectSettingsCommitStatus.Saved;
            var postSavePending = committed && source.NotificationErrors.Count > 0;
            var commitStatus = postSavePending
                ? GameDBCommitStatus.PostSavePending
                : ToCommitStatus(source.CommitStatus);
            var changedPaths = source.Changed
                && (source.CommitStatus == GameDBProjectSettingsCommitStatus.Saved
                    || source.CommitStatus == GameDBProjectSettingsCommitStatus.DryRun)
                ? new List<string> { ProjectSettingsPath }
                : new List<string>();
            var message = source.Error;
            if (message == null)
            {
                if (source.NotificationErrors.Count > 0)
                {
                    message = committed
                        ? "Project settings were saved, but one or more listeners failed."
                        : "Project settings were loaded, but one or more listeners failed.";
                }
                else if (source.CommitStatus == GameDBProjectSettingsCommitStatus.NotAttempted)
                {
                    message = "Project settings loaded.";
                }
                else if (dryRun)
                {
                    message = source.Changed
                        ? "Project settings validated; no files were written."
                        : "Project settings already match; no files were written.";
                }
                else
                {
                    message = source.Changed
                        ? "Project settings saved."
                        : "Project settings already match.";
                }
            }

            return new GameDBProjectSettingsResult
            {
                Success = source.Success && !postSavePending,
                DryRun = dryRun,
                CommitStatus = commitStatus,
                Message = message,
                RevisionBefore = source.RevisionBefore ?? revisionBefore ?? snapshot?.Revision,
                RevisionAfter = snapshot?.Revision,
                Snapshot = snapshot,
                SnapshotIsProspective = (source.CommitStatus
                        == GameDBProjectSettingsCommitStatus.DryRun && source.Changed)
                    || source.CommitStatus
                        == GameDBProjectSettingsCommitStatus.ValidationFailed,
                Issues = ToAutomationIssues(source.Snapshot),
                ChangedPaths = changedPaths,
                FilesCommitted = committed,
                PostSavePending = postSavePending,
                PostSaveErrors = committed
                    ? source.NotificationErrors.ToList()
                    : new List<string>()
            };
        }

        private static GameDBProjectSettingsResult ProjectSettingsFailure(bool dryRun,
            string message)
        {
            return new GameDBProjectSettingsResult
            {
                Success = false,
                DryRun = dryRun,
                CommitStatus = GameDBCommitStatus.NotAttempted,
                Message = message
            };
        }

        private static GameDBProjectSettingsResult ProjectSettingsValidationFailure(
            bool dryRun, WorkspaceProjectSettingsSnapshot current,
            GameDBProjectSettingsIssueKind kind, string value, string message)
        {
            return new GameDBProjectSettingsResult
            {
                Success = false,
                DryRun = dryRun,
                CommitStatus = GameDBCommitStatus.ValidationFailed,
                Message = message,
                RevisionBefore = current.Revision,
                RevisionAfter = current.Revision,
                Snapshot = ToAutomationSnapshot(current),
                SnapshotIsProspective = false,
                Issues = new List<GameDBProjectSettingsIssue>
                {
                    new GameDBProjectSettingsIssue
                    {
                        Kind = kind,
                        Value = value,
                        Message = message
                    }
                }
            };
        }

        private static GameDBProjectSettingsSnapshot ToAutomationSnapshot(
            WorkspaceProjectSettingsSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            return new GameDBProjectSettingsSnapshot
            {
                Revision = source.Revision,
                RegisteredDatabasePaths = source.RegisteredDatabasePaths
                    .Select(path => "Assets/" + path).ToList(),
                ImportedEnumTypeNames = source.ImportedEnumTypeNames.ToList(),
                ExportPath = ToAutomationAssetDirectory(source.ExportPath),
                BuildPath = ToAutomationAssetDirectory(source.BuildPath)
            };
        }

        private static List<GameDBProjectSettingsIssue> ToAutomationIssues(
            WorkspaceProjectSettingsSnapshot source)
        {
            if (source == null)
            {
                return new List<GameDBProjectSettingsIssue>();
            }

            return source.ValidationIssues.Select(issue => new GameDBProjectSettingsIssue
            {
                Kind = issue.Kind == Workspace.GameDBProjectSettingsIssueKind.MissingDatabasePath
                    ? GameDBProjectSettingsIssueKind.MissingDatabase
                    : GameDBProjectSettingsIssueKind.UnresolvedImportedEnumType,
                Value = issue.Kind == Workspace.GameDBProjectSettingsIssueKind.MissingDatabasePath
                    ? "Assets/" + issue.Value
                    : issue.Value,
                Message = issue.Kind == Workspace.GameDBProjectSettingsIssueKind.MissingDatabasePath
                    ? $"Database data or schema file is missing: Assets/{issue.Value}"
                    : $"Imported enum type could not be resolved: {issue.Value}"
            }).ToList();
        }

        private static GameDBCommitStatus ToCommitStatus(
            GameDBProjectSettingsCommitStatus status)
        {
            switch (status)
            {
                case GameDBProjectSettingsCommitStatus.NotAttempted:
                    return GameDBCommitStatus.NotAttempted;
                case GameDBProjectSettingsCommitStatus.DryRun:
                    return GameDBCommitStatus.DryRun;
                case GameDBProjectSettingsCommitStatus.Saved:
                    return GameDBCommitStatus.Saved;
                case GameDBProjectSettingsCommitStatus.NoChanges:
                    return GameDBCommitStatus.NoChanges;
                case GameDBProjectSettingsCommitStatus.ValidationFailed:
                    return GameDBCommitStatus.ValidationFailed;
                case GameDBProjectSettingsCommitStatus.Conflict:
                    return GameDBCommitStatus.Conflict;
                case GameDBProjectSettingsCommitStatus.PersistenceFailed:
                    return GameDBCommitStatus.PersistenceFailed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static string ResolveOptionalAssetDirectory(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }
            var directory = ResolveAssetDirectory(assetPath, true);
            if (string.IsNullOrEmpty(directory.RelativePath))
            {
                throw new ArgumentException(
                    "Output path must identify a child directory under Assets.");
            }
            return directory.RelativePath;
        }

        private static string ToAutomationAssetDirectory(string relativePath)
        {
            return string.IsNullOrWhiteSpace(relativePath)
                ? string.Empty
                : "Assets/" + relativePath;
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

            var inputRelative = normalized.Substring("Assets/".Length);
            var absolute = EnsureInsideAssets(Path.Combine(Application.dataPath, inputRelative));
            var canonicalDatabaseAssetPath = ToAssetPath(absolute);
            var relative = canonicalDatabaseAssetPath.Substring("Assets/".Length);
            var schemaAbsolute = Path.ChangeExtension(absolute, ".schema.json");
            return new DatabasePath
            {
                AssetPath = canonicalDatabaseAssetPath,
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

            var inputRelative = normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized.Substring("Assets/".Length);
            var absolute = EnsureInsideAssets(Path.Combine(Application.dataPath, inputRelative), true);
            var assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var relative = string.Equals(absolute, assetsRoot, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : absolute.Substring(assetsRoot.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
            var canonicalAssetPath = string.IsNullOrEmpty(relative)
                ? "Assets"
                : "Assets/" + relative;
            if (!allowCreate && !Directory.Exists(absolute))
            {
                return new AssetDirectory
                {
                    AssetPath = canonicalAssetPath,
                    RelativePath = relative,
                    AbsolutePath = absolute
                };
            }

            return new AssetDirectory
            {
                AssetPath = canonicalAssetPath,
                RelativePath = relative,
                AbsolutePath = absolute
            };
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
