using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        internal GameDBProjectSettingsSnapshot(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath,
            IEnumerable<GameDBProjectSettingsIssue> validationIssues)
        {
            m_registeredDatabasePaths = registeredDatabasePaths.ToArray();
            m_importedEnumTypeNames = importedEnumTypeNames.ToArray();
            m_validationIssues = validationIssues.ToArray();
            RegisteredDatabasePaths = new ReadOnlyCollection<string>(m_registeredDatabasePaths);
            ImportedEnumTypeNames = new ReadOnlyCollection<string>(m_importedEnumTypeNames);
            ValidationIssues = new ReadOnlyCollection<GameDBProjectSettingsIssue>(m_validationIssues);
            ExportPath = exportPath;
            BuildPath = buildPath;
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

        internal GameDBProjectSettingsResult(bool success, bool changed,
            GameDBProjectSettingsSnapshot snapshot, string error,
            IEnumerable<string> notificationErrors = null)
        {
            Success = success;
            Changed = changed;
            Snapshot = snapshot;
            Error = error;
            NotificationErrors = new ReadOnlyCollection<string>(
                (notificationErrors ?? Array.Empty<string>()).ToArray());
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
                path => File.Exists(Path.Combine(Application.dataPath, path)),
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
            if (!m_store.Exists)
            {
                m_snapshot = CreateSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                    string.Empty, string.Empty);
                return new GameDBProjectSettingsResult(true, false, m_snapshot, null);
            }

            try
            {
                if (!(JsonSerialization.Deserialize(m_store.ReadAllText())
                    is IDictionary<string, object> settings))
                {
                    throw new FormatException("GameDB settings must contain a JSON object.");
                }

                m_snapshot = CreateSnapshot(ReadStringList(settings, "gameDBPaths"),
                    ReadStringList(settings, "importedEnums"),
                    ReadString(settings, "exportPath"), ReadString(settings, "buildPath"));
                return new GameDBProjectSettingsResult(true, false, m_snapshot, null);
            }
            catch (Exception exception)
            {
                m_snapshot = CreateSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                    string.Empty, string.Empty);
                m_loadError = $"Failed to load GameDB project settings: {exception.Message}";
                return new GameDBProjectSettingsResult(false, false, m_snapshot, m_loadError);
            }
        }

        internal GameDBProjectSettingsSnapshot GetSnapshot()
        {
            return Load().Snapshot;
        }

        internal GameDBProjectSettingsResult Update(IEnumerable<string> registeredDatabasePaths,
            IEnumerable<string> importedEnumTypeNames, string exportPath, string buildPath)
        {
            var loaded = Load();
            var previous = loaded.Snapshot;
            if (!loaded.Success)
            {
                return new GameDBProjectSettingsResult(false, false, previous, loaded.Error);
            }

            var current = CreateSnapshot(registeredDatabasePaths, importedEnumTypeNames,
                exportPath, buildPath);
            if (previous.HasSameValues(current))
            {
                return new GameDBProjectSettingsResult(true, false, previous, null);
            }

            try
            {
                m_store.WriteAtomically(Serialize(current));
            }
            catch (Exception exception)
            {
                return new GameDBProjectSettingsResult(false, false, previous,
                    $"Failed to save GameDB project settings: {exception.Message}");
            }

            m_snapshot = current;
            var notificationErrors = NotifyChanged(new GameDBProjectSettingsChange(previous, current));
            return new GameDBProjectSettingsResult(true, true, current, null, notificationErrors);
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

            return new GameDBProjectSettingsSnapshot(paths, enumTypes,
                NormalizePath(exportPath), NormalizePath(buildPath), issues);
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
            var settings = new Dictionary<string, object>
            {
                { "gameDBPaths", snapshot.RegisteredDatabasePaths.ToArray() },
                { "exportPath", snapshot.ExportPath },
                { "importedEnums", snapshot.ImportedEnumTypeNames.ToArray() },
                { "buildPath", snapshot.BuildPath }
            };
            return JsonHelper.FormatJson(JsonSerialization.Serialize(settings));
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
