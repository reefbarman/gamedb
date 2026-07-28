using GameDBEditorLibrary;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GameDBLibrary.Tests
{
    public class GameDBRuntimeRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            GameDBEditorDomainServices.BeginPlaySession();
        }

        [TearDown]
        public void TearDown()
        {
            GameDBEditorDomainServices.BeginPlaySession();
        }

        [Test]
        public void Register_DeduplicatesByReferenceAndPreservesStableTargetId()
        {
            var registry = new GameDBRuntimeRegistry();
            var target = new TestRuntimeDB("Items", "Game");

            var first = registry.Register(target);
            var second = registry.Register(target);

            Assert.That(first.Changed, Is.True);
            Assert.That(second.Changed, Is.False);
            Assert.That(second.Target.TargetId, Is.EqualTo(first.Target.TargetId));
            Assert.That(second.Snapshot.Targets, Has.Count.EqualTo(1));
            Assert.That(registry.TryResolve(first.Target.TargetId, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(target));
        }

        [Test]
        public void BeginPlaySession_InvalidatesStaleIdsAndAdvancesEpoch()
        {
            var registry = new GameDBRuntimeRegistry();
            var target = new TestRuntimeDB("Items", "Game");
            var registered = registry.Register(target);

            var cleared = registry.BeginPlaySession();
            var replacement = registry.Register(target);

            Assert.That(cleared.Changed, Is.True);
            Assert.That(cleared.Snapshot.Epoch, Is.EqualTo(registered.Snapshot.Epoch + 1));
            Assert.That(cleared.Snapshot.Revision,
                Is.GreaterThan(registered.Snapshot.Revision));
            Assert.That(cleared.Snapshot.Targets, Is.Empty);
            Assert.That(registry.TryResolve(registered.Target.TargetId, out _), Is.False);
            Assert.That(replacement.Target.TargetId, Is.Not.EqualTo(registered.Target.TargetId));
            Assert.That(replacement.Target.Epoch, Is.EqualTo(cleared.Snapshot.Epoch));
        }

        [Test]
        public void Snapshot_DisambiguatesDuplicateAndBlankNamesDeterministically()
        {
            var registry = new GameDBRuntimeRegistry();
            var targets = new[]
            {
                new TestRuntimeDB("Shared", "First"),
                new TestRuntimeDB("Shared", "Second"),
                new TestRuntimeDB(" ", "Blank"),
                new TestRuntimeDB("", "AlsoBlank")
            };

            foreach (var target in targets)
            {
                registry.Register(target);
            }
            var snapshot = registry.GetSnapshot();

            Assert.That(snapshot.Targets.Select(target => target.Name), Is.EqualTo(new[]
            {
                "Shared", "Shared", "Unnamed GameDB", "Unnamed GameDB"
            }));
            Assert.That(snapshot.Targets.Select(target => target.DisplayName), Is.EqualTo(new[]
            {
                "Shared (1)", "Shared (2)", "Unnamed GameDB (1)", "Unnamed GameDB (2)"
            }));
            var readOnlyTargets = (IList<GameDBRuntimeTargetDescriptor>)snapshot.Targets;
            Assert.That(readOnlyTargets.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => readOnlyTargets.Add(null));
            Assert.That(typeof(GameDBRuntimeTargetDescriptor).GetProperties(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)
                .Any(property => property.PropertyType == typeof(GameDBBase)), Is.False);
        }

        [Test]
        public void Snapshot_DisplayNamesRemainUniqueWhenRawNameMatchesGeneratedSuffix()
        {
            var registry = new GameDBRuntimeRegistry();
            var targets = new[]
            {
                new TestRuntimeDB("Shared", "First"),
                new TestRuntimeDB("Shared", "Second"),
                new TestRuntimeDB("Shared (1)", "Third")
            };
            foreach (var target in targets)
            {
                registry.Register(target);
            }

            var displayNames = registry.GetSnapshot().Targets
                .Select(target => target.DisplayName).ToArray();

            Assert.That(displayNames.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(displayNames.Length));
        }

        [Test]
        public void DomainTransition_ClearsRegistry()
        {
            var target = new TestRuntimeDB("Items", "Game");
            GameDBEditor.AddRuntimeDB(target);
            var registered = GameDBEditorDomainServices.RuntimeRegistry.GetSnapshot();

            var cleared = GameDBEditorDomainServices.BeginPlaySession();

            Assert.That(cleared.Snapshot.Epoch, Is.EqualTo(registered.Epoch + 1));
            Assert.That(cleared.Snapshot.Targets, Is.Empty);
            Assert.That(GameDBEditorDomainServices.RuntimeRegistry.TryResolve(
                registered.Targets[0].TargetId, out _), Is.False);
        }

        [Test]
        public void DeadTarget_IsPrunedFromSnapshotAndCannotBeResolved()
        {
            var registry = new GameDBRuntimeRegistry();
            var registered = RegisterEphemeralTarget(registry);

            for (var attempt = 0; attempt < 5 && registered.Target.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            if (registered.Target.IsAlive)
            {
                Assert.Ignore("The conservative Unity GC retained the ephemeral runtime target.");
            }
            var snapshot = registry.GetSnapshot();

            Assert.That(snapshot.Targets, Is.Empty);
            Assert.That(registry.TryResolve(registered.TargetId, out _), Is.False);
        }

        [Test]
        public void Changed_NotifiesInOrderAndIsolatesSubscriberFailures()
        {
            var registry = new GameDBRuntimeRegistry();
            var observed = new List<string>();
            registry.Changed += snapshot => observed.Add("first:" + snapshot.Targets.Count);
            registry.Changed += snapshot => throw new InvalidOperationException("subscriber failed");
            registry.Changed += snapshot => observed.Add("last:" + snapshot.Targets.Count);

            var registered = registry.Register(new TestRuntimeDB("Items", "Game"));

            Assert.That(observed, Is.EqualTo(new[] { "first:1", "last:1" }));
            Assert.That(registered.NotificationErrors,
                Is.EqualTo(new[] { "subscriber failed" }));
        }

        [Test]
        public void TryResolve_RejectsMissingAndInvalidTargetIds()
        {
            var registry = new GameDBRuntimeRegistry();

            Assert.That(registry.TryResolve(null, out _), Is.False);
            Assert.That(registry.TryResolve(" ", out _), Is.False);
            Assert.That(registry.TryResolve("runtime-1-missing", out _), Is.False);
        }

        [Test]
        public void AddRuntimeDB_PreservesBridgeAndRegistersUniqueTarget()
        {
            var target = new TestRuntimeDB("Items", "Game");

            GameDBEditor.AddRuntimeDB(target);
            GameDBEditor.AddRuntimeDB(target);

            var snapshot = GameDBEditorDomainServices.RuntimeRegistry.GetSnapshot();
            Assert.That(snapshot.Targets, Has.Count.EqualTo(1));
            Assert.That(GameDBEditorDomainServices.RuntimeRegistry.TryResolve(
                snapshot.Targets[0].TargetId, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(target));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static EphemeralRegistration RegisterEphemeralTarget(
            GameDBRuntimeRegistry registry)
        {
            var target = new TestRuntimeDB("Ephemeral", "Game");
            return new EphemeralRegistration(registry.Register(target).Target.TargetId,
                new WeakReference(target));
        }

        private sealed class EphemeralRegistration
        {
            internal string TargetId { get; }
            internal WeakReference Target { get; }

            internal EphemeralRegistration(string targetId, WeakReference target)
            {
                TargetId = targetId;
                Target = target;
            }
        }

        private sealed class TestRuntimeDB : GameDBBase
        {
            internal TestRuntimeDB(string name, string scopeName) : base(name, scopeName)
            {
            }
        }
    }
}
