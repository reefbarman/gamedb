using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorDatabaseControlsTests
    {
        private const string FirstPath = "Assets/DatabaseControls/first.json";
        private const string SecondPath = "Assets/DatabaseControls/second.json";

        [Test]
        public void Workspace_CreatePublishesAfterRecoveryAndFailureReleasesStagedLease()
        {
            var pairStore = new MemoryPairStore();
            var recoveryStore = new RecoveryStore();
            var registry = new GameDBDocumentLeaseRegistry(pairStore);
            var workspace = CreateWorkspace(registry, recoveryStore);

            var created = workspace.TryCreateDatabase(FirstPath, "FirstScope", true);

            Assert.That(created.Status, Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Opened));
            Assert.That(created.Tab, Is.SameAs(workspace.ActiveTab));
            Assert.That(created.Tab.Session.CreateSnapshot().ScopeName, Is.EqualTo("FirstScope"));
            Assert.That(created.Tab.Session.CreateSnapshot().LocalizationDatabase, Is.True);
            Assert.That(recoveryStore.WriteCount, Is.EqualTo(1));
            recoveryStore.FailWrites = true;

            var failed = workspace.TryCreateDatabase(SecondPath, "SecondScope", false);

            Assert.That(failed.Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.RecoveryFailed));
            Assert.That(workspace.Tabs.Select(tab => tab.Session.AssetPath),
                Is.EqualTo(new[] { FirstPath }));
            var replacement = registry.TryAcquire(SecondPath, "replacement");
            Assert.That(replacement.Status,
                Is.EqualTo(GameDBDocumentLeaseAcquireStatus.Acquired));
            replacement.Lease.Dispose();
            recoveryStore.FailWrites = false;
            workspace.Dispose();
        }

        [Test]
        public void Workspace_CreateRejectsInvalidExistingAndBusyPathsWithoutTopologyMutation()
        {
            var pairStore = new MemoryPairStore();
            Seed(pairStore, FirstPath, "Existing");
            var registry = new GameDBDocumentLeaseRegistry(pairStore);
            var workspace = CreateWorkspace(registry, new RecoveryStore());

            Assert.That(workspace.TryCreateDatabase("outside.json", "Scope", false).Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Invalid));
            Assert.That(workspace.TryCreateDatabase(FirstPath, "Scope", false).Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Invalid));
            var lease = registry.TryAcquire(SecondPath, "other");
            Assert.That(workspace.TryCreateDatabase(SecondPath, "Scope", false).Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Busy));
            Assert.That(workspace.Tabs, Is.Empty);

            lease.Lease.Dispose();
            workspace.Dispose();
        }

        [Test]
        public void Workspace_OpenActivatesExistingWithoutDuplicateAndPersistsSelection()
        {
            var pairStore = new MemoryPairStore();
            Seed(pairStore, FirstPath, "First");
            Seed(pairStore, SecondPath, "Second");
            var recoveryStore = new RecoveryStore();
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(pairStore),
                recoveryStore);
            var first = workspace.TryOpenDatabase(FirstPath);
            var second = workspace.TryOpenDatabase(SecondPath);

            var activated = workspace.TryOpenDatabase(FirstPath.Replace('/', '\\'));

            Assert.That(first.Status, Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Opened));
            Assert.That(second.Status, Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Opened));
            Assert.That(activated.Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.ActivatedExisting));
            Assert.That(workspace.Tabs, Has.Count.EqualTo(2));
            Assert.That(workspace.ActiveTab.Session.AssetPath, Is.EqualTo(FirstPath));
            Assert.That(recoveryStore.WriteCount, Is.EqualTo(3));
            workspace.Dispose();
        }

        [Test]
        public void Workspace_ActivationRecoveryFailureKeepsPreviousSelection()
        {
            var pairStore = new MemoryPairStore();
            Seed(pairStore, FirstPath, "First");
            Seed(pairStore, SecondPath, "Second");
            var recoveryStore = new RecoveryStore();
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(pairStore),
                recoveryStore);
            Assert.That(workspace.TryOpenDatabase(FirstPath).Success, Is.True);
            Assert.That(workspace.TryOpenDatabase(SecondPath).Success, Is.True);
            recoveryStore.FailWrites = true;
            var firstTabId = workspace.Tabs.Single(tab =>
                tab.Session.AssetPath == FirstPath).TabId;

            Assert.That(workspace.TryActivateTab(firstTabId), Is.False);
            Assert.That(workspace.ActiveTab.Session.AssetPath, Is.EqualTo(SecondPath));
            Assert.That(workspace.LastTabOperationError,
                Does.Contain("recovery write failed"));

            recoveryStore.FailWrites = false;
            workspace.Dispose();
        }

        [Test]
        public void Workspace_OpenMissingBusyAndRecoveryFailureKeepExistingTopology()
        {
            var pairStore = new MemoryPairStore();
            Seed(pairStore, FirstPath, "First");
            Seed(pairStore, SecondPath, "Second");
            var recoveryStore = new RecoveryStore();
            var registry = new GameDBDocumentLeaseRegistry(pairStore);
            var workspace = CreateWorkspace(registry, recoveryStore);

            Assert.That(workspace.TryOpenDatabase("Assets/DatabaseControls/missing.json").Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Invalid));
            var busy = registry.TryAcquire(FirstPath, "other");
            Assert.That(workspace.TryOpenDatabase(FirstPath).Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.Busy));
            busy.Lease.Dispose();
            recoveryStore.FailWrites = true;
            Assert.That(workspace.TryOpenDatabase(SecondPath).Status,
                Is.EqualTo(GameDBWorkspaceDatabaseOpenStatus.RecoveryFailed));
            Assert.That(workspace.Tabs, Is.Empty);

            recoveryStore.FailWrites = false;
            workspace.Dispose();
        }

        [Test]
        public void Controller_PickerCancellationDoesNothingAndDisposeStopsFurtherPickerCalls()
        {
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var settings = CreateSettings(new SettingsStore());
            var dialogs = new RecordingDialogs();
            var root = BuildRoot();
            var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings, databaseDialogs: dialogs);

            Assert.That(controller.ChooseAndCreateDatabase(), Is.Null);
            Assert.That(controller.ChooseAndOpenDatabase(), Is.Null);
            Assert.That(controller.ChooseAndRegisterDatabase(), Is.Null);
            Assert.That(workspace.Tabs, Is.Empty);
            Assert.That(settings.GetSnapshot().RegisteredDatabasePaths, Is.Empty);
            Assert.That(dialogs.TotalCalls, Is.EqualTo(3));
            controller.OpenSettings();
            Assert.That(root.Q<VisualElement>("modal-host").pickingMode,
                Is.EqualTo(PickingMode.Position));

            controller.Dispose();
            Assert.That(root.Q<VisualElement>("modal-host").pickingMode,
                Is.EqualTo(PickingMode.Ignore));
            Assert.That(controller.ChooseAndCreateDatabase(), Is.Null);
            Assert.That(controller.ChooseAndOpenDatabase(), Is.Null);
            Assert.That(controller.ChooseAndRegisterDatabase(), Is.Null);
            Assert.That(dialogs.TotalCalls, Is.EqualTo(3));
            workspace.Dispose();
        }

        [Test]
        public void Controller_BindsRegisteredPathsSeparatelyFromOpenTabsAndUpdatesOutputs()
        {
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var settingsStore = new SettingsStore();
            var settings = CreateSettings(settingsStore, path => path == "first.json");
            var root = BuildRoot();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings, databaseDialogs: new RecordingDialogs()))
            {
                controller.OpenSettings();
                var registered = controller.RegisterDatabase("Assets/first.json");
                var missing = controller.RegisterDatabase("Assets/missing.json");
                var outputs = controller.UpdateProjectSettings(" Generated\\Code ",
                    " Build\\Data ");

                Assert.That(registered.Success, Is.True);
                Assert.That(missing.Success, Is.True);
                Assert.That(outputs.Snapshot.RegisteredDatabasePaths,
                    Is.EqualTo(new[] { "first.json", "missing.json" }));
                Assert.That(outputs.Snapshot.ExportPath, Is.EqualTo("Generated/Code"));
                Assert.That(outputs.Snapshot.BuildPath, Is.EqualTo("Build/Data"));
                Assert.That(workspace.Tabs, Is.Empty);
                Assert.That(root.Q<ScrollView>("registered-database-paths")
                    .Query<Label>(className: "gamedb-editor__registered-path-label")
                    .ToList().Select(label => label.text),
                    Is.EqualTo(new[] { "first.json", "missing.json" }));
                Assert.That(root.Q<VisualElement>("settings-validation-host")
                    .Query<HelpBox>().ToList().Single().text,
                    Does.Contain("missing.json"));
                Assert.That(root.Q<TextField>("export-path-field").value,
                    Is.EqualTo("Generated/Code"));
                Assert.That(root.Q<VisualElement>("modal-host").pickingMode,
                    Is.EqualTo(PickingMode.Position));
                AssertReadOnlyTableView(root);

                controller.CloseSettings();
                Assert.That(root.Q<VisualElement>("modal-host").pickingMode,
                    Is.EqualTo(PickingMode.Ignore));
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_OutputButtonsInvokeConfiguredServiceAndRespectAvailability()
        {
            var pairStore = new MemoryPairStore();
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(pairStore),
                new RecoveryStore());
            Assert.That(workspace.TryCreateDatabase(FirstPath, "OutputScope", false).Success,
                Is.True);
            var settings = CreateSettings(new SettingsStore());
            var output = new RecordingOutputService();
            var root = BuildRoot();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings, outputService: output, isPlaying: () => false))
            {
                var generate = root.Q<ToolbarButton>("generate-button");
                var build = root.Q<ToolbarButton>("build-button");
                Assert.That(generate.enabledSelf, Is.False);
                Assert.That(build.enabledSelf, Is.False);

                controller.UpdateProjectSettings("Generated/Code", "Build/Data");

                Assert.That(generate.enabledSelf, Is.True);
                Assert.That(build.enabledSelf, Is.True);
                controller.GenerateActiveDocument();
                controller.BuildActiveDocument();
                Assert.That(output.GenerateTab, Is.SameAs(workspace.ActiveTab));
                Assert.That(output.GeneratePath, Is.EqualTo("Generated/Code"));
                Assert.That(output.BuildTab, Is.SameAs(workspace.ActiveTab));
                Assert.That(output.BuildPath, Is.EqualTo("Build/Data"));
                var message = root.Q<VisualElement>("document-warning-host")
                    .Query<HelpBox>().ToList().Last();
                Assert.That(message.text, Does.Contain("build complete"));
                Assert.That(message.messageType, Is.EqualTo(HelpBoxMessageType.Info));
            }
            workspace.Dispose();

            var emptyWorkspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var configuredSettings = CreateSettings(new SettingsStore());
            configuredSettings.Update(Array.Empty<string>(), Array.Empty<string>(),
                "Generated", "Build");
            var emptyRoot = BuildRoot();
            using (var controller = new GameDBEditorWindowController(emptyRoot, emptyWorkspace,
                projectSettings: configuredSettings, outputService: new RecordingOutputService(),
                isPlaying: () => false))
            {
                Assert.That(emptyRoot.Q<ToolbarButton>("generate-button").enabledSelf, Is.False);
                Assert.That(emptyRoot.Q<ToolbarButton>("build-button").enabledSelf, Is.False);
            }
            emptyWorkspace.Dispose();
        }

        [Test]
        public void Controller_GenerateRequiresPolicyConfirmationBeforeAuthorizedRetry()
        {
            var pairStore = new MemoryPairStore();
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(pairStore),
                new RecoveryStore());
            Assert.That(workspace.TryCreateDatabase(FirstPath, "OutputScope", false).Success,
                Is.True);
            var settings = CreateSettings(new SettingsStore());
            settings.Update(Array.Empty<string>(), Array.Empty<string>(), "Generated", "Build");
            var output = new RecordingOutputService { RequireGenerateConfirmation = true };
            var policy = new RecordingDestructivePolicy();
            var root = BuildRoot();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings, outputService: output,
                destructiveActionPolicy: policy, isPlaying: () => false))
            {
                controller.GenerateActiveDocument();

                Assert.That(policy.Requests, Has.Count.EqualTo(1));
                Assert.That(policy.Requests[0].Kind, Is.Null);
                Assert.That(policy.Requests[0].Title, Is.EqualTo("Replace Generated Code"));
                Assert.That(output.GenerateAllowDestructiveCalls,
                    Is.EqualTo(new[] { false, true }));
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_ImportedEnumsMergePersistedAndAvailableAndSaveToggles()
        {
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var settings = CreateSettings(new SettingsStore());
            settings.Update(Array.Empty<string>(), new[] { "Missing.Enum", "Z.Available" },
                string.Empty, string.Empty);
            var root = BuildRoot();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings,
                availableEnumTypes: () => new[] { "Z.Available", "A.Available" }))
            {
                var enums = root.Q<ListView>("imported-enum-types");
                Assert.That(enums.itemsSource.Cast<string>(), Is.EqualTo(new[]
                {
                    "A.Available", "Missing.Enum", "Z.Available"
                }));
                Assert.That(enums.selectionType, Is.EqualTo(SelectionType.None));
                controller.SetImportedEnumEnabled("A.Available", true);
                controller.SetImportedEnumEnabled("Z.Available", false);
                controller.SaveSettings();

                Assert.That(settings.GetSnapshot().ImportedEnumTypeNames,
                    Is.EqualTo(new[] { "A.Available", "Missing.Enum" }));
            }
            workspace.Dispose();
        }

        [Test]
        public void Controller_EnumDiscoveryFailureFallsBackToPersistedNames()
        {
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var settings = CreateSettings(new SettingsStore());
            settings.Update(Array.Empty<string>(), new[] { "Persisted.Enum" },
                string.Empty, string.Empty);
            var root = BuildRoot();

            Assert.DoesNotThrow(() =>
            {
                using (var controller = new GameDBEditorWindowController(root, workspace,
                    projectSettings: settings,
                    availableEnumTypes: () => throw new InvalidOperationException("catalog failed")))
                {
                    Assert.That(root.Q<ListView>("imported-enum-types").itemsSource
                        .Cast<string>(), Is.EqualTo(new[] { "Persisted.Enum" }));
                    Assert.That(root.Q<Label>("settings-error-label").text,
                        Does.Contain("catalog failed"));
                }
            });
            workspace.Dispose();
        }

        [Test]
        public void Controller_SettingsNoOpFailureExternalChangeAndUnregisterRefreshBinding()
        {
            var workspace = CreateWorkspace(new GameDBDocumentLeaseRegistry(
                new MemoryPairStore()), new RecoveryStore());
            var store = new SettingsStore();
            var settings = CreateSettings(store);
            var root = BuildRoot();
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: settings, databaseDialogs: new RecordingDialogs()))
            {
                controller.OpenSettings();
                root.Q<TextField>("export-path-field").value = "Draft/Generated";
                Assert.That(controller.RegisterDatabase(FirstPath).Changed, Is.True);
                Assert.That(controller.RegisterDatabase(" Assets/DatabaseControls/first.json ")
                    .Changed, Is.False);
                var invalid = controller.RegisterDatabase(
                    "Assets/DatabaseControls/first.schema.json");
                Assert.That(invalid.Success, Is.False);
                Assert.That(settings.GetSnapshot().RegisteredDatabasePaths,
                    Is.EqualTo(new[] { "DatabaseControls/first.json" }));
                Assert.That(store.WriteCount, Is.EqualTo(1));
                Assert.That(root.Q<TextField>("export-path-field").value,
                    Is.EqualTo("Draft/Generated"));

                root.Q<TextField>("export-path-field").value = "Generated";
                store.WriteException = new IOException("settings disk full");
                var failed = controller.UpdateProjectSettings("Generated", "Build");
                Assert.That(failed.Success, Is.False);
                Assert.That(root.Q<Label>("settings-error-label").text,
                    Does.Contain("settings disk full"));
                Assert.That(root.Q<TextField>("export-path-field").value,
                    Is.EqualTo("Generated"));
                store.WriteException = null;

                settings.Update(new[] { "external.json" }, Array.Empty<string>(),
                    "External", "ExternalBuild");
                Assert.That(root.Q<TextField>("export-path-field").value,
                    Is.EqualTo("Generated"), "External refresh must preserve the open modal draft.");
                Assert.That(root.Q<ScrollView>("registered-database-paths")
                    .Query<Label>(className: "gamedb-editor__registered-path-label")
                    .ToList().Single().text, Is.EqualTo("external.json"));
                Assert.That(controller.UnregisterDatabase("external.json").Success, Is.True);
                Assert.That(root.Q<ScrollView>("registered-database-paths").childCount,
                    Is.Zero);
            }
            workspace.Dispose();
        }

        private static VisualElement BuildRoot()
        {
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            return root;
        }

        private static void AssertReadOnlyTableView(VisualElement root)
        {
            Assert.That(root.Q<MultiColumnListView>("table-row-grid"), Is.Not.Null);
            Assert.That(root.Q<ListView>("table-navigation-list"), Is.Not.Null);
            Assert.That(root.Q<TreeView>(), Is.Null);
            Assert.That(root.Q<IMGUIContainer>(), Is.Null);
            Assert.That(root.Q<VisualElement>("inspector-host").childCount,
                Is.GreaterThan(0));
            Assert.That(root.Q<ListView>("field-navigation-list"), Is.Not.Null);
            Assert.That(root.Q<MultiColumnListView>("table-row-grid")
                .Q<TextField>("database-scope-field"), Is.Null);
        }

        private static GameDBEditorWorkspace CreateWorkspace(
            GameDBDocumentLeaseRegistry registry, RecoveryStore recoveryStore)
        {
            return new GameDBEditorWorkspace(registry,
                new GameDBWorkspaceRecoveryService(recoveryStore),
                new GameDBActiveWorkspaceHub());
        }

        private static GameDBProjectSettingsService CreateSettings(SettingsStore store,
            Func<string, bool> exists = null)
        {
            return new GameDBProjectSettingsService(store, exists ?? (_ => true), _ => true);
        }

        private static void Seed(MemoryPairStore store, string path, string scope)
        {
            var document = GameDBDocument.CreateNew(path, scope, false, store,
                new NoOpPostSaveActions());
            Assert.That(document.Save().Success, Is.True);
        }

        private sealed class RecordingDialogs : IGameDBEditorDatabaseDialogs
        {
            internal int TotalCalls { get; private set; }

            public GameDBCreateDatabaseSelection SelectCreateDatabase()
            {
                TotalCalls++;
                return null;
            }

            public string SelectOpenDatabase()
            {
                TotalCalls++;
                return null;
            }

            public string SelectRegisterDatabase()
            {
                TotalCalls++;
                return null;
            }
        }

        private sealed class RecordingDestructivePolicy : IGameDBEditorDestructiveActionPolicy
        {
            internal List<GameDBDestructiveActionRequest> Requests { get; }
                = new List<GameDBDestructiveActionRequest>();

            public bool Confirm(GameDBDestructiveActionRequest request)
            {
                Requests.Add(request);
                return true;
            }
        }

        private sealed class RecordingOutputService : IGameDBEditorOutputService
        {
            internal GameDBEditorWorkspaceTab GenerateTab { get; private set; }
            internal string GeneratePath { get; private set; }
            internal GameDBEditorWorkspaceTab BuildTab { get; private set; }
            internal string BuildPath { get; private set; }
            internal bool RequireGenerateConfirmation { get; set; }
            internal List<bool> GenerateAllowDestructiveCalls { get; }
                = new List<bool>();

            public GameDBEditorOutputResult Generate(GameDBEditorWorkspaceTab tab,
                string exportPath, bool allowDestructive = false)
            {
                GenerateTab = tab;
                GeneratePath = exportPath;
                GenerateAllowDestructiveCalls.Add(allowDestructive);
                return RequireGenerateConfirmation && !allowDestructive
                    ? new GameDBEditorOutputResult(false, "confirmation required",
                        "Assets/Generated", true)
                    : new GameDBEditorOutputResult(true, "generation complete");
            }

            public GameDBEditorOutputResult Build(GameDBEditorWorkspaceTab tab,
                string buildPath)
            {
                BuildTab = tab;
                BuildPath = buildPath;
                return new GameDBEditorOutputResult(true, "build complete");
            }
        }

        private sealed class RecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            internal string Contents { get; private set; }
            internal int WriteCount { get; private set; }
            internal bool FailWrites { get; set; }
            public bool Exists => Contents != null;

            public string ReadAllText() => Contents;

            public void WriteAtomically(string contents)
            {
                if (FailWrites)
                {
                    throw new IOException("recovery write failed");
                }
                Contents = contents;
                WriteCount++;
            }

            public string QuarantinePrimary()
            {
                Contents = null;
                return "quarantine.json";
            }

            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }
        }

        private sealed class SettingsStore : IGameDBProjectSettingsStore
        {
            internal string Contents { get; private set; }
            internal int WriteCount { get; private set; }
            internal Exception WriteException { get; set; }
            public bool Exists => Contents != null;

            public string ReadAllText() => Contents;

            public void WriteAtomically(string contents)
            {
                WriteCount++;
                if (WriteException != null)
                {
                    throw WriteException;
                }
                Contents = contents;
            }
        }

        private sealed class MemoryPairStore : IGameDBPairStore
        {
            private readonly Dictionary<string, Pair> m_pairs
                = new Dictionary<string, Pair>(StringComparer.OrdinalIgnoreCase);

            public StringComparer LockKeyComparer => StringComparer.OrdinalIgnoreCase;

            public GameDBResolvedPath Resolve(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    throw new ArgumentException("Database path is required.");
                }
                var normalized = assetPath.Trim().Replace('\\', '/');
                if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || !normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Database path must be an Assets-relative data JSON path.");
                }
                var schema = Path.ChangeExtension(normalized, ".schema.json")
                    .Replace('\\', '/');
                return new GameDBResolvedPath(normalized, schema,
                    normalized.Substring("Assets/".Length), normalized, schema,
                    normalized.ToLowerInvariant());
            }

            public GameDBPairRead Read(string assetPath)
            {
                var resolved = Resolve(assetPath);
                m_pairs.TryGetValue(resolved.LockKey, out var pair);
                return new GameDBPairRead(resolved, pair?.Data?.ToArray(),
                    pair?.Schema?.ToArray(), Token(pair));
            }

            public GameDBPairCommitResult Commit(string assetPath,
                GameDBDiskToken expectedToken, byte[] dataBytes, byte[] schemaBytes)
            {
                var resolved = Resolve(assetPath);
                m_pairs.TryGetValue(resolved.LockKey, out var before);
                Assert.That(Token(before), Is.EqualTo(expectedToken));
                var pair = new Pair(dataBytes.ToArray(), schemaBytes.ToArray());
                m_pairs[resolved.LockKey] = pair;
                return new GameDBPairCommitResult
                {
                    Status = GameDBPairCommitStatus.Committed,
                    TokenBefore = expectedToken,
                    TokenAfter = Token(pair)
                };
            }

            private static GameDBDiskToken Token(Pair pair)
            {
                return new GameDBDiskToken
                {
                    DataExists = pair?.Data != null,
                    SchemaExists = pair?.Schema != null,
                    DataSha256 = Hash(pair?.Data),
                    SchemaSha256 = Hash(pair?.Schema)
                };
            }

            private static string Hash(byte[] bytes)
            {
                if (bytes == null)
                {
                    return null;
                }
                using (var algorithm = SHA256.Create())
                {
                    return string.Concat(algorithm.ComputeHash(bytes)
                        .Select(value => value.ToString("x2")));
                }
            }

            private sealed class Pair
            {
                internal byte[] Data { get; }
                internal byte[] Schema { get; }

                internal Pair(byte[] data, byte[] schema)
                {
                    Data = data;
                    Schema = schema;
                }
            }
        }

        private sealed class NoOpPostSaveActions : IGameDBPostSaveActions
        {
            public void Import(string assetPath)
            {
            }

            public void Notify(string scopeName)
            {
            }
        }
    }
}
