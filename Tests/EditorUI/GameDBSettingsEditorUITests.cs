using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary.Tests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDBLibrary.EditorUITests
{
    [Category("EditorUI")]
    public class GameDBSettingsEditorUITests
        : EditorWindowUITestFixture<GameDBAttachedSettingsTestWindow>
    {
        [Test]
        public void SettingsModal_RemainsScrollableReachableAndBlockingAtCompactHeight()
        {
            panelSize = new UnityEngine.Vector2(560f, 300f);
            window.OpenSettings();
            for (var frame = 0; frame < 6; frame++)
            {
                simulate.FrameUpdate();
            }

            Assert.That(window.ModalHost.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.ModalHost.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(window.SettingsPanel.worldBound.height,
                Is.LessThanOrEqualTo(window.Root.worldBound.height + 1f));
            Assert.That(window.SettingsScroll.worldBound.height, Is.GreaterThan(0f));
            Assert.That(window.SettingsPanel.resolvedStyle.backgroundColor.a,
                Is.EqualTo(1f).Within(0.01f));
            Assert.That(window.RegisteredDatabasePaths.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(window.RegisteredDatabaseEmpty.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            window.PopulateRegisteredDatabases();
            simulate.FrameUpdate();
            Assert.That(window.RegisteredDatabasePaths.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.RegisteredDatabasePaths.worldBound.height,
                Is.GreaterThanOrEqualTo(80f));
            Assert.That(window.RegisteredDatabaseEmpty.resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(window.SettingsScroll.contentContainer.layout.height,
                Is.GreaterThan(window.SettingsScroll.contentViewport.layout.height));
            Assert.That(window.SaveSettings.worldBound.yMax,
                Is.LessThanOrEqualTo(window.SettingsPanel.worldBound.yMax + 1f));
            Assert.That(window.CloseSettings.worldBound.yMax,
                Is.LessThanOrEqualTo(window.SettingsPanel.worldBound.yMax + 1f));
            Assert.That(window.EnumList.selectionType, Is.EqualTo(SelectionType.None));
            var enumNames = window.EnumList.itemsSource.Cast<string>().ToList();
            Assert.That(enumNames,
                Does.Contain(GameDBAttachedSettingsTestWindow.UnresolvedEnum));
            window.EnumList.ScrollToItem(enumNames.IndexOf(
                GameDBAttachedSettingsTestWindow.UnresolvedEnum));
            simulate.FrameUpdate();
            Assert.That(window.EnumList.Query<Toggle>().ToList()
                .Any(toggle => Equals(toggle.userData,
                    GameDBAttachedSettingsTestWindow.UnresolvedEnum)), Is.True);
            Assert.That(window.ModalHost.Contains(
                window.Root.panel.Pick(window.Grid.worldBound.center)), Is.True);
        }
    }

    public sealed class GameDBAttachedSettingsTestWindow : EditorWindow
    {
        internal const string UnresolvedEnum = "Missing.LegacyEnum";
        private GameDBEditorWorkspace m_workspace;
        private GameDBEditorWindowController m_controller;
        private GameDBProjectSettingsService m_settings;

        internal VisualElement Root => rootVisualElement.Q<VisualElement>(
            "gamedb-editor-root");
        internal MultiColumnListView Grid => rootVisualElement.Q<MultiColumnListView>(
            "table-row-grid");
        internal VisualElement ModalHost => rootVisualElement.Q<VisualElement>("modal-host");
        internal VisualElement SettingsPanel => rootVisualElement.Q<VisualElement>(
            "settings-panel");
        internal ScrollView SettingsScroll => SettingsPanel.Q<ScrollView>(
            className: "gamedb-editor__settings-scroll");
        internal ListView EnumList => rootVisualElement.Q<ListView>("imported-enum-types");
        internal ScrollView RegisteredDatabasePaths => rootVisualElement.Q<ScrollView>(
            "registered-database-paths");
        internal Label RegisteredDatabaseEmpty => rootVisualElement.Q<Label>(
            "registered-database-empty-label");
        internal Button SaveSettings => rootVisualElement.Q<Button>("save-settings-button");
        internal Button CloseSettings => rootVisualElement.Q<Button>("close-settings-button");

        internal void OpenSettings()
        {
            m_controller.OpenSettings();
        }

        internal void PopulateRegisteredDatabases()
        {
            m_settings.Update(new[] { "registered.json" }, new[] { UnresolvedEnum },
                "Generated", "Build");
        }

        public void CreateGUI()
        {
            GameDBEditorUiAssets.Build(rootVisualElement);
            var document = (GameDBDocument)GameDBRepresentativeFixture
                .CreateDocumentForEditorUiTests(documentId: "settings-editor-ui");
            var recovery = new GameDBWorkspaceRecoveryService(new MemoryRecoveryStore());
            recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("settings", document.CaptureState())
            }, "settings"));
            m_workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
            m_settings = new GameDBProjectSettingsService(new MemorySettingsStore(),
                _ => true, name => name != UnresolvedEnum);
            m_settings.Update(Array.Empty<string>(), new[] { UnresolvedEnum },
                "Generated", "Build");
            var enums = Enumerable.Range(0, 40)
                .Select(index => $"Game.Enums.Enum{index:00}").ToArray();
            m_controller = new GameDBEditorWindowController(rootVisualElement, m_workspace,
                projectSettings: m_settings, availableEnumTypes: () => enums);
        }

        private void OnDisable()
        {
            m_controller?.Dispose();
            m_controller = null;
            m_workspace?.Dispose();
            m_workspace = null;
            m_settings = null;
        }

        private sealed class MemoryRecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
            public string QuarantinePrimary()
            {
                m_contents = null;
                return "quarantine.json";
            }
            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }
        }

        private sealed class MemorySettingsStore : IGameDBProjectSettingsStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
        }
    }
}
