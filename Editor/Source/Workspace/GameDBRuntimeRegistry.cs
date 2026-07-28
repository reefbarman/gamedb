using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace GameDBEditorLibrary.Workspace
{
    internal sealed class GameDBRuntimeTargetDescriptor
    {
        internal string TargetId { get; }
        internal string Name { get; }
        internal string DisplayName { get; }
        internal long Epoch { get; }

        internal GameDBRuntimeTargetDescriptor(string targetId, string name,
            string displayName, long epoch)
        {
            TargetId = targetId;
            Name = name;
            DisplayName = displayName;
            Epoch = epoch;
        }
    }

    internal sealed class GameDBRuntimeRegistrySnapshot
    {
        private readonly GameDBRuntimeTargetDescriptor[] m_targets;

        internal long Epoch { get; }
        internal long Revision { get; }
        internal IReadOnlyList<GameDBRuntimeTargetDescriptor> Targets { get; }

        internal GameDBRuntimeRegistrySnapshot(long epoch, long revision,
            IEnumerable<GameDBRuntimeTargetDescriptor> targets)
        {
            Epoch = epoch;
            Revision = revision;
            m_targets = targets.ToArray();
            Targets = new ReadOnlyCollection<GameDBRuntimeTargetDescriptor>(m_targets);
        }
    }

    internal sealed class GameDBRuntimeRegistryResult
    {
        internal GameDBRuntimeTargetDescriptor Target { get; }
        internal GameDBRuntimeRegistrySnapshot Snapshot { get; }
        internal bool Changed { get; }
        internal IReadOnlyList<string> NotificationErrors { get; }

        internal GameDBRuntimeRegistryResult(GameDBRuntimeTargetDescriptor target,
            GameDBRuntimeRegistrySnapshot snapshot, bool changed,
            IEnumerable<string> notificationErrors = null)
        {
            Target = target;
            Snapshot = snapshot;
            Changed = changed;
            NotificationErrors = new ReadOnlyCollection<string>(
                (notificationErrors ?? Array.Empty<string>()).ToArray());
        }
    }

    internal sealed class GameDBRuntimeRegistry
    {
        private sealed class Entry
        {
            internal string TargetId { get; }
            internal WeakReference Target { get; }

            internal Entry(string targetId, GameDBBase target)
            {
                TargetId = targetId;
                Target = new WeakReference(target);
            }
        }

        private readonly object m_gate = new object();
        private readonly List<Entry> m_entries = new List<Entry>();
        private long m_epoch = 1;
        private long m_revision;
        private long m_nextTargetId = 1;
        private Action<GameDBRuntimeRegistrySnapshot> m_changed;

        internal event Action<GameDBRuntimeRegistrySnapshot> Changed
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

        internal GameDBRuntimeRegistryResult Register(GameDBBase target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            GameDBRuntimeRegistrySnapshot snapshot;
            GameDBRuntimeTargetDescriptor descriptor;
            Action<GameDBRuntimeRegistrySnapshot> subscribers;
            lock (m_gate)
            {
                RemoveDeadEntriesLocked();
                var existing = m_entries.FirstOrDefault(entry =>
                    ReferenceEquals(entry.Target.Target, target));
                if (existing != null)
                {
                    snapshot = CreateSnapshotLocked();
                    descriptor = snapshot.Targets.First(item =>
                        item.TargetId == existing.TargetId);
                    return new GameDBRuntimeRegistryResult(descriptor, snapshot, false);
                }

                var entry = new Entry($"runtime-{m_epoch}-{m_nextTargetId++}", target);
                m_entries.Add(entry);
                m_revision++;
                snapshot = CreateSnapshotLocked();
                descriptor = snapshot.Targets.First(item => item.TargetId == entry.TargetId);
                subscribers = m_changed;
            }

            return new GameDBRuntimeRegistryResult(descriptor, snapshot, true,
                Notify(subscribers, snapshot));
        }

        internal GameDBRuntimeRegistryResult BeginPlaySession()
        {
            GameDBRuntimeRegistrySnapshot snapshot;
            Action<GameDBRuntimeRegistrySnapshot> subscribers;
            lock (m_gate)
            {
                m_entries.Clear();
                m_epoch++;
                m_revision++;
                m_nextTargetId = 1;
                snapshot = CreateSnapshotLocked();
                subscribers = m_changed;
            }

            return new GameDBRuntimeRegistryResult(null, snapshot, true,
                Notify(subscribers, snapshot));
        }

        internal GameDBRuntimeRegistrySnapshot GetSnapshot()
        {
            Action<GameDBRuntimeRegistrySnapshot> subscribers = null;
            GameDBRuntimeRegistrySnapshot snapshot;
            bool removed;
            lock (m_gate)
            {
                removed = RemoveDeadEntriesLocked();
                if (removed)
                {
                    m_revision++;
                }
                snapshot = CreateSnapshotLocked();
                if (removed)
                {
                    subscribers = m_changed;
                }
            }

            if (removed)
            {
                Notify(subscribers, snapshot);
            }

            return snapshot;
        }

        internal bool TryResolve(string targetId, out GameDBBase target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            lock (m_gate)
            {
                RemoveDeadEntriesLocked();
                var entry = m_entries.FirstOrDefault(item => item.TargetId == targetId);
                if (entry == null)
                {
                    return false;
                }

                target = entry.Target.Target as GameDBBase;
                return target != null;
            }
        }

        private bool RemoveDeadEntriesLocked()
        {
            return m_entries.RemoveAll(entry => !entry.Target.IsAlive) > 0;
        }

        private GameDBRuntimeRegistrySnapshot CreateSnapshotLocked()
        {
            var liveEntries = m_entries
                .Select(entry => new { Entry = entry, Target = entry.Target.Target as GameDBBase })
                .Where(item => item.Target != null)
                .ToArray();
            var totalsByName = liveEntries
                .GroupBy(item => NormalizeName(item.Target.Name), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var occurrenceByName = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedDisplayNames = new HashSet<string>(StringComparer.Ordinal);
            var descriptors = new List<GameDBRuntimeTargetDescriptor>(liveEntries.Length);

            foreach (var item in liveEntries)
            {
                var name = NormalizeName(item.Target.Name);
                occurrenceByName.TryGetValue(name, out var occurrence);
                occurrence++;
                occurrenceByName[name] = occurrence;
                var candidate = totalsByName[name] == 1
                    ? name
                    : $"{name} ({occurrence})";
                var displayName = candidate;
                var suffix = 2;
                while (!usedDisplayNames.Add(displayName))
                {
                    displayName = $"{candidate} [{suffix++}]";
                }
                descriptors.Add(new GameDBRuntimeTargetDescriptor(item.Entry.TargetId,
                    name, displayName, m_epoch));
            }

            return new GameDBRuntimeRegistrySnapshot(m_epoch, m_revision, descriptors);
        }

        private static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Unnamed GameDB" : name.Trim();
        }

        private static IReadOnlyList<string> Notify(
            Action<GameDBRuntimeRegistrySnapshot> subscribers,
            GameDBRuntimeRegistrySnapshot snapshot)
        {
            var errors = new List<string>();
            if (subscribers == null)
            {
                return errors;
            }

            foreach (Action<GameDBRuntimeRegistrySnapshot> subscriber
                in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(snapshot);
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            return errors;
        }
    }

    [InitializeOnLoad]
    internal static class GameDBEditorDomainServices
    {
        internal static GameDBRuntimeRegistry RuntimeRegistry { get; }
            = new GameDBRuntimeRegistry();
        internal static GameDBActiveWorkspaceHub ActiveWorkspaceHub { get; }
            = new GameDBActiveWorkspaceHub();
        internal static GameDBProjectSettingsService ProjectSettings { get; }
            = GameDBProjectSettingsService.CreateDefault();
        internal static GameDBEditorFacadeRouter FacadeRouter { get; }
            = new GameDBEditorFacadeRouter(ActiveWorkspaceHub,
                GameDBLegacyHeadlessFacadeTarget.Instance);

        static GameDBEditorDomainServices()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static GameDBRuntimeRegistryResult BeginPlaySession()
        {
            return RuntimeRegistry.BeginPlaySession();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode
                || state == PlayModeStateChange.ExitingPlayMode)
            {
                BeginPlaySession();
            }
        }
    }
}
