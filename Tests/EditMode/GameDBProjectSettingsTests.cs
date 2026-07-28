using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBProjectSettingsTests
    {
        [Test]
        public void Update_NormalizesDeduplicatesAndIsolatesSnapshotCollections()
        {
            var store = new MemoryStore();
            var paths = new List<string>
            {
                " Databases\\items.json ",
                "Databases/items.json",
                "Databases/other.json",
                " "
            };
            var enumTypes = new List<string> { "Z.Type", " A.Type ", "Z.Type" };
            var service = CreateService(store);

            var result = service.Update(paths, enumTypes, " Generated\\Code ", " Build\\Data ");
            paths[0] = "mutated";
            enumTypes.Clear();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Snapshot.RegisteredDatabasePaths,
                Is.EqualTo(new[] { "Databases/items.json", "Databases/other.json" }));
            Assert.That(result.Snapshot.ImportedEnumTypeNames,
                Is.EqualTo(new[] { "A.Type", "Z.Type" }));
            Assert.That(result.Snapshot.ExportPath, Is.EqualTo("Generated/Code"));
            Assert.That(result.Snapshot.BuildPath, Is.EqualTo("Build/Data"));
            var readOnlyPaths = (IList<string>)result.Snapshot.RegisteredDatabasePaths;
            var readOnlyEnumTypes = (IList<string>)result.Snapshot.ImportedEnumTypeNames;
            Assert.That(readOnlyPaths.IsReadOnly, Is.True);
            Assert.That(readOnlyEnumTypes.IsReadOnly, Is.True);
            var readOnlyIssues = (IList<GameDBProjectSettingsIssue>)
                result.Snapshot.ValidationIssues;
            Assert.That(readOnlyIssues.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => readOnlyPaths[0] = "mutated");
            Assert.Throws<NotSupportedException>(() => readOnlyEnumTypes[0] = "mutated");
            Assert.Throws<NotSupportedException>(() => readOnlyIssues.Add(null));
        }

        [Test]
        public void Load_PreservesInvalidValuesAndReportsValidationIssues()
        {
            var store = new MemoryStore
            {
                Contents = Serialize(new[] { "present.json", "missing.json" },
                    new[] { "Present.Enum", "Missing.Enum" }, "Generated", "Build")
            };
            var service = new GameDBProjectSettingsService(store,
                path => path == "present.json",
                typeName => typeName == "Present.Enum");

            var result = service.Load();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Snapshot.RegisteredDatabasePaths,
                Is.EqualTo(new[] { "present.json", "missing.json" }));
            Assert.That(result.Snapshot.ImportedEnumTypeNames,
                Is.EqualTo(new[] { "Missing.Enum", "Present.Enum" }));
            Assert.That(result.Snapshot.ValidationIssues.Select(issue =>
                    new { issue.Kind, issue.Value }),
                Is.EqualTo(new[]
                {
                    new
                    {
                        Kind = GameDBProjectSettingsIssueKind.MissingDatabasePath,
                        Value = "missing.json"
                    },
                    new
                    {
                        Kind = GameDBProjectSettingsIssueKind.UnresolvedImportedEnumType,
                        Value = "Missing.Enum"
                    }
                }));
        }

        [Test]
        public void Load_ValidatorExceptionsBecomeIssuesInsteadOfEscaping()
        {
            var store = new MemoryStore
            {
                Contents = Serialize(new[] { "database.json" }, new[] { "Game.Enum" }, "", "")
            };
            var service = new GameDBProjectSettingsService(store,
                path => throw new IOException("path validation failed"),
                typeName => throw new InvalidOperationException("type validation failed"));

            var result = service.Load();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Snapshot.ValidationIssues.Select(issue => issue.Kind),
                Is.EqualTo(new[]
                {
                    GameDBProjectSettingsIssueKind.MissingDatabasePath,
                    GameDBProjectSettingsIssueKind.UnresolvedImportedEnumType
                }));
        }

        [Test]
        public void Load_MissingOrMalformedSettingsReturnsStableDefaultsAndLoadsOnce()
        {
            var missingStore = new MemoryStore();
            var missingService = CreateService(missingStore);

            var missing = missingService.Load();
            missingStore.Contents = Serialize(new[] { "later.json" }, Array.Empty<string>(), "", "");
            var repeated = missingService.Load();

            Assert.That(missing.Success, Is.True);
            Assert.That(missing.Snapshot.RegisteredDatabasePaths, Is.Empty);
            Assert.That(repeated.Snapshot, Is.SameAs(missing.Snapshot));
            Assert.That(missingStore.ReadCount, Is.Zero);

            var malformedStore = new MemoryStore { Contents = "not json" };
            var malformedService = CreateService(malformedStore);
            var malformed = malformedService.Load();

            Assert.That(malformed.Success, Is.False);
            Assert.That(malformed.Error, Does.StartWith("Failed to load GameDB project settings:"));
            Assert.That(malformed.Snapshot.RegisteredDatabasePaths, Is.Empty);
            Assert.That(malformed.Snapshot.ImportedEnumTypeNames, Is.Empty);
            Assert.That(malformedService.GetSnapshot(), Is.SameAs(malformed.Snapshot));
            Assert.That(malformedStore.ReadCount, Is.EqualTo(1));

            var refused = malformedService.Update(new[] { "replacement.json" },
                Array.Empty<string>(), "Generated", "Build");
            Assert.That(refused.Success, Is.False);
            Assert.That(refused.Error, Is.EqualTo(malformed.Error));
            Assert.That(malformedStore.WriteCount, Is.Zero);
            Assert.That(malformedStore.Contents, Is.EqualTo("not json"));
        }

        [Test]
        public void Update_PublishesAfterWriteAndIsolatesSubscribersInRegistrationOrder()
        {
            var observed = new List<string>();
            GameDBProjectSettingsSnapshot observedCurrent = null;
            GameDBProjectSettingsSnapshot observedFromService = null;
            string observedPreviousExportPath = null;
            GameDBProjectSettingsService service = null;
            var store = new MemoryStore
            {
                OnWrite = () => observed.Add("write")
            };
            service = CreateService(store);
            service.Changed += change =>
            {
                observed.Add("first:" + change.Current.ExportPath);
                observedCurrent = change.Current;
                observedFromService = service.GetSnapshot();
                observedPreviousExportPath = change.Previous.ExportPath;
            };
            service.Changed += change => throw new InvalidOperationException("subscriber failed");
            service.Changed += change => observed.Add("last:" + change.Current.ExportPath);

            var result = service.Update(Array.Empty<string>(), Array.Empty<string>(),
                "Generated", string.Empty);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(observed, Is.EqualTo(new[]
            {
                "write",
                "first:Generated",
                "last:Generated"
            }));
            Assert.That(observedCurrent, Is.SameAs(result.Snapshot));
            Assert.That(observedFromService, Is.SameAs(observedCurrent));
            Assert.That(observedPreviousExportPath, Is.Empty);
            Assert.That(result.NotificationErrors, Is.EqualTo(new[] { "subscriber failed" }));
        }

        [Test]
        public void Update_NormalizedNoOpDoesNotWriteOrNotify()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var notifications = 0;
            service.Changed += change => notifications++;
            var first = service.Update(new[] { "database.json" }, new[] { "Game.Enum" },
                "Generated", "Build");

            var second = service.Update(new[] { " database.json ", "database.json" },
                new[] { " Game.Enum " }, " Generated ", " Build ");

            Assert.That(first.Changed, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(second.Changed, Is.False);
            Assert.That(second.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Revalidate_PublishesChangedIssuesWithoutWritingSettings()
        {
            var pathExists = false;
            var store = new MemoryStore();
            var service = new GameDBProjectSettingsService(store,
                path => pathExists, typeName => true);
            var initial = service.Update(new[] { "database.json" },
                Array.Empty<string>(), string.Empty, string.Empty);
            GameDBProjectSettingsChange observed = null;
            service.Changed += change => observed = change;
            pathExists = true;

            var refreshed = service.Revalidate();

            Assert.That(initial.Snapshot.ValidationIssues.Count, Is.EqualTo(1));
            Assert.That(refreshed.Success, Is.True);
            Assert.That(refreshed.Changed, Is.True);
            Assert.That(refreshed.Snapshot.ValidationIssues, Is.Empty);
            Assert.That(observed.Previous, Is.SameAs(initial.Snapshot));
            Assert.That(observed.Current, Is.SameAs(refreshed.Snapshot));
            Assert.That(store.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_RoundTripsExistingFourKeyWireContract()
        {
            var store = new MemoryStore();
            var writer = CreateService(store);
            var written = writer.Update(new[] { "database.json" },
                new[] { "Game.Enum" }, "Generated", "Build");

            var parsed = (IDictionary<string, object>)JsonSerialization.Deserialize(store.Contents);
            var reader = CreateService(store);
            var loaded = reader.Load();

            Assert.That(written.Success, Is.True, written.Error);
            Assert.That(parsed.Keys, Is.EquivalentTo(new[]
            {
                "gameDBPaths", "exportPath", "importedEnums", "buildPath"
            }));
            Assert.That((IEnumerable<object>)parsed["gameDBPaths"],
                Is.EqualTo(new object[] { "database.json" }));
            Assert.That((IEnumerable<object>)parsed["importedEnums"],
                Is.EqualTo(new object[] { "Game.Enum" }));
            Assert.That(parsed["exportPath"], Is.EqualTo("Generated"));
            Assert.That(parsed["buildPath"], Is.EqualTo("Build"));
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Snapshot.RegisteredDatabasePaths,
                Is.EqualTo(written.Snapshot.RegisteredDatabasePaths));
            Assert.That(loaded.Snapshot.ImportedEnumTypeNames,
                Is.EqualTo(written.Snapshot.ImportedEnumTypeNames));
            Assert.That(loaded.Snapshot.ExportPath, Is.EqualTo("Generated"));
            Assert.That(loaded.Snapshot.BuildPath, Is.EqualTo("Build"));
        }

        [Test]
        public void Update_WriteFailureKeepsPreviousSnapshotAndEmitsNoEvent()
        {
            var store = new MemoryStore();
            var service = CreateService(store);
            var initial = service.Update(Array.Empty<string>(), Array.Empty<string>(),
                "Initial", string.Empty);
            var notifications = 0;
            service.Changed += change => notifications++;
            store.WriteException = new IOException("disk full");

            var failed = service.Update(Array.Empty<string>(), Array.Empty<string>(),
                "Replacement", string.Empty);

            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Changed, Is.False);
            Assert.That(failed.Error, Does.Contain("disk full"));
            Assert.That(failed.Snapshot, Is.SameAs(initial.Snapshot));
            Assert.That(service.GetSnapshot(), Is.SameAs(initial.Snapshot));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void FileStore_FirstWriteCreatesParentAndDestination()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "GameDBProjectSettingsTests_" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "nested", "GameDBSettings.json");

            try
            {
                var store = new GameDBProjectSettingsFileStore(path);

                Assert.That(store.Exists, Is.False);
                store.WriteAtomically("new");

                Assert.That(store.Exists, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void FileStore_ReplacesDestinationAndCleansTemporaryArtifacts()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "GameDBProjectSettingsTests_" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "GameDBSettings.json");

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, "old");
                var store = new GameDBProjectSettingsFileStore(path);

                store.WriteAtomically("new");

                Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static GameDBProjectSettingsService CreateService(MemoryStore store)
        {
            return new GameDBProjectSettingsService(store, path => true, typeName => true);
        }

        private static string Serialize(IEnumerable<string> paths,
            IEnumerable<string> enumTypes, string exportPath, string buildPath)
        {
            return JsonSerialization.Serialize(new Dictionary<string, object>
            {
                { "gameDBPaths", paths.ToArray() },
                { "exportPath", exportPath },
                { "importedEnums", enumTypes.ToArray() },
                { "buildPath", buildPath }
            });
        }

        private sealed class MemoryStore : IGameDBProjectSettingsStore
        {
            internal string Contents { get; set; }
            internal int ReadCount { get; private set; }
            internal int WriteCount { get; private set; }
            internal Exception WriteException { get; set; }
            internal Action OnWrite { get; set; }

            public bool Exists => Contents != null;

            public string ReadAllText()
            {
                ReadCount++;
                return Contents;
            }

            public void WriteAtomically(string contents)
            {
                WriteCount++;
                OnWrite?.Invoke();
                if (WriteException != null)
                {
                    throw WriteException;
                }

                Contents = contents;
            }
        }
    }
}
