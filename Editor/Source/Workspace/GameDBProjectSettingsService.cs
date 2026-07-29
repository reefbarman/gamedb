using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GameDBEditorLibrary.Workspace
{
    internal enum GameDBProjectSettingsIssueKind
    {
        MissingDatabasePath,
        UnresolvedImportedEnumType
    }

    internal sealed class GameDBProjectSettingsIssue
    {
        internal GameDBProjectSettingsIssueKind Kind { get; }
        internal string Value { get; }

        internal GameDBProjectSettingsIssue(GameDBProjectSettingsIssueKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }
    }

    internal enum GameDBProjectSettingsCommitStatus
    {
        NotAttempted,
        DryRun,
        Saved,
        NoChanges,
        ValidationFailed,
        Conflict,
        PersistenceFailed
    }

    internal sealed class GameDBProjectSettingsSnapshot
    {
        private readonly string[] m_registeredDatabasePaths;
        private readonly string[] m_importedEnumTypeNames;
        private readonly GameDBProjectSettingsIssue[] m_validationIssues;

        internal IReadOnlyList<string> RegisteredDatabasePaths { get; }
        internal IReadOnlyList<string> ImportedEnumTypeNames { get; }
        internal string ExportPath { get; }
        internal string BuildPath { get; }
        internal IReadOnlyList<GameDBProjectSettingsIssue> ValidationIssues { get; }
        internal string Revision { get; }

        internal GameDBProjectSettingsSnapshot(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath,
            IEnumerable<GameDBProjectSettingsIssue> validationIssues, string revision)
        {
            m_registeredDatabasePaths = registeredDatabasePaths.ToArray();
            m_importedEnumTypeNames = importedEnumTypeNames.ToArray();
            m_validationIssues = validationIssues.ToArray();
            RegisteredDatabasePaths = new ReadOnlyCollection<string>(m_registeredDatabasePaths);
            ImportedEnumTypeNames = new ReadOnlyCollection<string>(m_importedEnumTypeNames);
            ValidationIssues = new ReadOnlyCollection<GameDBProjectSettingsIssue>(m_validationIssues);
            ExportPath = exportPath;
            BuildPath = buildPath;
            Revision = revision;
        }

        internal bool HasSameValues(GameDBProjectSettingsSnapshot other)
        {
            return other != null
                && ExportPath == other.ExportPath
                && BuildPath == other.BuildPath
                && m_registeredDatabasePaths.SequenceEqual(other.m_registeredDatabasePaths)
                && m_importedEnumTypeNames.SequenceEqual(other.m_importedEnumTypeNames);
        }

        internal bool HasSameValidation(GameDBProjectSettingsSnapshot other)
        {
            return other != null
                && m_validationIssues.Select(issue => new { issue.Kind, issue.Value })
                    .SequenceEqual(other.m_validationIssues.Select(issue =>
                        new { issue.Kind, issue.Value }));
        }
    }

    internal sealed class GameDBProjectSettingsChange
    {
        internal GameDBProjectSettingsSnapshot Previous { get; }
        internal GameDBProjectSettingsSnapshot Current { get; }

        internal GameDBProjectSettingsChange(GameDBProjectSettingsSnapshot previous,
            GameDBProjectSettingsSnapshot current)
        {
            Previous = previous;
            Current = current;
        }
    }

    internal sealed class GameDBProjectSettingsResult
    {
        internal bool Success { get; }
        internal bool Changed { get; }
        internal GameDBProjectSettingsSnapshot Snapshot { get; }
        internal string Error { get; }
        internal IReadOnlyList<string> NotificationErrors { get; }
        internal GameDBProjectSettingsCommitStatus CommitStatus { get; }
        internal string RevisionBefore { get; }

        internal GameDBProjectSettingsResult(bool success, bool changed,
            GameDBProjectSettingsSnapshot snapshot, string error,
            IEnumerable<string> notificationErrors = null,
            GameDBProjectSettingsCommitStatus commitStatus = GameDBProjectSettingsCommitStatus.NotAttempted,
            string revisionBefore = null)
        {
            Success = success;
            Changed = changed;
            Snapshot = snapshot;
            Error = error;
            NotificationErrors = new ReadOnlyCollection<string>(
                (notificationErrors ?? Array.Empty<string>()).ToArray());
            CommitStatus = commitStatus;
            RevisionBefore = revisionBefore;
        }
    }

    internal interface IGameDBProjectSettingsStore
    {
        bool Exists { get; }
        string ReadAllText();
        void WriteAtomically(string contents);
    }

    internal sealed class GameDBProjectSettingsFileStore : IGameDBProjectSettingsStore
    {
        private readonly string m_path;

        internal GameDBProjectSettingsFileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Settings path is required.", nameof(path));
            }

            m_path = Path.GetFullPath(path);
        }

        public bool Exists => File.Exists(m_path);

        public string ReadAllText()
        {
            return File.ReadAllText(m_path);
        }

        public void WriteAtomically(string contents)
        {
            var directory = Path.GetDirectoryName(m_path);
            Directory.CreateDirectory(directory);
            var temporaryPath = m_path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(temporaryPath, contents);
                if (File.Exists(m_path))
                {
                    File.Replace(temporaryPath, m_path, null);
                }
                else
                {
                    File.Move(temporaryPath, m_path);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                }
            }
        }
    }

    internal sealed class GameDBProjectSettingsService
    {
        private readonly IGameDBProjectSettingsStore m_store;
        private readonly Func<string, bool> m_databasePathExists;
        private readonly Func<string, bool> m_importedEnumTypeExists;
        private GameDBProjectSettingsSnapshot m_snapshot;
        private bool m_loaded;
        private string m_loadError;
        private Action<GameDBProjectSettingsChange> m_changed;

        internal event Action<GameDBProjectSettingsChange> Changed
        {
            add { m_changed += value; }
            remove { m_changed -= value; }
        }

        internal GameDBProjectSettingsService(IGameDBProjectSettingsStore store,
            Func<string, bool> databasePathExists, Func<string, bool> importedEnumTypeExists)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
            m_databasePathExists = databasePathExists
                ?? throw new ArgumentNullException(nameof(databasePathExists));
            m_importedEnumTypeExists = importedEnumTypeExists
                ?? throw new ArgumentNullException(nameof(importedEnumTypeExists));
        }

        internal static GameDBProjectSettingsService CreateDefault()
        {
            var settingsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "ProjectSettings", "GameDBSettings.json"));
            return new GameDBProjectSettingsService(
                new GameDBProjectSettingsFileStore(settingsPath),
                path => File.Exists(Path.Combine(Application.dataPath, path))
                    && File.Exists(Path.ChangeExtension(
                        Path.Combine(Application.dataPath, path), ".schema.json")),
                typeName =>
                {
                    AssemblyExplorer.Instance.Load();
                    var type = AssemblyExplorer.Instance.GetType(typeName);
                    return type != null && type.IsEnum;
                });
        }

        internal GameDBProjectSettingsResult Load()
        {
            if (m_loaded)
            {
                return new GameDBProjectSettingsResult(m_loadError == null, false,
                    m_snapshot, m_loadError);
            }

            m_loaded = true;
            var loaded = ReadStore();
            m_snapshot = loaded.Snapshot;
            m_loadError = loaded.Error;
            return loaded;
        }

        internal GameDBProjectSettingsResult Refresh()
        {
            if (!m_loaded)
            {
                return Load();
            }

            var previous = m_snapshot;
            var refreshed = ReadStore();
            if (!refreshed.Success)
            {
                m_loadError = refreshed.Error;
                return new GameDBProjectSettingsResult(false, false, previous,
                    refreshed.Error);
            }

            m_loadError = null;
            if (previous.HasSameValues(refreshed.Snapshot)
                && previous.HasSameValidation(refreshed.Snapshot))
            {
                return new GameDBProjectSettingsResult(true, false, previous, null);
            }

            m_snapshot = refreshed.Snapshot;
            var notificationErrors = NotifyChanged(
                new GameDBProjectSettingsChange(previous, m_snapshot));
            return new GameDBProjectSettingsResult(true, true, m_snapshot, null,
                notificationErrors);
        }

        internal GameDBProjectSettingsSnapshot GetSnapshot()
        {
            return Load().Snapshot;
        }

        internal GameDBProjectSettingsResult Update(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath)
        {
            return Update(registeredDatabasePaths, importedEnumTypeNames, exportPath, buildPath,
                false, null, false);
        }

        internal GameDBProjectSettingsResult Update(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath,
            bool dryRun, string expectedRevision, bool requireValid)
        {
            var loaded = Refresh();
            var previous = loaded.Snapshot;
            if (!loaded.Success)
            {
                return new GameDBProjectSettingsResult(false, false, previous, loaded.Error);
            }

            var current = CreateSnapshot(registeredDatabasePaths, importedEnumTypeNames,
                exportPath, buildPath);
            if (!string.IsNullOrWhiteSpace(expectedRevision)
                && !string.Equals(expectedRevision, previous.Revision,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new GameDBProjectSettingsResult(false, false, previous,
                    $"Revision conflict. Expected '{expectedRevision}', current revision is '{previous.Revision}'.",
                    commitStatus: GameDBProjectSettingsCommitStatus.Conflict,
                    revisionBefore: previous.Revision);
            }
            if (requireValid && current.ValidationIssues.Count > 0)
            {
                return new GameDBProjectSettingsResult(false, false, current,
                    $"Project settings have {current.ValidationIssues.Count} validation issue(s).",
                    commitStatus: GameDBProjectSettingsCommitStatus.ValidationFailed,
                    revisionBefore: previous.Revision);
            }
            if (previous.HasSameValues(current))
            {
                return new GameDBProjectSettingsResult(true, false, previous, null,
                    commitStatus: GameDBProjectSettingsCommitStatus.NoChanges,
                    revisionBefore: previous.Revision);
            }
            if (dryRun)
            {
                return new GameDBProjectSettingsResult(true, true, current, null,
                    commitStatus: GameDBProjectSettingsCommitStatus.DryRun,
                    revisionBefore: previous.Revision);
            }

            try
            {
                m_store.WriteAtomically(Serialize(current));
            }
            catch (Exception exception)
            {
                return new GameDBProjectSettingsResult(false, false, previous,
                    $"Failed to save GameDB project settings: {exception.Message}",
                    commitStatus: GameDBProjectSettingsCommitStatus.PersistenceFailed,
                    revisionBefore: previous.Revision);
            }

            m_snapshot = current;
            var notificationErrors = loaded.NotificationErrors.Concat(
                NotifyChanged(new GameDBProjectSettingsChange(previous, current))).ToArray();
            return new GameDBProjectSettingsResult(true, true, current, null, notificationErrors,
                GameDBProjectSettingsCommitStatus.Saved, previous.Revision);
        }

        internal GameDBProjectSettingsResult Revalidate()
        {
            var loaded = Load();
            var previous = loaded.Snapshot;
            if (!loaded.Success)
            {
                return new GameDBProjectSettingsResult(false, false, previous, loaded.Error);
            }

            var current = CreateSnapshot(previous.RegisteredDatabasePaths,
                previous.ImportedEnumTypeNames, previous.ExportPath, previous.BuildPath);
            if (previous.HasSameValidation(current))
            {
                return new GameDBProjectSettingsResult(true, false, previous, null);
            }

            m_snapshot = current;
            var notificationErrors = NotifyChanged(new GameDBProjectSettingsChange(previous, current));
            return new GameDBProjectSettingsResult(true, true, current, null, notificationErrors);
        }

        private GameDBProjectSettingsResult ReadStore()
        {
            if (!m_store.Exists)
            {
                return new GameDBProjectSettingsResult(true, false,
                    CreateSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                        string.Empty, string.Empty), null);
            }

            try
            {
                if (!(JsonSerialization.Deserialize(m_store.ReadAllText())
                    is IDictionary<string, object> settings))
                {
                    throw new FormatException("GameDB settings must contain a JSON object.");
                }

                return new GameDBProjectSettingsResult(true, false,
                    CreateSnapshot(ReadStringList(settings, "gameDBPaths"),
                        ReadStringList(settings, "importedEnums"),
                        ReadString(settings, "exportPath"),
                        ReadString(settings, "buildPath")), null);
            }
            catch (Exception exception)
            {
                return new GameDBProjectSettingsResult(false, false,
                    CreateSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                        string.Empty, string.Empty),
                    $"Failed to load GameDB project settings: {exception.Message}");
            }
        }

        private GameDBProjectSettingsSnapshot CreateSnapshot(
            IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath)
        {
            var paths = NormalizeValues(registeredDatabasePaths, false,
                value => value.Replace('\\', '/'));
            var enumTypes = NormalizeValues(importedEnumTypeNames, true, value => value);
            var issues = new List<GameDBProjectSettingsIssue>();

            foreach (var path in paths)
            {
                if (!Validate(m_databasePathExists, path))
                {
                    issues.Add(new GameDBProjectSettingsIssue(
                        GameDBProjectSettingsIssueKind.MissingDatabasePath, path));
                }
            }

            foreach (var typeName in enumTypes)
            {
                if (!Validate(m_importedEnumTypeExists, typeName))
                {
                    issues.Add(new GameDBProjectSettingsIssue(
                        GameDBProjectSettingsIssueKind.UnresolvedImportedEnumType, typeName));
                }
            }

            var normalizedExportPath = NormalizePath(exportPath);
            var normalizedBuildPath = NormalizePath(buildPath);
            return new GameDBProjectSettingsSnapshot(paths, enumTypes,
                normalizedExportPath, normalizedBuildPath, issues,
                ComputeRevision(paths, enumTypes, normalizedExportPath, normalizedBuildPath));
        }

        private static string[] NormalizeValues(IEnumerable<string> values, bool sort,
            Func<string, string> normalize)
        {
            var normalized = (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => normalize(value.Trim()))
                .Distinct(StringComparer.Ordinal);
            if (sort)
            {
                normalized = normalized.OrderBy(value => value, StringComparer.Ordinal);
            }

            return normalized.ToArray();
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static bool Validate(Func<string, bool> validator, string value)
        {
            try
            {
                return validator(value);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> ReadStringList(
            IDictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value)
                || !(value is IEnumerable<object> values))
            {
                return Array.Empty<string>();
            }

            return values.OfType<string>();
        }

        private static string ReadString(IDictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? value as string ?? string.Empty
                : string.Empty;
        }

        private static string Serialize(GameDBProjectSettingsSnapshot snapshot)
        {
            return JsonHelper.FormatJson(JsonSerialization.Serialize(CreateWireValues(
                snapshot.RegisteredDatabasePaths, snapshot.ImportedEnumTypeNames,
                snapshot.ExportPath, snapshot.BuildPath)));
        }

        private static string ComputeRevision(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath)
        {
            var json = JsonSerialization.Serialize(CreateWireValues(registeredDatabasePaths,
                importedEnumTypeNames, exportPath, buildPath));
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(json))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static Dictionary<string, object> CreateWireValues(
            IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath)
        {
            return new Dictionary<string, object>
            {
                { "gameDBPaths", registeredDatabasePaths.ToArray() },
                { "exportPath", exportPath },
                { "importedEnums", importedEnumTypeNames.ToArray() },
                { "buildPath", buildPath }
            };
        }

        private IReadOnlyList<string> NotifyChanged(GameDBProjectSettingsChange change)
        {
            var errors = new List<string>();
            var subscribers = m_changed;
            if (subscribers == null)
            {
                return errors;
            }

            foreach (Action<GameDBProjectSettingsChange> subscriber
                in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(change);
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            return errors;
        }
    }
}
