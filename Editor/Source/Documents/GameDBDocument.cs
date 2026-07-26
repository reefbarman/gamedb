using GameDBEditorLibrary.Automation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDBEditorLibrary.Documents
{
    internal sealed class GameDBTransactionOptions
    {
        internal string ExpectedRevision { get; set; }
        internal IReadOnlyCollection<GameDBCommandKind> AllowedDestructiveOperations { get; set; }
            = Array.Empty<GameDBCommandKind>();
    }

    internal enum GameDBTransactionFailureKind
    {
        None,
        InvalidRequest,
        AuthorizationDenied,
        RevisionConflict,
        StageCloneFailed,
        CommandFailed,
        CommandThrew,
        AttemptedStateFailed,
        ValidationFailed,
        ValidationThrew,
        SnapshotFailed
    }

    internal sealed class GameDBTransactionResult
    {
        internal bool Success { get; set; }
        internal GameDBTransactionFailureKind FailureKind { get; set; }
        internal int FailedCommandIndex { get; set; } = -1;
        internal int DeniedCommandIndex { get; set; } = -1;
        internal GameDBCommandKind? DeniedCommandKind { get; set; }
        internal string Message { get; set; }
        internal string RevisionBefore { get; set; }
        internal string AttemptedRevision { get; set; }
        internal GameDBSerializedState AttemptedState { get; set; }
        internal GameDBSnapshot AttemptedSnapshot { get; set; }
        internal IReadOnlyList<GameDBValidationIssue> Issues { get; set; }
            = Array.Empty<GameDBValidationIssue>();
        internal IReadOnlyList<GameDBCommandKind> Changes { get; set; }
            = Array.Empty<GameDBCommandKind>();
        internal IReadOnlyList<string> NotificationErrors { get; set; }
            = Array.Empty<string>();
        internal IReadOnlyList<string> AttemptedMetadataErrors { get; set; }
            = Array.Empty<string>();
    }

    internal sealed class GameDBDocumentChange
    {
        internal string DocumentId { get; }
        internal string RevisionBefore { get; }
        internal string RevisionAfter { get; }
        internal IReadOnlyList<GameDBCommandKind> Commands { get; }

        internal GameDBDocumentChange(string documentId, string revisionBefore,
            string revisionAfter, IReadOnlyList<GameDBCommandKind> commands)
        {
            DocumentId = documentId;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Commands = commands;
        }
    }

    internal sealed class GameDBDocument
    {
        private static readonly Dictionary<Type, CommandDescriptor> CommandDescriptors
            = CreateCommandDescriptors();

        private readonly object m_gate = new object();
        private readonly object m_saveGate = new object();
        private readonly Queue<PendingChange> m_pendingChanges = new Queue<PendingChange>();
        private readonly IGameDBPairStore m_pairStore;
        private readonly IGameDBPostSaveActions m_postSaveActions;
        private GameDB m_model;
        private string m_baselineRevision;
        private string m_currentRevision;
        private GameDBDiskToken m_baselineDiskToken;
        private GameDBPostSaveState m_pendingPostSave;
        private bool m_persistenceStateUnknown;
        private bool m_drainingChanges;
        private Action<GameDBDocumentChange> m_changed;

        internal string DocumentId { get; }
        internal string AssetPath { get; }
        internal string SchemaAssetPath { get; }

        internal string BaselineRevision
        {
            get
            {
                lock (m_gate)
                {
                    return m_baselineRevision;
                }
            }
        }

        internal string CurrentRevision
        {
            get
            {
                lock (m_gate)
                {
                    return GetCurrentRevisionLocked();
                }
            }
        }

        internal bool IsDirty
        {
            get
            {
                lock (m_gate)
                {
                    return !string.Equals(GetCurrentRevisionLocked(), m_baselineRevision,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        internal bool HasPendingPostSaveWork
        {
            get
            {
                lock (m_gate)
                {
                    return m_pendingPostSave != null && m_pendingPostSave.HasPendingWork;
                }
            }
        }

        internal event Action<GameDBDocumentChange> Changed
        {
            add
            {
                lock (m_gate)
                {
                    m_changed += value;
                }
            }
            remove
            {
                lock (m_gate)
                {
                    m_changed -= value;
                }
            }
        }

        private GameDBDocument(string documentId, GameDBResolvedPath path, GameDB model,
            string baselineRevision, GameDBDiskToken baselineDiskToken,
            GameDBPostSaveState pendingPostSave, bool persistenceStateUnknown,
            IGameDBPairStore pairStore, IGameDBPostSaveActions postSaveActions)
        {
            DocumentId = documentId;
            AssetPath = path.AssetPath;
            SchemaAssetPath = path.SchemaAssetPath;
            m_model = model;
            m_baselineRevision = baselineRevision;
            m_currentRevision = GameDBModelCodec.ComputeRevision(model);
            m_baselineDiskToken = baselineDiskToken;
            m_pendingPostSave = pendingPostSave;
            m_persistenceStateUnknown = persistenceStateUnknown;
            m_pairStore = pairStore;
            m_postSaveActions = postSaveActions;
        }

        internal static GameDBDocument Load(string assetPath)
        {
            return Load(assetPath, GameDBFilePairStore.Instance, GameDBUnityPostSaveActions.Instance);
        }

        internal static GameDBDocument CreateNew(string assetPath, string scopeName, bool localization)
        {
            return CreateNew(assetPath, scopeName, localization,
                GameDBFilePairStore.Instance, GameDBUnityPostSaveActions.Instance);
        }

        internal static GameDBDocument CreateReplacement(string assetPath,
            string dataJson, string schemaJson)
        {
            return CreateReplacement(assetPath, dataJson, schemaJson,
                GameDBFilePairStore.Instance, GameDBUnityPostSaveActions.Instance);
        }

        internal static GameDBDocument CreateNewReplacement(string assetPath,
            string dataJson, string schemaJson)
        {
            return CreateNewReplacement(assetPath, dataJson, schemaJson,
                GameDBFilePairStore.Instance, GameDBUnityPostSaveActions.Instance);
        }

        internal static GameDBDocument RestoreState(GameDBDocumentState state)
        {
            return RestoreState(state, GameDBFilePairStore.Instance, GameDBUnityPostSaveActions.Instance);
        }

        internal GameDBDocument CreateReplacement(string scopeName, bool localization)
        {
            lock (m_gate)
            {
                var path = m_pairStore.Resolve(AssetPath);
                var model = new GameDB();
                model.CreateInMemory(path.RelativePath);
                model.ScopeName = scopeName;
                model.LocalizationDB = localization;
                return CreateReplacementLocked(path, model);
            }
        }

        internal GameDBDocument CreateReplacement(string dataJson, string schemaJson)
        {
            lock (m_gate)
            {
                var path = m_pairStore.Resolve(AssetPath);
                var model = GameDBModelCodec.Import(dataJson, schemaJson, path.RelativePath);
                return CreateReplacementLocked(path, model);
            }
        }

        private GameDBDocument CreateReplacementLocked(GameDBResolvedPath path, GameDB model)
        {
            if (m_persistenceStateUnknown)
            {
                throw new InvalidOperationException(
                    "Unknown persistence state must be recovered before replacing the document.");
            }

            if (m_pendingPostSave != null && m_pendingPostSave.HasPendingWork)
            {
                throw new InvalidOperationException(
                    "Pending post-save work must complete before replacing the document.");
            }

            if (!string.Equals(GetCurrentRevisionLocked(), m_baselineRevision,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only a clean loaded document can establish a replacement baseline.");
            }

            return new GameDBDocument(Guid.NewGuid().ToString("N"), path, model,
                m_baselineRevision, m_baselineDiskToken, null, false,
                m_pairStore, m_postSaveActions);
        }

        internal static GameDBDocument Load(string assetPath, IGameDBPairStore pairStore,
            IGameDBPostSaveActions postSaveActions)
        {
            var pair = pairStore.Read(assetPath);
            RequireCompletePair(pair, true);
            var model = GameDBModelCodec.Import(
                GameDBFilePairStore.Decode(pair.DataBytes),
                GameDBFilePairStore.Decode(pair.SchemaBytes),
                pair.Path.RelativePath);
            var revision = GameDBModelCodec.ComputeRevision(model);
            return new GameDBDocument(Guid.NewGuid().ToString("N"), pair.Path, model,
                revision, pair.Token, null, false, pairStore, postSaveActions);
        }

        internal static GameDBDocument CreateNew(string assetPath, string scopeName, bool localization,
            IGameDBPairStore pairStore, IGameDBPostSaveActions postSaveActions)
        {
            var pair = pairStore.Read(assetPath);
            if (pair.Token.DataExists || pair.Token.SchemaExists)
            {
                throw new IOException("Database files already exist.");
            }

            var model = new GameDB();
            model.CreateInMemory(pair.Path.RelativePath);
            model.ScopeName = scopeName;
            model.LocalizationDB = localization;
            return new GameDBDocument(Guid.NewGuid().ToString("N"), pair.Path, model,
                null, pair.Token, null, false, pairStore, postSaveActions);
        }

        internal static GameDBDocument CreateReplacement(string assetPath,
            string dataJson, string schemaJson, IGameDBPairStore pairStore,
            IGameDBPostSaveActions postSaveActions)
        {
            var pair = pairStore.Read(assetPath);
            RequireCompletePair(pair, false);
            string baselineRevision = null;
            if (pair.Token.DataExists)
            {
                var baselineModel = GameDBModelCodec.Import(
                    GameDBFilePairStore.Decode(pair.DataBytes),
                    GameDBFilePairStore.Decode(pair.SchemaBytes),
                    pair.Path.RelativePath);
                baselineRevision = GameDBModelCodec.ComputeRevision(baselineModel);
            }

            return CreateReplacement(pair, dataJson, schemaJson,
                baselineRevision, pairStore, postSaveActions);
        }

        internal static GameDBDocument CreateNewReplacement(string assetPath,
            string dataJson, string schemaJson, IGameDBPairStore pairStore,
            IGameDBPostSaveActions postSaveActions)
        {
            var pair = pairStore.Read(assetPath);
            if (pair.Token.DataExists || pair.Token.SchemaExists)
            {
                throw new IOException("Database files already exist.");
            }

            return CreateReplacement(pair, dataJson, schemaJson,
                null, pairStore, postSaveActions);
        }

        private static GameDBDocument CreateReplacement(GameDBPairRead pair,
            string dataJson, string schemaJson, string baselineRevision,
            IGameDBPairStore pairStore, IGameDBPostSaveActions postSaveActions)
        {
            var model = GameDBModelCodec.Import(dataJson, schemaJson, pair.Path.RelativePath);
            return new GameDBDocument(Guid.NewGuid().ToString("N"), pair.Path, model,
                baselineRevision, pair.Token, null, false, pairStore, postSaveActions);
        }

        internal static GameDBDocument RestoreState(GameDBDocumentState state,
            IGameDBPairStore pairStore, IGameDBPostSaveActions postSaveActions)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Version != GameDBDocumentState.CurrentVersion)
            {
                throw new FormatException($"Unsupported document state version: {state.Version}.");
            }

            if (string.IsNullOrWhiteSpace(state.DocumentId))
            {
                throw new FormatException("Document state identity is required.");
            }

            var path = pairStore.Resolve(state.AssetPath);
            var model = GameDBModelCodec.Import(state.DataJson, state.SchemaJson, path.RelativePath);
            var currentRevision = GameDBModelCodec.ComputeRevision(model);
            var isDirty = !string.Equals(currentRevision, state.BaselineRevision,
                StringComparison.OrdinalIgnoreCase);
            if (isDirty != state.WasDirty)
            {
                throw new FormatException("Document state dirty assertion is inconsistent.");
            }

            var hasPendingImports = state.DataImportPending || state.SchemaImportPending;
            var hasPendingPostSave = hasPendingImports || state.CallbackPending;
            if (hasPendingImports && !state.CallbackPending)
            {
                throw new FormatException("Pending imports require a pending saved callback.");
            }

            if (state.PersistenceStateUnknown && hasPendingPostSave)
            {
                throw new FormatException("Unknown persistence state cannot include pending post-save work.");
            }

            if (hasPendingPostSave && string.IsNullOrWhiteSpace(state.PendingScopeName))
            {
                throw new FormatException("Pending post-save state requires a scope name.");
            }

            if (!hasPendingPostSave && !string.IsNullOrEmpty(state.PendingScopeName))
            {
                throw new FormatException("A pending scope name requires pending post-save work.");
            }

            if (hasPendingPostSave && (!state.BaselineDiskToken.DataExists
                || !state.BaselineDiskToken.SchemaExists
                || string.IsNullOrWhiteSpace(state.BaselineRevision)))
            {
                throw new FormatException("Pending post-save work requires a committed database baseline.");
            }

            var pendingPostSave = hasPendingPostSave
                ? new GameDBPostSaveState
                {
                    DataImportPending = state.DataImportPending,
                    SchemaImportPending = state.SchemaImportPending,
                    CallbackPending = state.CallbackPending,
                    ScopeName = state.PendingScopeName
                }
                : null;
            return new GameDBDocument(state.DocumentId, path, model, state.BaselineRevision,
                state.BaselineDiskToken, pendingPostSave,
                state.PersistenceStateUnknown, pairStore, postSaveActions);
        }

        internal GameDBSerializedState SerializeCurrent()
        {
            lock (m_gate)
            {
                return GameDBModelCodec.Serialize(m_model);
            }
        }

        internal GameDBSnapshot CreateSnapshot()
        {
            lock (m_gate)
            {
                return GameDBModelCodec.CreateSnapshot(AssetPath, SchemaAssetPath, m_model);
            }
        }

        internal GameDB CreateDetachedModel()
        {
            lock (m_gate)
            {
                return GameDBModelCodec.CreateDetachedModel(m_model);
            }
        }

        internal IReadOnlyList<GameDBValidationIssue> Validate()
        {
            lock (m_gate)
            {
                return GameDBModelOperations.Validate(m_model).AsReadOnly();
            }
        }

        internal GameDBDocumentState CaptureState()
        {
            lock (m_gate)
            {
                var current = GameDBModelCodec.Serialize(m_model);
                return new GameDBDocumentState
                {
                    DocumentId = DocumentId,
                    AssetPath = AssetPath,
                    DataJson = current.DataJson,
                    SchemaJson = current.SchemaJson,
                    BaselineRevision = m_baselineRevision,
                    BaselineDiskToken = m_baselineDiskToken,
                    DataImportPending = m_pendingPostSave?.DataImportPending ?? false,
                    SchemaImportPending = m_pendingPostSave?.SchemaImportPending ?? false,
                    CallbackPending = m_pendingPostSave?.CallbackPending ?? false,
                    PendingScopeName = m_pendingPostSave?.ScopeName,
                    PersistenceStateUnknown = m_persistenceStateUnknown,
                    WasDirty = !string.Equals(current.Revision, m_baselineRevision,
                        StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        internal GameDBSaveOutcome Save(GameDBSaveOptions options = null)
        {
            lock (m_saveGate)
            {
                return SaveLocked(options ?? new GameDBSaveOptions());
            }
        }

        private GameDBSaveOutcome SaveLocked(GameDBSaveOptions options)
        {
            var pendingOutcome = RetryPendingPostSave();
            if (pendingOutcome != null && !pendingOutcome.Success)
            {
                return pendingOutcome;
            }

            GameDB candidate;
            GameDBSerializedState state;
            List<GameDBValidationIssue> issues;
            string revisionBefore;
            string candidateSourceRevision;
            string scopeNameToSave;
            GameDBDiskToken baselineToken;
            lock (m_gate)
            {
                if (m_persistenceStateUnknown)
                {
                    return Outcome(GameDBSaveStatus.PersistenceStateUnknown, false,
                        "Persistence state is unknown; reload or recover the database before saving.",
                        false, m_baselineRevision, null, GetCurrentRevisionLocked(),
                        m_baselineDiskToken, m_baselineDiskToken);
                }

                revisionBefore = m_baselineRevision;
                candidateSourceRevision = GetCurrentRevisionLocked();
                scopeNameToSave = m_model.ScopeName;
                baselineToken = m_baselineDiskToken;
                try
                {
                    candidate = GameDBModelCodec.CreateDetachedModel(m_model);
                    GameDBUnityObjectNormalizer.Normalize(candidate);
                    state = GameDBModelCodec.Serialize(candidate);
                }
                catch (Exception exception)
                {
                    return Outcome(GameDBSaveStatus.SerializationFailed, false,
                        exception.Message, false, revisionBefore, null, GetCurrentRevisionLocked(),
                        baselineToken, baselineToken);
                }

                try
                {
                    issues = GameDBModelOperations.Validate(candidate);
                }
                catch (Exception exception)
                {
                    return Outcome(GameDBSaveStatus.ValidationFailed, false,
                        exception.Message, false, revisionBefore, null, state.Revision,
                        baselineToken, baselineToken);
                }

                if (issues.Count > 0)
                {
                    return Outcome(GameDBSaveStatus.ValidationFailed, false,
                        $"Save blocked by {issues.Count} validation issue(s).", false,
                        revisionBefore, null, state.Revision, baselineToken, baselineToken);
                }

            }

            if (!options.ForceWrite && string.Equals(state.Revision, revisionBefore,
                StringComparison.OrdinalIgnoreCase))
            {
                GameDBPairRead currentPair;
                try
                {
                    currentPair = m_pairStore.Read(AssetPath);
                }
                catch (GameDBRecoveryRequiredException exception)
                {
                    var recovery = Outcome(GameDBSaveStatus.PersistenceStateUnknown, false,
                        exception.Message, false, revisionBefore, null, CurrentRevision,
                        baselineToken, baselineToken);
                    recovery.RecoveryArtifacts = exception.Artifacts;
                    return recovery;
                }
                catch (Exception exception)
                {
                    return Outcome(GameDBSaveStatus.PersistenceFailed, false,
                        exception.Message, false, revisionBefore, null, CurrentRevision,
                        baselineToken, baselineToken);
                }

                if (currentPair.Token != baselineToken)
                {
                    return Outcome(GameDBSaveStatus.Conflict, false,
                        "Database files changed after this document was loaded.", false,
                        revisionBefore, null, CurrentRevision,
                        baselineToken, currentPair.Token);
                }

                return Outcome(GameDBSaveStatus.NoChanges, true,
                    "Document has no changes.", false, revisionBefore, revisionBefore,
                    state.Revision, baselineToken, currentPair.Token);
            }

            var dataBytes = GameDBFilePairStore.Encode(state.DataJson);
            var schemaBytes = GameDBFilePairStore.Encode(state.SchemaJson);
            GameDBPairCommitResult commit;
            try
            {
                commit = m_pairStore.Commit(AssetPath, baselineToken, dataBytes, schemaBytes);
            }
            catch (Exception exception)
            {
                return Outcome(GameDBSaveStatus.PersistenceFailed, false,
                    exception.Message, false, revisionBefore, null, CurrentRevision,
                    baselineToken, baselineToken);
            }

            if (commit.Status == GameDBPairCommitStatus.Conflict)
            {
                return Outcome(GameDBSaveStatus.Conflict, false, commit.Message, false,
                    revisionBefore, null, CurrentRevision, commit.TokenBefore, commit.TokenAfter);
            }

            if (commit.Status == GameDBPairCommitStatus.Failed)
            {
                return Outcome(GameDBSaveStatus.PersistenceFailed, false, commit.Message, false,
                    revisionBefore, null, CurrentRevision, commit.TokenBefore, commit.TokenAfter);
            }

            if (commit.Status == GameDBPairCommitStatus.StateUnknown)
            {
                lock (m_gate)
                {
                    m_persistenceStateUnknown = true;
                }

                var unknown = Outcome(GameDBSaveStatus.PersistenceStateUnknown, false,
                    commit.Message, false, revisionBefore, null, CurrentRevision,
                    commit.TokenBefore, commit.TokenAfter);
                unknown.RecoveryArtifacts = commit.RecoveryArtifacts;
                return unknown;
            }

            lock (m_gate)
            {
                if (string.Equals(GetCurrentRevisionLocked(), candidateSourceRevision,
                    StringComparison.OrdinalIgnoreCase))
                {
                    m_model = candidate;
                    m_currentRevision = state.Revision;
                }

                m_baselineRevision = state.Revision;
                m_baselineDiskToken = commit.TokenAfter;
                m_pendingPostSave = new GameDBPostSaveState
                {
                    DataImportPending = true,
                    SchemaImportPending = true,
                    CallbackPending = true,
                    ScopeName = scopeNameToSave
                };
            }

            var postSave = RetryPendingPostSave();
            if (postSave != null && !postSave.Success)
            {
                postSave.RevisionBefore = revisionBefore;
                postSave.RevisionSaved = state.Revision;
                postSave.DiskTokenBefore = commit.TokenBefore;
                postSave.DiskTokenAfter = commit.TokenAfter;
                return postSave;
            }

            return Outcome(GameDBSaveStatus.Saved, true, "Database saved.", true,
                revisionBefore, state.Revision, CurrentRevision,
                commit.TokenBefore, commit.TokenAfter);
        }

        private GameDBSaveOutcome RetryPendingPostSave()
        {
            GameDBPostSaveState pending;
            GameDBDiskToken baselineToken;
            string baselineRevision;
            lock (m_gate)
            {
                if (m_pendingPostSave == null || !m_pendingPostSave.HasPendingWork)
                {
                    return null;
                }

                pending = m_pendingPostSave.Copy();
                baselineToken = m_baselineDiskToken;
                baselineRevision = m_baselineRevision;
            }

            GameDBPairRead pair;
            try
            {
                pair = m_pairStore.Read(AssetPath);
            }
            catch (Exception exception)
            {
                return Outcome(GameDBSaveStatus.Conflict, false, exception.Message, true,
                    baselineRevision, baselineRevision, CurrentRevision,
                    baselineToken, baselineToken, true, new[] { exception.Message });
            }

            if (pair.Token != baselineToken)
            {
                return Outcome(GameDBSaveStatus.Conflict, false,
                    "Database files changed before pending post-save work completed.", true,
                    baselineRevision, baselineRevision, CurrentRevision,
                    baselineToken, pair.Token, true);
            }

            var errors = new List<string>();
            if (pending.DataImportPending)
            {
                try
                {
                    m_postSaveActions.Import(AssetPath);
                    pending.DataImportPending = false;
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            if (pending.SchemaImportPending)
            {
                try
                {
                    m_postSaveActions.Import(SchemaAssetPath);
                    pending.SchemaImportPending = false;
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            if (!pending.DataImportPending && !pending.SchemaImportPending && pending.CallbackPending)
            {
                try
                {
                    m_postSaveActions.Notify(pending.ScopeName);
                    pending.CallbackPending = false;
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            lock (m_gate)
            {
                m_pendingPostSave = pending.HasPendingWork ? pending : null;
            }

            if (pending.HasPendingWork)
            {
                return Outcome(GameDBSaveStatus.PostSavePending, false,
                    "Database files were saved, but post-save work is still pending.", true,
                    baselineRevision, baselineRevision, CurrentRevision,
                    baselineToken, baselineToken, true, errors);
            }

            return Outcome(GameDBSaveStatus.Saved, true,
                "Pending post-save work completed.", true,
                baselineRevision, baselineRevision, CurrentRevision,
                baselineToken, baselineToken, false, errors);
        }

        private GameDBSaveOutcome Outcome(GameDBSaveStatus status, bool success,
            string message, bool filesCommitted, string revisionBefore, string revisionSaved,
            string revisionCurrent, GameDBDiskToken tokenBefore, GameDBDiskToken tokenAfter,
            bool postSavePending = false, IReadOnlyList<string> postSaveErrors = null)
        {
            return new GameDBSaveOutcome
            {
                Status = status,
                Success = success,
                FilesCommitted = filesCommitted,
                PostSavePending = postSavePending,
                Message = message,
                RevisionBefore = revisionBefore,
                RevisionSaved = revisionSaved,
                RevisionCurrent = revisionCurrent,
                DiskTokenBefore = tokenBefore,
                DiskTokenAfter = tokenAfter,
                ChangedPaths = filesCommitted
                    ? new[] { AssetPath, SchemaAssetPath }
                    : Array.Empty<string>(),
                PostSaveErrors = postSaveErrors ?? Array.Empty<string>()
            };
        }

        internal GameDBTransactionResult ApplyTransaction(IReadOnlyList<GameDBCommand> commands,
            GameDBTransactionOptions options = null)
        {
            GameDBTransactionResult result;
            GameDBDocumentChange change = null;
            var drainChanges = false;

            lock (m_gate)
            {
                options = options ?? new GameDBTransactionOptions();
                result = ApplyTransactionLocked(commands, options, out change);
                if (change != null)
                {
                    m_pendingChanges.Enqueue(new PendingChange(change, result));
                    if (!m_drainingChanges)
                    {
                        m_drainingChanges = true;
                        drainChanges = true;
                    }
                }
            }

            if (drainChanges)
            {
                DrainChanges();
            }

            return result;
        }

        private GameDBTransactionResult ApplyTransactionLocked(IReadOnlyList<GameDBCommand> commands,
            GameDBTransactionOptions options, out GameDBDocumentChange change)
        {
            change = null;
            GameDBTransactionResult result;
            var revisionBefore = GetCurrentRevisionLocked();
            if (commands == null)
            {
                return Failure(GameDBTransactionFailureKind.InvalidRequest,
                    "Commands are required.", revisionBefore);
            }

            GameDBCommand[] commandArray;
            try
            {
                commandArray = commands.ToArray();
            }
            catch (Exception exception)
            {
                return Failure(GameDBTransactionFailureKind.InvalidRequest,
                    exception.Message, revisionBefore);
            }

            var descriptors = new CommandDescriptor[commandArray.Length];
            for (var index = 0; index < commandArray.Length; index++)
            {
                var command = commandArray[index];
                if (command == null)
                {
                    return Failure(GameDBTransactionFailureKind.InvalidRequest,
                        "Commands cannot contain null entries.", revisionBefore);
                }

                if (!CommandDescriptors.TryGetValue(command.GetType(), out descriptors[index]))
                {
                    return Failure(GameDBTransactionFailureKind.InvalidRequest,
                        $"Unsupported command type: {command.GetType().FullName}.", revisionBefore);
                }
            }

            var allowed = options.AllowedDestructiveOperations == null
                ? new HashSet<GameDBCommandKind>()
                : new HashSet<GameDBCommandKind>(options.AllowedDestructiveOperations);
            for (var index = 0; index < descriptors.Length; index++)
            {
                if (descriptors[index].IsDestructive && !allowed.Contains(descriptors[index].Kind))
                {
                    return new GameDBTransactionResult
                    {
                        Success = false,
                        FailureKind = GameDBTransactionFailureKind.AuthorizationDenied,
                        DeniedCommandIndex = index,
                        DeniedCommandKind = descriptors[index].Kind,
                        RevisionBefore = revisionBefore,
                        Message = $"Destructive command is not authorized: {descriptors[index].Kind}."
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(options.ExpectedRevision)
                && !string.Equals(options.ExpectedRevision, revisionBefore,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(GameDBTransactionFailureKind.RevisionConflict,
                    $"Revision conflict. Expected {options.ExpectedRevision}, but the document is {revisionBefore}.",
                    revisionBefore);
            }

            if (commandArray.Length == 0)
            {
                var state = GameDBModelCodec.Serialize(m_model);
                return new GameDBTransactionResult
                {
                    Success = true,
                    FailureKind = GameDBTransactionFailureKind.None,
                    RevisionBefore = revisionBefore,
                    AttemptedRevision = revisionBefore,
                    AttemptedState = state,
                    AttemptedSnapshot = GameDBModelCodec.CreateSnapshot(AssetPath, SchemaAssetPath, m_model)
                };
            }

            GameDB stage;
            try
            {
                stage = GameDBModelCodec.CreateDetachedModel(m_model);
            }
            catch (Exception exception)
            {
                return Failure(GameDBTransactionFailureKind.StageCloneFailed,
                    exception.Message, revisionBefore);
            }

            var context = new GameDBCommandContext(stage);
            for (var index = 0; index < commandArray.Length; index++)
            {
                GameDBCommandExecution execution;
                try
                {
                    execution = commandArray[index].Execute(context);
                    if (execution == null)
                    {
                        result = CaptureAttempt(stage, revisionBefore);
                        result.Success = false;
                        result.FailureKind = GameDBTransactionFailureKind.CommandFailed;
                        result.FailedCommandIndex = index;
                        result.Message = "Command returned no execution result.";
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    result = CaptureAttempt(stage, revisionBefore);
                    result.Success = false;
                    result.FailureKind = GameDBTransactionFailureKind.CommandThrew;
                    result.FailedCommandIndex = index;
                    result.Message = exception.Message;
                    return result;
                }

                if (!execution.Success)
                {
                    result = CaptureAttempt(stage, revisionBefore);
                    result.Success = false;
                    result.FailureKind = GameDBTransactionFailureKind.CommandFailed;
                    result.FailedCommandIndex = index;
                    result.Message = execution.Message;
                    return result;
                }
            }

            GameDBSerializedState attemptedState;
            try
            {
                attemptedState = GameDBModelCodec.Serialize(stage);
            }
            catch (Exception exception)
            {
                return Failure(GameDBTransactionFailureKind.AttemptedStateFailed,
                    exception.Message, revisionBefore);
            }

            List<GameDBValidationIssue> issues;
            try
            {
                issues = GameDBModelOperations.Validate(stage);
            }
            catch (Exception exception)
            {
                return new GameDBTransactionResult
                {
                    Success = false,
                    FailureKind = GameDBTransactionFailureKind.ValidationThrew,
                    Message = exception.Message,
                    RevisionBefore = revisionBefore,
                    AttemptedRevision = attemptedState.Revision,
                    AttemptedState = attemptedState
                };
            }

            GameDBSnapshot attemptedSnapshot;
            try
            {
                attemptedSnapshot = GameDBModelCodec.CreateSnapshot(AssetPath, SchemaAssetPath, stage);
            }
            catch (Exception exception)
            {
                return new GameDBTransactionResult
                {
                    Success = false,
                    FailureKind = GameDBTransactionFailureKind.SnapshotFailed,
                    Message = exception.Message,
                    RevisionBefore = revisionBefore,
                    AttemptedRevision = attemptedState.Revision,
                    AttemptedState = attemptedState,
                    Issues = issues.AsReadOnly()
                };
            }

            var changes = descriptors.Select(descriptor => descriptor.Kind).ToArray();
            if (issues.Count > 0)
            {
                return new GameDBTransactionResult
                {
                    Success = false,
                    FailureKind = GameDBTransactionFailureKind.ValidationFailed,
                    Message = $"Transaction blocked by {issues.Count} validation issue(s).",
                    RevisionBefore = revisionBefore,
                    AttemptedRevision = attemptedState.Revision,
                    AttemptedState = attemptedState,
                    AttemptedSnapshot = attemptedSnapshot,
                    Issues = issues.AsReadOnly(),
                    Changes = changes
                };
            }

            result = new GameDBTransactionResult
            {
                Success = true,
                FailureKind = GameDBTransactionFailureKind.None,
                RevisionBefore = revisionBefore,
                AttemptedRevision = attemptedState.Revision,
                AttemptedState = attemptedState,
                AttemptedSnapshot = attemptedSnapshot,
                Issues = issues.AsReadOnly(),
                Changes = changes
            };

            if (string.Equals(revisionBefore, attemptedState.Revision,
                StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            GameDB committedModel;
            try
            {
                committedModel = GameDBModelCodec.Import(
                    attemptedState.DataJson, attemptedState.SchemaJson, stage.LoadedPath);
            }
            catch (Exception exception)
            {
                return new GameDBTransactionResult
                {
                    Success = false,
                    FailureKind = GameDBTransactionFailureKind.StageCloneFailed,
                    Message = exception.Message,
                    RevisionBefore = revisionBefore,
                    AttemptedRevision = attemptedState.Revision,
                    AttemptedState = attemptedState,
                    AttemptedSnapshot = attemptedSnapshot,
                    Issues = issues.AsReadOnly(),
                    Changes = changes
                };
            }

            m_model = committedModel;
            m_currentRevision = attemptedState.Revision;
            change = new GameDBDocumentChange(DocumentId, revisionBefore,
                attemptedState.Revision, changes);
            return result;
        }

        private GameDBTransactionResult CaptureAttempt(GameDB stage, string revisionBefore)
        {
            var result = new GameDBTransactionResult { RevisionBefore = revisionBefore };
            var errors = new List<string>();
            try
            {
                result.AttemptedState = GameDBModelCodec.Serialize(stage);
                result.AttemptedRevision = result.AttemptedState.Revision;
            }
            catch (Exception exception)
            {
                errors.Add($"Attempted state could not be serialized: {exception.Message}");
            }

            try
            {
                result.AttemptedSnapshot = GameDBModelCodec.CreateSnapshot(
                    AssetPath, SchemaAssetPath, stage);
            }
            catch (Exception exception)
            {
                errors.Add($"Attempted snapshot could not be created: {exception.Message}");
            }

            result.AttemptedMetadataErrors = errors.AsReadOnly();
            return result;
        }

        private void DrainChanges()
        {
            while (true)
            {
                PendingChange pending;
                Action<GameDBDocumentChange> subscribers;
                lock (m_gate)
                {
                    if (m_pendingChanges.Count == 0)
                    {
                        m_drainingChanges = false;
                        return;
                    }

                    pending = m_pendingChanges.Dequeue();
                    subscribers = m_changed;
                }

                var errors = new List<string>();
                if (subscribers != null)
                {
                    foreach (Action<GameDBDocumentChange> subscriber in subscribers.GetInvocationList())
                    {
                        try
                        {
                            subscriber(pending.Change);
                        }
                        catch (Exception exception)
                        {
                            errors.Add(exception.Message);
                        }
                    }
                }

                pending.Result.NotificationErrors = errors.AsReadOnly();
            }
        }

        private string GetCurrentRevisionLocked()
        {
            if (m_currentRevision == null)
            {
                m_currentRevision = GameDBModelCodec.ComputeRevision(m_model);
            }

            return m_currentRevision;
        }

        private static Dictionary<Type, CommandDescriptor> CreateCommandDescriptors()
        {
            return new Dictionary<Type, CommandDescriptor>
            {
                { typeof(AddTableCommand), new CommandDescriptor(GameDBCommandKind.AddTable, false) },
                { typeof(RenameTableCommand), new CommandDescriptor(GameDBCommandKind.RenameTable, true) },
                { typeof(DeleteTableCommand), new CommandDescriptor(GameDBCommandKind.DeleteTable, true) },
                { typeof(AddFieldCommand), new CommandDescriptor(GameDBCommandKind.AddField, false) },
                { typeof(ReplaceFieldCommand), new CommandDescriptor(GameDBCommandKind.ReplaceField, true) },
                { typeof(RenameFieldCommand), new CommandDescriptor(GameDBCommandKind.RenameField, true) },
                { typeof(DeleteFieldCommand), new CommandDescriptor(GameDBCommandKind.DeleteField, true) },
                { typeof(AddRowCommand), new CommandDescriptor(GameDBCommandKind.AddRow, false) },
                { typeof(UpdateRowCommand), new CommandDescriptor(GameDBCommandKind.UpdateRow, false) },
                { typeof(SetValueCommand), new CommandDescriptor(GameDBCommandKind.SetValue, false) },
                { typeof(RenameRowCommand), new CommandDescriptor(GameDBCommandKind.RenameRow, true) },
                { typeof(DeleteRowCommand), new CommandDescriptor(GameDBCommandKind.DeleteRow, true) },
                { typeof(UpsertTableRowsCommand), new CommandDescriptor(GameDBCommandKind.UpsertTableRows, false) },
                { typeof(ReplaceTableRowsCommand), new CommandDescriptor(GameDBCommandKind.ReplaceTableRows, true) }
            };
        }

        private static GameDBTransactionResult Failure(GameDBTransactionFailureKind kind,
            string message, string revisionBefore)
        {
            return new GameDBTransactionResult
            {
                Success = false,
                FailureKind = kind,
                Message = message,
                RevisionBefore = revisionBefore
            };
        }

        private sealed class CommandDescriptor
        {
            internal GameDBCommandKind Kind { get; }
            internal bool IsDestructive { get; }

            internal CommandDescriptor(GameDBCommandKind kind, bool isDestructive)
            {
                Kind = kind;
                IsDestructive = isDestructive;
            }
        }

        private sealed class PendingChange
        {
            internal GameDBDocumentChange Change { get; }
            internal GameDBTransactionResult Result { get; }

            internal PendingChange(GameDBDocumentChange change, GameDBTransactionResult result)
            {
                Change = change;
                Result = result;
            }
        }

        private static void RequireCompletePair(GameDBPairRead pair, bool requireExisting)
        {
            if (!pair.Token.DataExists && !pair.Token.SchemaExists)
            {
                if (requireExisting)
                {
                    throw new FileNotFoundException("Database file does not exist.", pair.Path.AssetPath);
                }

                return;
            }

            if (!pair.Token.DataExists)
            {
                throw new FileNotFoundException("Database file does not exist.", pair.Path.AssetPath);
            }

            if (!pair.Token.SchemaExists)
            {
                throw new FileNotFoundException("Database schema file does not exist.", pair.Path.SchemaAssetPath);
            }
        }
    }
}
