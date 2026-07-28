using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace GameDBEditorLibrary.Workspace
{
    internal interface IGameDBEditorFacadeTarget
    {
        bool LoadGameDB(string gameDBPath);
        bool SaveGameDB();
        void AddRowToTable(string table, string key,
            Dictionary<string, object> data);
    }

    internal sealed class GameDBWorkspaceRegistration : IDisposable
    {
        private readonly GameDBActiveWorkspaceHub m_owner;
        private GameDBActiveWorkspaceHub m_hub;

        internal long RegistrationId { get; }
        internal bool IsDisposed => Volatile.Read(ref m_hub) == null;

        internal GameDBWorkspaceRegistration(GameDBActiveWorkspaceHub hub,
            long registrationId)
        {
            m_owner = hub;
            m_hub = hub;
            RegistrationId = registrationId;
        }

        internal bool MarkFocused()
        {
            return Volatile.Read(ref m_hub)?.MarkFocused(this) ?? false;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref m_hub, null)?.Unregister(this);
        }

        internal bool IsOwnedBy(GameDBActiveWorkspaceHub hub)
        {
            return ReferenceEquals(m_owner, hub);
        }

        internal void Invalidate()
        {
            Interlocked.Exchange(ref m_hub, null);
        }
    }

    internal sealed class GameDBActiveWorkspaceHub
    {
        private sealed class Entry
        {
            internal long RegistrationId { get; }
            internal WeakReference Target { get; }
            internal GameDBWorkspaceRegistration Registration { get; }
            internal long FocusSequence { get; set; }

            internal Entry(long registrationId, IGameDBEditorFacadeTarget target,
                GameDBWorkspaceRegistration registration, long focusSequence)
            {
                RegistrationId = registrationId;
                Target = new WeakReference(target);
                Registration = registration;
                FocusSequence = focusSequence;
            }
        }

        private readonly object m_gate = new object();
        private readonly List<Entry> m_entries = new List<Entry>();
        private long m_nextRegistrationId = 1;
        private long m_nextFocusSequence = 1;

        internal GameDBWorkspaceRegistration Register(IGameDBEditorFacadeTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            lock (m_gate)
            {
                var inheritedFocusSequence = 0L;
                for (var index = m_entries.Count - 1; index >= 0; index--)
                {
                    var existingTarget = m_entries[index].Target.Target;
                    if (existingTarget == null)
                    {
                        m_entries[index].Registration.Invalidate();
                        m_entries.RemoveAt(index);
                    }
                    else if (ReferenceEquals(existingTarget, target))
                    {
                        inheritedFocusSequence = Math.Max(inheritedFocusSequence,
                            m_entries[index].FocusSequence);
                        m_entries[index].Registration.Invalidate();
                        m_entries.RemoveAt(index);
                    }
                }

                var registrationId = m_nextRegistrationId++;
                var registration = new GameDBWorkspaceRegistration(this, registrationId);
                m_entries.Add(new Entry(registrationId, target, registration,
                    inheritedFocusSequence));
                return registration;
            }
        }

        internal bool TryGetActive(out IGameDBEditorFacadeTarget target)
        {
            lock (m_gate)
            {
                target = null;
                Entry activeEntry = null;
                foreach (var entry in m_entries.ToArray())
                {
                    var liveTarget = entry.Target.Target as IGameDBEditorFacadeTarget;
                    if (liveTarget == null)
                    {
                        entry.Registration.Invalidate();
                        m_entries.Remove(entry);
                    }
                    else if (entry.FocusSequence > 0
                        && (activeEntry == null
                            || entry.FocusSequence > activeEntry.FocusSequence))
                    {
                        activeEntry = entry;
                        target = liveTarget;
                    }
                }
                return target != null;
            }
        }

        internal int RegistrationCount
        {
            get
            {
                lock (m_gate)
                {
                    RemoveDeadEntriesLocked();
                    return m_entries.Count;
                }
            }
        }

        internal bool MarkFocused(GameDBWorkspaceRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }
            if (!registration.IsOwnedBy(this))
            {
                return false;
            }

            lock (m_gate)
            {
                RemoveDeadEntriesLocked();
                var entry = m_entries.FirstOrDefault(candidate =>
                    candidate.RegistrationId == registration.RegistrationId);
                if (entry == null)
                {
                    return false;
                }

                entry.FocusSequence = m_nextFocusSequence++;
                return true;
            }
        }

        internal void Unregister(GameDBWorkspaceRegistration registration)
        {
            if (registration == null || !registration.IsOwnedBy(this))
            {
                return;
            }

            lock (m_gate)
            {
                m_entries.RemoveAll(entry =>
                    entry.RegistrationId == registration.RegistrationId);
            }
        }

        private void RemoveDeadEntriesLocked()
        {
            for (var index = m_entries.Count - 1; index >= 0; index--)
            {
                if (m_entries[index].Target.Target == null)
                {
                    m_entries[index].Registration.Invalidate();
                    m_entries.RemoveAt(index);
                }
            }
        }
    }

    internal sealed class GameDBLegacyHeadlessFacadeTarget : IGameDBEditorFacadeTarget
    {
        internal static GameDBLegacyHeadlessFacadeTarget Instance { get; }
            = new GameDBLegacyHeadlessFacadeTarget();

        private GameDBLegacyHeadlessFacadeTarget()
        {
        }

        public bool LoadGameDB(string gameDBPath)
        {
            return GameDB.Instance.Load(gameDBPath);
        }

        public bool SaveGameDB()
        {
            return GameDB.Instance.Save();
        }

        public void AddRowToTable(string table, string key,
            Dictionary<string, object> data)
        {
            GameDB.Instance.AddRowToTable(table, key, data);
        }
    }

    internal sealed class GameDBEditorFacadeRouter : IGameDBEditorFacadeTarget
    {
        private readonly GameDBActiveWorkspaceHub m_hub;
        private readonly IGameDBEditorFacadeTarget m_headlessTarget;

        internal GameDBEditorFacadeRouter(GameDBActiveWorkspaceHub hub,
            IGameDBEditorFacadeTarget headlessTarget)
        {
            m_hub = hub ?? throw new ArgumentNullException(nameof(hub));
            m_headlessTarget = headlessTarget
                ?? throw new ArgumentNullException(nameof(headlessTarget));
        }

        public bool LoadGameDB(string gameDBPath)
        {
            try
            {
                return ResolveTarget().LoadGameDB(gameDBPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"failed to load gameDB: {gameDBPath}");
                Debug.LogException(exception);
                return false;
            }
        }

        public bool SaveGameDB()
        {
            try
            {
                return ResolveTarget().SaveGameDB();
            }
            catch (Exception exception)
            {
                Debug.LogError("failed to save gameDB");
                Debug.LogException(exception);
                return false;
            }
        }

        public void AddRowToTable(string table, string key,
            Dictionary<string, object> data)
        {
            ResolveTarget().AddRowToTable(table, key, data);
        }

        private IGameDBEditorFacadeTarget ResolveTarget()
        {
            return m_hub.TryGetActive(out var active) ? active : m_headlessTarget;
        }
    }
}
