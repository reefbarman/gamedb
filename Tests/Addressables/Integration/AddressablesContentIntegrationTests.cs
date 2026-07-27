using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace GameDBLibraryAddressables.Tests
{
    internal sealed class AddressablesContentIntegrationTests
    {
        private const string TestRoot = "Assets/GameDBAddressablesIntegrationTests";
        private const string ConfigFolder = TestRoot + "/Config";
        private const string AssetPath = TestRoot + "/Sword.prefab";
        private const string DatabasePath = TestRoot + "/main.json";
        private const string SettingsName = "GameDBAddressablesIntegrationSettings";
        private const string CustomAddress = "custom-visible-address-not-used-by-gamedb";
        private const string DatabaseAddress = "gamedb-main-json";

        [UnityTest]
        [Explicit("Builds content and initializes the global Addressables runtime; run in an isolated filtered Unity process.")]
        public IEnumerator PublicAdapterLoadsBuiltContentByGuidDespiteCustomAddress()
        {
            AddressableAssetLease<GameObject> lease = null;
            var previousSettings = AddressableAssetSettingsDefaultObject.Settings;
            var defaultFolderExisted = AssetDatabase.IsValidFolder(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            const string noRuntimeDataPath = "__GameDB_NoAddressablesRuntimeDataPath__";
            var previousRuntimeDataPath = SessionState.GetString(
                Addressables.kAddressablesRuntimeDataPath,
                noRuntimeDataPath);
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CreateFolder(TestRoot);
                CreateFolder(ConfigFolder);
                CreateFolder(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);

                var source = new GameObject("Addressable Sword");
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(source, AssetPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }

                File.WriteAllText(DatabasePath,
                    "{\"tables\":{\"Items\":{\"Sword\":{}}}}");
                AssetDatabase.ImportAsset(DatabasePath, ImportAssetOptions.ForceSynchronousImport);

                var guid = AssetDatabase.AssetPathToGUID(AssetPath);
                var databaseGuid = AssetDatabase.AssetPathToGUID(DatabasePath);
                Assert.That(guid, Has.Length.EqualTo(32));
                Assert.That(databaseGuid, Has.Length.EqualTo(32));

                var settings = AddressableAssetSettings.Create(
                    ConfigFolder, SettingsName, true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                var group = settings.CreateGroup(
                    "GameDB Integration Group",
                    true,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema));
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                schema.IncludeGUIDInCatalog = true;
                schema.IncludeAddressInCatalog = true;

                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = CustomAddress;
                var databaseEntry = settings.CreateOrMoveEntry(databaseGuid, group);
                databaseEntry.address = DatabaseAddress;
                settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.EntryModified,
                    entry,
                    true,
                    true);
                AssetDatabase.SaveAssets();

                AddressableAssetSettings.BuildPlayerContent(
                    out AddressablesPlayerBuildResult buildResult);
                Assert.That(buildResult.Error, Is.Null.Or.Empty);
                Assert.That(buildResult.OutputPath, Is.Not.Null.And.Not.Empty);
                SessionState.SetString(
                    Addressables.kAddressablesRuntimeDataPath,
                    buildResult.OutputPath);
                var initialization = Addressables.InitializeAsync(false);
                initialization.WaitForCompletion();
                Assert.That(initialization.Status,
                    Is.EqualTo(AsyncOperationStatus.Succeeded),
                    initialization.OperationException?.ToString());
                Addressables.Release(initialization);

                var reference = new UnityObjectReference(guid, AssetPath);
                Exception exception = null;
                yield return Await(
                    reference.LoadAddressableAsync<GameObject>(),
                    "GUID-backed prefab",
                    value => lease = value,
                    value => exception = value);

                Assert.That(exception, Is.Null);
                Assert.That(lease.Asset, Is.Not.Null);
                Assert.That(lease.Asset.name, Is.EqualTo("Sword"));
                Assert.That(entry.address, Is.EqualTo(CustomAddress));

                string databaseJson = null;
                Exception databaseException = null;
                yield return Await(
                    AddressablesGameDBDataLoader.Instance.LoadAsync(DatabaseAddress),
                    "database JSON",
                    value => databaseJson = value,
                    value => databaseException = value);

                Assert.That(databaseException, Is.Null);
                Assert.That(databaseJson,
                    Is.EqualTo("{\"tables\":{\"Items\":{\"Sword\":{}}}}"));
                Assert.That(databaseEntry.address, Is.EqualTo(DatabaseAddress));
                Assert.That(lease.Asset, Is.Not.Null);
                Assert.That(lease.Asset.name, Is.EqualTo("Sword"));
            }
            finally
            {
                lease?.Dispose();
                AddressableAssetSettings.CleanPlayerContent();
                AddressableAssetSettingsDefaultObject.Settings = previousSettings;
                if (previousSettings == null)
                {
                    EditorBuildSettings.RemoveConfigObject(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
                }

                AssetDatabase.DeleteAsset(TestRoot);
                if (!defaultFolderExisted)
                {
                    AssetDatabase.DeleteAsset(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
                }

                AssetDatabase.Refresh();
                if (previousRuntimeDataPath == noRuntimeDataPath)
                {
                    SessionState.EraseString(
                        Addressables.kAddressablesRuntimeDataPath);
                }
                else
                {
                    SessionState.SetString(
                        Addressables.kAddressablesRuntimeDataPath,
                        previousRuntimeDataPath);
                }

                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        private static IEnumerator Await<T>(Awaitable<T> awaitable,
            string operation, Action<T> onSuccess, Action<Exception> onFailure)
        {
            var awaiter = awaitable.GetAwaiter();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!awaiter.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(180))
            {
                yield return null;
            }

            Assert.That(awaiter.IsCompleted, Is.True,
                $"Addressables {operation} load did not complete within 180 seconds.");
            try
            {
                onSuccess(awaiter.GetResult());
            }
            catch (Exception exception)
            {
                onFailure(exception);
            }
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
