using GameDBEditorLibrary.Automation;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary.Documents
{
    internal sealed class GameDBHistoryState
    {
        internal bool CanUndo { get; }
        internal bool CanRedo { get; }
        internal string UndoLabel { get; }
        internal string RedoLabel { get; }

        internal GameDBHistoryState(bool canUndo, bool canRedo,
            string undoLabel, string redoLabel)
        {
            CanUndo = canUndo;
            CanRedo = canRedo;
            UndoLabel = undoLabel;
            RedoLabel = redoLabel;
        }
    }

    internal sealed class GameDBHistoryResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; }
        internal GameDBWorkingStateFailureKind FailureKind { get; set; }
        internal GameDBSnapshot Snapshot { get; set; }
        internal IReadOnlyList<string> NotificationErrors { get; set; }
            = Array.Empty<string>();
    }

    internal sealed class GameDBDocumentHistory
    {
        internal const int DefaultMaximumEntries = 50;

        private sealed class Entry
        {
            internal GameDBSerializedState State { get; }
            internal string Label { get; }

            internal Entry(GameDBSerializedState state, string label)
            {
                State = state;
                Label = label;
            }
        }

        private readonly int m_maximumEntries;
        private readonly List<Entry> m_entries = new List<Entry>();
        private int m_cursor;

        internal GameDBDocumentHistory(GameDBSerializedState initial,
            int maximumEntries = DefaultMaximumEntries)
        {
            if (initial == null)
            {
                throw new ArgumentNullException(nameof(initial));
            }
            if (maximumEntries < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEntries));
            }
            m_maximumEntries = maximumEntries;
            m_entries.Add(new Entry(initial, null));
        }

        internal GameDBHistoryState GetState()
        {
            return new GameDBHistoryState(m_cursor > 0,
                m_cursor + 1 < m_entries.Count,
                m_cursor > 0 ? m_entries[m_cursor].Label : null,
                m_cursor + 1 < m_entries.Count ? m_entries[m_cursor + 1].Label : null);
        }

        internal GameDBSerializedState PeekUndo()
        {
            return m_cursor > 0 ? m_entries[m_cursor - 1].State : null;
        }

        internal GameDBSerializedState PeekRedo()
        {
            return m_cursor + 1 < m_entries.Count ? m_entries[m_cursor + 1].State : null;
        }

        internal void Record(GameDBSerializedState state, string label)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (string.Equals(m_entries[m_cursor].State.Revision, state.Revision,
                StringComparison.OrdinalIgnoreCase))
            {
                m_entries[m_cursor] = new Entry(state, m_entries[m_cursor].Label);
                return;
            }
            if (m_cursor + 1 < m_entries.Count)
            {
                m_entries.RemoveRange(m_cursor + 1, m_entries.Count - m_cursor - 1);
            }
            m_entries.Add(new Entry(state, string.IsNullOrWhiteSpace(label)
                ? "Edit GameDB" : label.Trim()));
            m_cursor = m_entries.Count - 1;
            while (m_entries.Count > m_maximumEntries)
            {
                m_entries.RemoveAt(0);
                m_cursor--;
            }
        }

        internal bool CanMove(bool redo, string revision)
        {
            var target = redo ? m_cursor + 1 : m_cursor - 1;
            return target >= 0 && target < m_entries.Count
                && string.Equals(m_entries[target].State.Revision, revision,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal void CommitMove(bool redo)
        {
            m_cursor += redo ? 1 : -1;
        }

        internal void SynchronizeCurrent(GameDBSerializedState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            var label = m_entries[m_cursor].Label;
            m_entries[m_cursor] = new Entry(state, label);
            if (m_cursor > 0 && string.Equals(m_entries[m_cursor - 1].State.Revision,
                state.Revision, StringComparison.OrdinalIgnoreCase))
            {
                m_entries.RemoveAt(m_cursor);
                m_cursor--;
            }
            if (m_cursor + 1 < m_entries.Count && string.Equals(
                m_entries[m_cursor + 1].State.Revision, state.Revision,
                StringComparison.OrdinalIgnoreCase))
            {
                m_entries.RemoveAt(m_cursor + 1);
            }
        }
    }
}
