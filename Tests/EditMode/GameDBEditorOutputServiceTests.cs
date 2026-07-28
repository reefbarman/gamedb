using GameDBEditorLibrary;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorOutputServiceTests
    {
        private string m_rootAssetPath;
        private string m_rootAbsolutePath;
        private string m_databasePath;
        private GameDBEditorWorkspace m_workspace;

        [SetUp]
        public void SetUp()
        {
            m_rootAssetPath = $"Assets/GameDBEditorOutputServiceTests_{Guid.NewGuid():N}";
            m_rootAbsolutePath = Path.Combine(Application.dataPath,
                m_rootAssetPath.Substring("Assets/".Length));
            m_databasePath = m_rootAssetPath + "/database.json";
            m_workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                new GameDBWorkspaceRecoveryService(new MemoryRecoveryStore()),
                new GameDBActiveWorkspaceHub());
            var opened = m_workspace.TryCreateDatabase(m_databasePath, "OutputService", false);
            Assert.That(opened.Success, Is.True, opened.Status.ToString());
            Assert.That(opened.Tab.Session.ApplyTransaction(new GameDBCommand[]
            {
                new AddTableCommand("Items", KeyType.@string, null),
                new AddFieldCommand("Items", "Power",
                    new GameDBFieldTypeSpec(FieldType.@int, false, null)),
                new AddRowCommand("Items", "Sword", new Dictionary<string, object>
                {
                    { "Power", 12L }
                })
            }).Success, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            m_workspace?.Dispose();
            GameDBEditor.OnGameDBSaved = null;
            AssetDatabase.DeleteAsset(m_rootAssetPath);
            if (Directory.Exists(m_rootAbsolutePath))
            {
                Directory.Delete(m_rootAbsolutePath, true);
            }
        }

        [Test]
        public void Build_SavesAndWritesOnlyCurrentDataJson()
        {
            var service = new GameDBEditorOutputService();

            var result = service.Build(m_workspace.ActiveTab,
                m_rootAssetPath.Substring("Assets/".Length) + "/Build");

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.OutputPath, Is.EqualTo(m_rootAssetPath + "/Build/database.json"));
            var builtPath = Path.Combine(m_rootAbsolutePath, "Build", "database.json");
            Assert.That(File.Exists(builtPath), Is.True);
            Assert.That(File.ReadAllText(builtPath),
                Is.EqualTo(m_workspace.ActiveTab.Session.SerializeCurrent().DataJson));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(builtPath), "*.schema.json"),
                Is.Empty);
            Assert.That(m_workspace.ActiveTab.Session.GetState().IsDirty, Is.False);
        }

        [Test]
        public void Generate_SavesAndProducesScopeDirectory()
        {
            var service = new GameDBEditorOutputService();
            GameDBEditorOutputResult result;
            var ignoredBefore = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                result = service.Generate(m_workspace.ActiveTab,
                    m_rootAssetPath + "/Generated");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoredBefore;
            }

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.OutputPath, Is.EqualTo(m_rootAssetPath + "/Generated"));
            var generatedScope = Path.Combine(m_rootAbsolutePath, "Generated", "OutputService");
            Assert.That(Directory.Exists(generatedScope), Is.True);
            Assert.That(Directory.GetFiles(generatedScope, "*.cs", SearchOption.AllDirectories),
                Is.Not.Empty);
            Assert.That(m_workspace.ActiveTab.Session.GetState().IsDirty, Is.False);
        }

        [Test]
        public void Generate_RequiresExplicitAuthorizationBeforeReplacingScopeDirectory()
        {
            var service = new GameDBEditorOutputService();
            var scopePath = Path.Combine(m_rootAbsolutePath, "Generated", "OutputService");
            Directory.CreateDirectory(scopePath);
            File.WriteAllText(Path.Combine(scopePath, "handwritten.cs"), "keep");

            var blocked = service.Generate(m_workspace.ActiveTab,
                m_rootAssetPath + "/Generated");

            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.RequiresConfirmation, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(scopePath, "handwritten.cs")),
                Is.EqualTo("keep"));
            Assert.That(m_workspace.ActiveTab.Session.GetState().IsDirty, Is.True,
                "Confirmation must happen before saving the active document.");

            GameDBEditorOutputResult replaced;
            var ignoredBefore = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                replaced = service.Generate(m_workspace.ActiveTab,
                    m_rootAssetPath + "/Generated", true);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoredBefore;
            }
            Assert.That(replaced.Success, Is.True, replaced.Message);
            Assert.That(File.Exists(Path.Combine(scopePath, "handwritten.cs")), Is.False);
        }

        [Test]
        public void Build_RejectsExistingOutputWithoutOverwriting()
        {
            var service = new GameDBEditorOutputService();
            var buildDirectory = Path.Combine(m_rootAbsolutePath, "Build");
            Directory.CreateDirectory(buildDirectory);
            var output = Path.Combine(buildDirectory, "database.json");
            File.WriteAllText(output, "keep");

            var result = service.Build(m_workspace.ActiveTab,
                m_rootAssetPath.Substring("Assets/".Length) + "/Build");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("already exists"));
            Assert.That(File.ReadAllText(output), Is.EqualTo("keep"));
        }

        [TestCase("")]
        [TestCase("/tmp/output")]
        [TestCase("../outside")]
        [TestCase("Generated/../outside")]
        [TestCase("Assets")]
        public void Output_RejectsMissingRootedAndTraversalPaths(string path)
        {
            var service = new GameDBEditorOutputService();

            var generated = service.Generate(m_workspace.ActiveTab, path);
            var built = service.Build(m_workspace.ActiveTab, path);

            Assert.That(generated.Success, Is.False);
            Assert.That(built.Success, Is.False);
            Assert.That(m_workspace.ActiveTab.Session.GetState().IsDirty, Is.True,
                "Validation must happen before saving the active document.");
        }

        [Test]
        public void Output_RejectsMissingAndPlayModeTabsWithoutSaving()
        {
            var service = new GameDBEditorOutputService();
            Assert.That(service.Generate(null, "Generated").Success, Is.False);
            Assert.That(service.Build(null, "Build").Success, Is.False);
            var tab = m_workspace.ActiveTab;
            tab.BeginPlayMode(tab.Session.CaptureState(), false);

            var generated = service.Generate(tab, "Generated");
            var built = service.Build(tab, "Build");

            Assert.That(generated.Success, Is.False);
            Assert.That(generated.Message, Does.Contain("runtime GameDB"));
            Assert.That(built.Success, Is.False);
            Assert.That(built.Message, Does.Contain("runtime GameDB"));
            Assert.That(tab.Session.GetState().IsDirty, Is.True);
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
    }
}
