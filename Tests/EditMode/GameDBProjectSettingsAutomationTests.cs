using GameDBEditorLibrary.Automation;
using GameDBLibrary;
using WorkspaceProjectSettingsService
    = GameDBEditorLibrary.Workspace.GameDBProjectSettingsService;
using WorkspaceProjectSettingsStore
    = GameDBEditorLibrary.Workspace.IGameDBProjectSettingsStore;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBProjectSettingsAutomationTests
    {
        private const string DatabasePath = "Assets/Data/Resources/GameDB/gameplay.json";
        private const string StoredDatabasePath = "Data/Resources/GameDB/gameplay.json";
        private const string ExportPath = "Assets/Generated/GameDB";

        [Test]
        public void Update_DryRunCommitInspectAndNoOpUseCanonicalPathsAndStableRevisions()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            var request = DesiredRequest(initial.Snapshot.Revision, true);

            var preview = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(preview.Success, Is.True, preview.Message);
            Assert.That(preview.DryRun, Is.True);
            Assert.That(preview.CommitStatus, Is.EqualTo(GameDBCommitStatus.DryRun));
            Assert.That(preview.FilesCommitted, Is.False);
            Assert.That(preview.SnapshotIsProspective, Is.True);
            Assert.That(preview.ChangedPaths,
                Is.EqualTo(new[] { "ProjectSettings/GameDBSettings.json" }));
            Assert.That(preview.RevisionBefore, Is.EqualTo(initial.Snapshot.Revision));
            Assert.That(preview.RevisionAfter, Is.Not.EqualTo(preview.RevisionBefore));
            AssertDesiredSnapshot(preview.Snapshot);
            Assert.That(store.WriteCount, Is.Zero);

            request.Options.DryRun = false;
            var committed = GameDBAutomationService.UpdateProjectSettings(request, service);
            var inspected = GameDBAutomationService.InspectProjectSettings(service);

            Assert.That(committed.Success, Is.True, committed.Message);
            Assert.That(committed.CommitStatus, Is.EqualTo(GameDBCommitStatus.Saved));
            Assert.That(committed.FilesCommitted, Is.True);
            Assert.That(committed.RevisionBefore, Is.EqualTo(preview.RevisionBefore));
            Assert.That(committed.RevisionAfter, Is.EqualTo(preview.RevisionAfter));
            AssertDesiredSnapshot(committed.Snapshot);
            AssertDesiredSnapshot(inspected.Snapshot);
            Assert.That(inspected.Snapshot.Revision, Is.EqualTo(committed.RevisionAfter));
            Assert.That(store.WriteCount, Is.EqualTo(1));
            var wire = (IDictionary<string, object>)JsonSerialization.Deserialize(store.Contents);
            Assert.That(((IEnumerable<object>)wire["gameDBPaths"]).Cast<string>(),
                Is.EqualTo(new[] { StoredDatabasePath }));
            Assert.That(wire["exportPath"], Is.EqualTo("Generated/GameDB"));
            Assert.That(wire["buildPath"], Is.EqualTo(string.Empty));
            Assert.That(((IEnumerable<object>)wire["importedEnums"]), Is.Empty);

            request.Options.ExpectedRevision = committed.RevisionAfter;
            var repeated = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(repeated.Success, Is.True, repeated.Message);
            Assert.That(repeated.CommitStatus, Is.EqualTo(GameDBCommitStatus.NoChanges));
            Assert.That(repeated.FilesCommitted, Is.False);
            Assert.That(repeated.ChangedPaths, Is.Empty);
            Assert.That(repeated.RevisionAfter, Is.EqualTo(committed.RevisionAfter));
            Assert.That(store.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_ReplacesAndClearsAllSettingsValuesExplicitly()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            service.Update(new[] { StoredDatabasePath, "Other/database.json" },
                new[] { "Legacy.GameEnum" }, "Legacy/Generated", "Legacy/Build");
            var current = GameDBAutomationService.InspectProjectSettings(service);

            var result = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(current.Snapshot.Revision, false), service);

            Assert.That(result.Success, Is.True, result.Message);
            AssertDesiredSnapshot(result.Snapshot);
            Assert.That(result.Snapshot.RegisteredDatabasePaths,
                Is.EqualTo(new[] { DatabasePath }));
            Assert.That(result.Snapshot.ImportedEnumTypeNames, Is.Empty);
            Assert.That(result.Snapshot.ExportPath, Is.EqualTo(ExportPath));
            Assert.That(result.Snapshot.BuildPath, Is.Empty);
            var wire = (IDictionary<string, object>)JsonSerialization.Deserialize(store.Contents);
            Assert.That(((IEnumerable<object>)wire["gameDBPaths"]).Cast<string>(),
                Is.EqualTo(new[] { StoredDatabasePath }));
            Assert.That(((IEnumerable<object>)wire["importedEnums"]), Is.Empty);
            Assert.That(wire["exportPath"], Is.EqualTo("Generated/GameDB"));
            Assert.That(wire["buildPath"], Is.EqualTo(string.Empty));
        }

        [Test]
        public void Update_RequiresExplicitFullStateMembers()
        {
            var store = new MemoryStore();
            var service = CreateService(store);

            var result = GameDBAutomationService.UpdateProjectSettings(
                new GameDBProjectSettingsRequest
                {
                    ExportPath = ExportPath,
                    BuildPath = string.Empty
                }, service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.NotAttempted));
            Assert.That(result.Message, Does.Contain("are required"));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Update_StaleRevisionReturnsCurrentSnapshotWithoutWriting()
        {
            var store = new MemoryStore();
            var service = CreateService(store);

            var result = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest("stale-revision", false), service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.Conflict));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.ChangedPaths, Is.Empty);
            Assert.That(result.RevisionBefore, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.EqualTo(result.Snapshot.Revision));
            Assert.That(result.Snapshot.RegisteredDatabasePaths, Is.Empty);
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void InspectAndUpdate_RefreshExternalSettingsBeforeRevisionGuard()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            store.ReplaceContents(JsonSerialization.Serialize(new Dictionary<string, object>
            {
                { "gameDBPaths", new[] { StoredDatabasePath } },
                { "exportPath", "Generated/GameDB" },
                { "importedEnums", Array.Empty<string>() },
                { "buildPath", string.Empty }
            }));

            var refreshed = GameDBAutomationService.InspectProjectSettings(service);
            var stale = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(initial.Snapshot.Revision, false), service);

            Assert.That(refreshed.Success, Is.True, refreshed.Message);
            Assert.That(refreshed.Snapshot.Revision, Is.Not.EqualTo(initial.Snapshot.Revision));
            AssertDesiredSnapshot(refreshed.Snapshot);
            Assert.That(stale.Success, Is.False);
            Assert.That(stale.CommitStatus, Is.EqualTo(GameDBCommitStatus.Conflict));
            Assert.That(stale.Snapshot.Revision, Is.EqualTo(refreshed.Snapshot.Revision));
            Assert.That(stale.FilesCommitted, Is.False);
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Update_MissingDatabasePairReturnsProspectiveValidationIssueWithoutWriting()
        {
            var store = new MemoryStore();
            var service = new WorkspaceProjectSettingsService(store,
                path => false, typeName => true);
            var initial = GameDBAutomationService.InspectProjectSettings(service);

            var result = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(initial.Snapshot.Revision, true), service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.ValidationFailed));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.SnapshotIsProspective, Is.True);
            Assert.That(result.RevisionBefore, Is.EqualTo(initial.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.Not.EqualTo(result.RevisionBefore));
            AssertDesiredSnapshot(result.Snapshot);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind,
                Is.EqualTo(GameDBProjectSettingsIssueKind.MissingDatabase));
            Assert.That(result.Issues[0].Value, Is.EqualTo(DatabasePath));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Update_RequireValidFalseCommitsSupportedShapeUnresolvedValues()
        {
            var store = new MemoryStore();
            var service = new WorkspaceProjectSettingsService(store,
                path => false, typeName => false);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            var request = DesiredRequest(initial.Snapshot.Revision, false);
            request.ImportedEnumTypeNames.Add("Missing.GameEnum");
            request.Options.RequireValid = false;

            var result = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.Saved));
            Assert.That(result.FilesCommitted, Is.True);
            Assert.That(result.Issues.Select(issue => issue.Kind), Is.EquivalentTo(new[]
            {
                GameDBProjectSettingsIssueKind.MissingDatabase,
                GameDBProjectSettingsIssueKind.UnresolvedImportedEnumType
            }));
            Assert.That(store.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_InvalidDatabaseIssueIdentifiesExactFailingPath()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            var request = DesiredRequest(initial.Snapshot.Revision, true);
            request.RegisteredDatabasePaths.Add("Assets/invalid.schema.json");

            var result = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.ValidationFailed));
            Assert.That(result.Issues.Single().Kind,
                Is.EqualTo(GameDBProjectSettingsIssueKind.InvalidDatabasePath));
            Assert.That(result.Issues.Single().Value,
                Is.EqualTo("Assets/invalid.schema.json"));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [TestCase("Assets", GameDBProjectSettingsIssueKind.InvalidExportPath)]
        [TestCase("Assets/.", GameDBProjectSettingsIssueKind.InvalidExportPath)]
        [TestCase("../Generated", GameDBProjectSettingsIssueKind.InvalidExportPath)]
        public void Update_InvalidOutputPathReturnsStructuredValidationIssue(string exportPath,
            GameDBProjectSettingsIssueKind expectedKind)
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            var request = DesiredRequest(initial.Snapshot.Revision, true);
            request.ExportPath = exportPath;

            var result = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.ValidationFailed));
            Assert.That(result.SnapshotIsProspective, Is.False);
            Assert.That(result.Issues.Single().Kind, Is.EqualTo(expectedKind));
            Assert.That(result.Issues.Single().Value, Is.EqualTo(exportPath));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Update_CanonicalizesEquivalentDatabaseAndOutputPaths()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            var request = DesiredRequest(initial.Snapshot.Revision, true);
            request.RegisteredDatabasePaths[0]
                = "Assets/Data/Resources/GameDB/../GameDB/gameplay.json";
            request.ExportPath = "Assets/Generated/Temp/../GameDB";

            var result = GameDBAutomationService.UpdateProjectSettings(request, service);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Snapshot.RegisteredDatabasePaths,
                Is.EqualTo(new[] { DatabasePath }));
            Assert.That(result.Snapshot.ExportPath, Is.EqualTo(ExportPath));
        }

        [Test]
        public void Update_PersistenceFailureReturnsCurrentSnapshotWithoutCommit()
        {
            var store = new MemoryStore { WriteException = new IOException("disk full") };
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);

            var result = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(initial.Snapshot.Revision, false), service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.PersistenceFailed));
            Assert.That(result.FilesCommitted, Is.False);
            Assert.That(result.SnapshotIsProspective, Is.False);
            Assert.That(result.Snapshot.Revision, Is.EqualTo(initial.Snapshot.Revision));
            Assert.That(result.RevisionBefore, Is.EqualTo(initial.Snapshot.Revision));
            Assert.That(result.RevisionAfter, Is.EqualTo(initial.Snapshot.Revision));
        }

        [Test]
        public void InspectAndUpdate_PreserveMalformedExternalFileAndReportError()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = GameDBAutomationService.InspectProjectSettings(service);
            store.ReplaceContents("not json");

            var inspected = GameDBAutomationService.InspectProjectSettings(service);
            var updated = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(initial.Snapshot.Revision, false), service);

            Assert.That(inspected.Success, Is.False);
            Assert.That(inspected.Message, Does.Contain("Failed to load"));
            Assert.That(inspected.Snapshot.Revision, Is.EqualTo(initial.Snapshot.Revision));
            Assert.That(updated.Success, Is.False);
            Assert.That(updated.FilesCommitted, Is.False);
            Assert.That(store.Contents, Is.EqualTo("not json"));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void Update_ListenerFailureReportsCommittedStateAndPendingPostSaveWork()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            service.Changed += _ => throw new InvalidOperationException("listener failed");
            var initial = GameDBAutomationService.InspectProjectSettings(service);

            var result = GameDBAutomationService.UpdateProjectSettings(
                DesiredRequest(initial.Snapshot.Revision, false), service);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CommitStatus, Is.EqualTo(GameDBCommitStatus.PostSavePending));
            Assert.That(result.FilesCommitted, Is.True);
            Assert.That(result.PostSavePending, Is.True);
            Assert.That(result.PostSaveErrors, Is.EqualTo(new[] { "listener failed" }));
            Assert.That(result.RecoveryArtifacts, Is.Empty);
            Assert.That(result.ChangedPaths,
                Is.EqualTo(new[] { "ProjectSettings/GameDBSettings.json" }));
            Assert.That(store.WriteCount, Is.EqualTo(1));
            AssertDesiredSnapshot(GameDBAutomationService.InspectProjectSettings(service).Snapshot);
        }

        private static GameDBProjectSettingsRequest DesiredRequest(string expectedRevision,
            bool dryRun)
        {
            return new GameDBProjectSettingsRequest
            {
                RegisteredDatabasePaths = new List<string> { DatabasePath },
                ImportedEnumTypeNames = new List<string>(),
                ExportPath = ExportPath,
                BuildPath = string.Empty,
                Options = new GameDBProjectSettingsOptions
                {
                    DryRun = dryRun,
                    ExpectedRevision = expectedRevision
                }
            };
        }

        private static void AssertDesiredSnapshot(GameDBProjectSettingsSnapshot snapshot)
        {
            Assert.That(snapshot.Revision, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(snapshot.RegisteredDatabasePaths,
                Is.EqualTo(new[] { DatabasePath }));
            Assert.That(snapshot.ImportedEnumTypeNames, Is.Empty);
            Assert.That(snapshot.ExportPath, Is.EqualTo(ExportPath));
            Assert.That(snapshot.BuildPath, Is.Empty);
        }

        private static WorkspaceProjectSettingsService CreateService(MemoryStore store)
        {
            return new WorkspaceProjectSettingsService(store,
                path => path == StoredDatabasePath, typeName => true);
        }

        private sealed class MemoryStore : WorkspaceProjectSettingsStore
        {
            internal string Contents { get; private set; }
            internal int WriteCount { get; private set; }
            internal Exception WriteException { get; set; }

            public bool Exists => Contents != null;

            public string ReadAllText()
            {
                return Contents;
            }

            public void WriteAtomically(string contents)
            {
                WriteCount++;
                if (WriteException != null)
                {
                    throw WriteException;
                }
                Contents = contents;
            }

            internal void ReplaceContents(string contents)
            {
                Contents = contents;
            }
        }
    }
}
