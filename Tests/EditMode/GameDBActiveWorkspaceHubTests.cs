using GameDBEditorLibrary;
using GameDBEditorLibrary.Workspace;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    public class GameDBActiveWorkspaceHubTests
    {
        private readonly List<GameDBWorkspaceRegistration> m_domainRegistrations
            = new List<GameDBWorkspaceRegistration>();

        [TearDown]
        public void TearDown()
        {
            foreach (var registration in m_domainRegistrations)
            {
                registration.Dispose();
            }
            m_domainRegistrations.Clear();
        }

        [Test]
        public void Router_UsesHeadlessUntilARegisteredWorkspaceIsFocused()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var headless = new RecordingTarget { LoadResult = true, SaveResult = true };
            var workspace = new RecordingTarget();
            var router = new GameDBEditorFacadeRouter(hub, headless);
            var registration = hub.Register(workspace);

            Assert.That(router.LoadGameDB("before.json"), Is.True);
            Assert.That(router.SaveGameDB(), Is.True);
            router.AddRowToTable("Items", "Before", new Dictionary<string, object>());
            registration.MarkFocused();
            router.LoadGameDB("after.json");
            router.SaveGameDB();
            router.AddRowToTable("Items", "After", new Dictionary<string, object>());

            Assert.That(headless.Calls, Is.EqualTo(new[]
            {
                "load:before.json", "save", "add:Items:Before"
            }));
            Assert.That(workspace.Calls, Is.EqualTo(new[]
            {
                "load:after.json", "save", "add:Items:After"
            }));
            registration.Dispose();
        }

        [Test]
        public void Hub_RoutesToLastFocusedAndFallsBackToPreviousLiveWorkspace()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var first = new RecordingTarget();
            var second = new RecordingTarget();
            var firstRegistration = hub.Register(first);
            var secondRegistration = hub.Register(second);

            Assert.That(firstRegistration.MarkFocused(), Is.True);
            Assert.That(hub.TryGetActive(out var active), Is.True);
            Assert.That(active, Is.SameAs(first));
            Assert.That(secondRegistration.MarkFocused(), Is.True);
            Assert.That(hub.TryGetActive(out active), Is.True);
            Assert.That(active, Is.SameAs(second));

            secondRegistration.Dispose();

            Assert.That(hub.TryGetActive(out active), Is.True);
            Assert.That(active, Is.SameAs(first));
            firstRegistration.Dispose();
            Assert.That(hub.TryGetActive(out _), Is.False);
        }

        [Test]
        public void Hub_StaleRegistrationCannotFocusOrUnregisterReplacement()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var workspace = new RecordingTarget();
            var stale = hub.Register(workspace);
            Assert.That(stale.MarkFocused(), Is.True);
            var replacement = hub.Register(workspace);

            Assert.That(stale.IsDisposed, Is.True);
            Assert.That(stale.MarkFocused(), Is.False);
            stale.Dispose();
            Assert.That(hub.RegistrationCount, Is.EqualTo(1));
            Assert.That(hub.TryGetActive(out var active), Is.True);
            Assert.That(active, Is.SameAs(workspace));
            Assert.That(replacement.MarkFocused(), Is.True);
            Assert.That(hub.TryGetActive(out active), Is.True);
            Assert.That(active, Is.SameAs(workspace));
            replacement.Dispose();
        }

        [Test]
        public void Hub_RejectsRegistrationTokenFromAnotherHub()
        {
            var firstHub = new GameDBActiveWorkspaceHub();
            var secondHub = new GameDBActiveWorkspaceHub();
            var firstTarget = new RecordingTarget();
            var secondTarget = new RecordingTarget();
            var foreign = firstHub.Register(firstTarget);
            var local = secondHub.Register(secondTarget);

            Assert.That(secondHub.MarkFocused(foreign), Is.False);
            secondHub.Unregister(foreign);
            Assert.That(secondHub.RegistrationCount, Is.EqualTo(1));
            Assert.That(local.MarkFocused(), Is.True);
            Assert.That(secondHub.TryGetActive(out var active), Is.True);
            Assert.That(active, Is.SameAs(secondTarget));
            foreign.Dispose();
            local.Dispose();
        }

        [Test]
        public void Router_DoesNotFallThroughWhenFocusedWorkspaceHasNoActiveDocument()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var headless = new RecordingTarget { LoadResult = true, SaveResult = true };
            var workspace = new RecordingTarget
            {
                LoadResult = false,
                SaveResult = false,
                AddException = new InvalidOperationException("No active document.")
            };
            var registration = hub.Register(workspace);
            registration.MarkFocused();
            var router = new GameDBEditorFacadeRouter(hub, headless);

            Assert.That(router.LoadGameDB("database.json"), Is.False);
            Assert.That(router.SaveGameDB(), Is.False);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                router.AddRowToTable("Items", "Sword", new Dictionary<string, object>()));

            Assert.That(exception.Message, Is.EqualTo("No active document."));
            Assert.That(headless.Calls, Is.Empty);
            registration.Dispose();
        }

        [Test]
        public void Router_PreservesTargetResultsAndExceptionsWithoutTranslation()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var expected = new ArgumentOutOfRangeException("table", "Missing", "unknown");
            var target = new RecordingTarget
            {
                LoadResult = false,
                SaveResult = true,
                AddException = expected
            };
            var registration = hub.Register(target);
            registration.MarkFocused();
            var router = new GameDBEditorFacadeRouter(hub, new RecordingTarget());

            Assert.That(router.LoadGameDB("database.json"), Is.False);
            Assert.That(router.SaveGameDB(), Is.True);
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
                router.AddRowToTable("Items", "Sword", new Dictionary<string, object>())),
                Is.SameAs(expected));
            registration.Dispose();
        }

        [Test]
        public void Router_ConvertsWorkspaceLoadAndSaveExceptionsToFalse()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var workspace = new RecordingTarget
            {
                LoadException = new IOException("load failed"),
                SaveException = new InvalidOperationException("save failed")
            };
            var registration = hub.Register(workspace);
            registration.MarkFocused();
            var router = new GameDBEditorFacadeRouter(hub, new RecordingTarget());
            LogAssert.Expect(UnityEngine.LogType.Error, "failed to load gameDB: database.json");
            LogAssert.Expect(UnityEngine.LogType.Exception, "IOException: load failed");
            LogAssert.Expect(UnityEngine.LogType.Error, "failed to save gameDB");
            LogAssert.Expect(UnityEngine.LogType.Exception,
                "InvalidOperationException: save failed");

            Assert.That(router.LoadGameDB("database.json"), Is.False);
            Assert.That(router.SaveGameDB(), Is.False);
            registration.Dispose();
        }

        [Test]
        public void PublicFacade_RoutesToFocusedWorkspace()
        {
            var workspace = new RecordingTarget { LoadResult = true, SaveResult = false };
            var registration = GameDBEditorDomainServices.ActiveWorkspaceHub.Register(workspace);
            m_domainRegistrations.Add(registration);
            registration.MarkFocused();

            Assert.That(GameDBEditor.LoadGameDB("workspace.json"), Is.True);
            Assert.That(GameDBEditor.SaveGameDB(), Is.False);
            GameDBEditor.AddRowToTable("Items", "Sword", new Dictionary<string, object>());

            Assert.That(workspace.Calls, Is.EqualTo(new[]
            {
                "load:workspace.json", "save", "add:Items:Sword"
            }));
            registration.Dispose();
            m_domainRegistrations.Remove(registration);
        }

        [Test]
        public void Hub_PrunesCollectedWorkspaceRegistration()
        {
            var hub = new GameDBActiveWorkspaceHub();
            var weakTarget = RegisterEphemeralFocusedWorkspace(hub);

            for (var attempt = 0; attempt < 5 && weakTarget.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            if (weakTarget.IsAlive)
            {
                Assert.Ignore("The conservative Unity GC retained the ephemeral workspace.");
            }

            Assert.That(hub.TryGetActive(out _), Is.False);
            Assert.That(hub.RegistrationCount, Is.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterEphemeralFocusedWorkspace(
            GameDBActiveWorkspaceHub hub)
        {
            var workspace = new RecordingTarget();
            var weakTarget = new WeakReference(workspace);
            hub.Register(workspace).MarkFocused();
            return weakTarget;
        }

        private sealed class RecordingTarget : IGameDBEditorFacadeTarget
        {
            internal List<string> Calls { get; } = new List<string>();
            internal bool LoadResult { get; set; }
            internal bool SaveResult { get; set; }
            internal Exception LoadException { get; set; }
            internal Exception SaveException { get; set; }
            internal Exception AddException { get; set; }

            public bool LoadGameDB(string gameDBPath)
            {
                Calls.Add("load:" + gameDBPath);
                if (LoadException != null)
                {
                    throw LoadException;
                }
                return LoadResult;
            }

            public bool SaveGameDB()
            {
                Calls.Add("save");
                if (SaveException != null)
                {
                    throw SaveException;
                }
                return SaveResult;
            }

            public void AddRowToTable(string table, string key,
                Dictionary<string, object> data)
            {
                Calls.Add($"add:{table}:{key}");
                if (AddException != null)
                {
                    throw AddException;
                }
            }
        }
    }
}
