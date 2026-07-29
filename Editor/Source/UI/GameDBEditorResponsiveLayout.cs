using System;
using System.Linq;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBEditorResponsiveLayout : IDisposable
    {
        internal const float CompactWidth = 900f;
        internal const float NarrowWidth = 560f;
        internal const string CompactClass = "gamedb-editor--compact";
        internal const string NarrowClass = "gamedb-editor--narrow";
        internal const string InspectorOpenClass = "gamedb-editor--inspector-open";

        private readonly VisualElement m_root;
        private readonly Button m_inspectorToggle;
        private readonly VisualElement m_inspector;
        private readonly VisualElement m_inspectorScrim;
        private readonly Button m_inspectorClose;
        private readonly VisualElement m_tableNavigation;
        private readonly VisualElement m_tableSurface;
        private bool m_inspectorOpen;
        private bool m_wideInspectorOpen = true;
        private bool? m_compact;
        private Action m_closeRequested;
        private int m_focusGeneration;
        private bool m_disposed;

        internal bool IsInspectorOpen => m_inspectorOpen;

        internal void SetCloseRequested(Action closeRequested)
        {
            m_closeRequested = closeRequested;
        }

        internal GameDBEditorResponsiveLayout(VisualElement root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            m_inspectorToggle = root.Q<Button>("table-inspector-toggle-button");
            m_inspector = root.Q<VisualElement>("inspector-host");
            m_inspectorScrim = root.Q<VisualElement>("inspector-scrim");
            m_inspectorClose = root.Q<Button>("inspector-close-button");
            m_tableNavigation = root.Q<VisualElement>("table-navigation-host");
            m_tableSurface = root.Q<VisualElement>("table-surface-host");
            m_root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            if (m_inspectorToggle != null)
            {
                m_inspectorToggle.clicked += ToggleInspector;
            }
            if (m_inspectorScrim != null)
            {
                m_inspectorScrim.RegisterCallback<PointerDownEvent>(OnInspectorScrimPointerDown);
            }
            if (m_inspectorClose != null)
            {
                m_inspectorClose.clicked += CloseInspector;
            }
            m_compact = false;
            SetInspectorOpen(true, false);
            var initialWidth = m_root.resolvedStyle.width;
            if (!float.IsNaN(initialWidth) && initialWidth > 0f)
            {
                Apply(initialWidth);
            }
        }

        internal void Apply(float width)
        {
            if (m_disposed || float.IsNaN(width))
            {
                return;
            }

            var compact = width < CompactWidth;
            m_root.EnableInClassList(CompactClass, compact);
            m_root.EnableInClassList(NarrowClass, width < NarrowWidth);
            if (m_compact != compact)
            {
                m_compact = compact;
                SetInspectorOpen(compact ? false : m_wideInspectorOpen, false);
            }
        }

        internal void ToggleInspector()
        {
            if (m_inspectorOpen)
            {
                RequestCloseInspector();
                return;
            }
            if (m_compact == false)
            {
                m_wideInspectorOpen = true;
            }
            SetInspectorOpen(true, true);
        }

        internal void CloseInspector()
        {
            RequestCloseInspector();
        }

        internal void CloseInspectorImmediately()
        {
            if (m_compact == false)
            {
                m_wideInspectorOpen = false;
            }
            SetInspectorOpen(false, true);
        }

        private void RequestCloseInspector()
        {
            if (m_closeRequested != null)
            {
                m_closeRequested();
                return;
            }
            CloseInspectorImmediately();
        }

        internal void EnsureInspectorOpen(VisualElement focusTarget = null)
        {
            if (m_compact == false)
            {
                m_wideInspectorOpen = true;
            }
            SetInspectorOpen(true, false);
            ScheduleInspectorFocus(focusTarget);
        }

        private void SetInspectorOpen(bool open, bool updateFocus)
        {
            if (m_disposed)
            {
                return;
            }
            var changed = m_inspectorOpen != open;
            m_inspectorOpen = open;
            m_focusGeneration++;
            m_root.EnableInClassList(InspectorOpenClass, open);
            var drawerOpen = open && m_compact == true;
            m_tableNavigation?.SetEnabled(!drawerOpen);
            m_tableSurface?.SetEnabled(!drawerOpen);
            if (!changed || !updateFocus)
            {
                return;
            }
            if (open)
            {
                ScheduleInspectorFocus(null);
            }
            else
            {
                m_inspectorToggle?.Focus();
            }
        }

        private void ScheduleInspectorFocus(VisualElement focusTarget)
        {
            if (m_inspector == null)
            {
                return;
            }
            var generation = m_focusGeneration;
            m_inspector.schedule.Execute(() =>
            {
                if (m_disposed || !m_inspectorOpen || generation != m_focusGeneration)
                {
                    return;
                }
                var controls = m_inspector.Q<VisualElement>("inspector-content-host")
                    ?? m_inspector;
                var control = IsVisibleFocusable(focusTarget)
                    ? focusTarget
                    : m_compact == true && IsVisibleFocusable(m_inspectorClose)
                        ? m_inspectorClose
                        : controls.Query<VisualElement>().ToList().FirstOrDefault(
                            IsVisibleFocusable);
                control?.Focus();
            }).ExecuteLater(1);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_closeRequested = null;
            m_focusGeneration++;
            m_inspectorOpen = false;
            m_root.EnableInClassList(InspectorOpenClass, false);
            m_tableNavigation?.SetEnabled(true);
            m_tableSurface?.SetEnabled(true);
            m_root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            if (m_inspectorToggle != null)
            {
                m_inspectorToggle.clicked -= ToggleInspector;
            }
            if (m_inspectorScrim != null)
            {
                m_inspectorScrim.UnregisterCallback<PointerDownEvent>(
                    OnInspectorScrimPointerDown);
            }
            if (m_inspectorClose != null)
            {
                m_inspectorClose.clicked -= CloseInspector;
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent change)
        {
            Apply(change.newRect.width);
        }

        private void OnInspectorScrimPointerDown(PointerDownEvent evt)
        {
            if (m_inspectorOpen && evt.button == 0)
            {
                CloseInspector();
                evt.StopImmediatePropagation();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (m_inspectorOpen && m_compact == true
                && evt.keyCode == UnityEngine.KeyCode.Escape
                && !IsTextInputEvent(evt.target as VisualElement))
            {
                CloseInspector();
                evt.StopImmediatePropagation();
            }
        }

        private bool IsVisibleFocusable(VisualElement element)
        {
            if (element == null || element.panel == null || !element.focusable
                || !element.enabledInHierarchy)
            {
                return false;
            }
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
                if (current == m_inspector)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsTextInputEvent(VisualElement element)
        {
            while (element != null && element != m_root)
            {
                if (element is TextField
                    || element.ClassListContains("unity-base-text-field"))
                {
                    return true;
                }
                element = element.parent;
            }
            return false;
        }
    }
}
