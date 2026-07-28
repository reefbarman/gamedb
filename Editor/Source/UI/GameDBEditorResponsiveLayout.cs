using System;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBEditorResponsiveLayout : IDisposable
    {
        internal const float CompactWidth = 760f;
        internal const float NarrowWidth = 520f;
        internal const string CompactClass = "gamedb-editor--compact";
        internal const string NarrowClass = "gamedb-editor--narrow";

        private readonly VisualElement m_root;
        private bool m_disposed;

        internal GameDBEditorResponsiveLayout(VisualElement root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            m_root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Apply(m_root.resolvedStyle.width);
        }

        internal void Apply(float width)
        {
            if (m_disposed || float.IsNaN(width))
            {
                return;
            }

            m_root.EnableInClassList(CompactClass, width < CompactWidth);
            m_root.EnableInClassList(NarrowClass, width < NarrowWidth);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent change)
        {
            Apply(change.newRect.width);
        }
    }
}
