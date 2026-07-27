using GameDBEditorLibrary;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDBLibrary.Tests
{
    internal class CodeGenerationCompilationTests
    {
        private const string GeneratedScope = "CompilationCoverage";
        private const string LocalizationScope = "LocalizationCompilationCoverage";

        [SerializeField]
        private string m_assetFolderName;

        [SerializeField]
        private string m_generatedDataJson;

        [SerializeField]
        private string m_localizationDataJson;

        [UnityTest]
        public IEnumerator ExportedCodeCompilesAndRegenerationRemovesStaleTableTypes()
        {
            m_assetFolderName = $"GameDBCompilationTests_{Guid.NewGuid():N}";
            var exportRoot = GetExportRoot();
            Directory.CreateDirectory(exportRoot);

            var representativeDatabase = CreateRepresentativeDatabase(true);
            var localizationDatabase = CreateLocalizationDatabase();
            m_generatedDataJson = representativeDatabase.SerializeData();
            m_localizationDataJson = localizationDatabase.SerializeData();
            new CSharpExporter().Export(exportRoot, representativeDatabase, true);
            new CSharpExporter().Export(exportRoot, localizationDatabase, true);

            yield return new RecompileScripts(true, true);

            var assembly = GetGeneratedAssembly();
            AssertGeneratedMembers(assembly);
            yield return AssertGeneratedAsyncLoadBehavior(assembly);
            Assert.That(assembly.GetType($"GameDB{GeneratedScope}.Obsolete", false), Is.Not.Null);
            Assert.That(assembly.GetType($"GameDB{LocalizationScope}.Translations", false), Is.Not.Null);

            new CSharpExporter().Export(GetExportRoot(), CreateRepresentativeDatabase(false), true);

            yield return new RecompileScripts(true, true);

            assembly = GetGeneratedAssembly();
            AssertGeneratedMembers(assembly);
            Assert.That(assembly.GetType($"GameDB{GeneratedScope}.Obsolete", false), Is.Null);
            Assert.That(File.Exists(Path.Combine(GetExportRoot(), GeneratedScope, "Obsolete.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(GetExportRoot(), GeneratedScope, "ObsoleteSchema.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(GetExportRoot(), GeneratedScope, "ObsoleteTable.cs")), Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (string.IsNullOrEmpty(m_assetFolderName))
            {
                yield break;
            }

            var generatedTypesAreLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name == "Assembly-CSharp")
                .Any(assembly => assembly.GetType($"GameDB{GeneratedScope}.GameDB", false) != null
                    || assembly.GetType($"GameDB{LocalizationScope}.GameDB", false) != null);
            var assetPath = $"Assets/{m_assetFolderName}";
            var deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted && Directory.Exists(GetExportRoot()))
            {
                Directory.Delete(GetExportRoot(), true);
            }

            m_assetFolderName = null;
            m_generatedDataJson = null;
            m_localizationDataJson = null;
            if (generatedTypesAreLoaded)
            {
                yield return new RecompileScripts(true, true);
            }
        }

        private static GameDB CreateRepresentativeDatabase(bool includeObsoleteTable)
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory($"CodeGenerationCompilationTests_{Guid.NewGuid():N}/database.json");
            gameDB.ScopeName = GeneratedScope;

            Assert.That(gameDB.AddTable("Categories", KeyType.@string), Is.True);
            var categories = (TableModel)gameDB.Tables["Categories"];
            Assert.That(categories.AddField("DisplayName", FieldType.@string, false), Is.True);
            Assert.That(categories.AddKey("Weapons"), Is.True);

            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Power", FieldType.@int, false), Is.True);
            Assert.That(items.AddField("LongValue", FieldType.@long, false), Is.True);
            Assert.That(items.AddField("LongValues", FieldType.@long, true), Is.True);
            Assert.That(items.AddField("LongByKey", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.@long, null)), Is.True);
            Assert.That(items.AddField("DoubleValue", FieldType.@double, false), Is.True);
            Assert.That(items.AddField("DoubleValues", FieldType.@double, true), Is.True);
            Assert.That(items.AddField("DoubleByKey", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.@double, null)), Is.True);
            Assert.That(items.AddField("Attributes", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.@int, null)), Is.True);
            Assert.That(items.AddField("Category", FieldType.tableRef, false, "Categories"), Is.True);
            Assert.That(items.AddField("Icon", FieldType.unityObject, false), Is.True);
            Assert.That(items.AddField("Icons", FieldType.unityObject, true), Is.True);
            Assert.That(items.AddField("IconsBySlot", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.unityObject, null)), Is.True);
            Assert.That(items.AddKey("Sword"), Is.True);
            Assert.That(items.SetValue("Sword", "Power", 12L), Is.True);

            if (includeObsoleteTable)
            {
                Assert.That(gameDB.AddTable("Obsolete", KeyType.@string), Is.True);
                var obsolete = (TableModel)gameDB.Tables["Obsolete"];
                Assert.That(obsolete.AddField("Value", FieldType.@string, false), Is.True);
                Assert.That(obsolete.AddKey("Legacy"), Is.True);
            }

            return gameDB;
        }

        private static GameDB CreateLocalizationDatabase()
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory($"CodeGenerationLocalizationCompilationTests_{Guid.NewGuid():N}/database.json");
            gameDB.ScopeName = LocalizationScope;
            gameDB.LocalizationDB = true;

            Assert.That(gameDB.AddTable("Translations", KeyType.@string), Is.True);
            var translations = (TableModel)gameDB.Tables["Translations"];
            Assert.That(translations.AddField("English", FieldType.@string, false), Is.True);
            Assert.That(translations.AddField("French", FieldType.@string, false), Is.True);
            Assert.That(translations.AddKey("Greeting"), Is.True);
            Assert.That(translations.SetValue("Greeting", "English", "Hello"), Is.True);
            Assert.That(translations.SetValue("Greeting", "French", "Bonjour"), Is.True);

            return gameDB;
        }

        private static Assembly GetGeneratedAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => assembly.GetName().Name == "Assembly-CSharp");
        }

        private static void AssertGeneratedMembers(Assembly assembly)
        {
            var gameDBType = RequireType(assembly, $"GameDB{GeneratedScope}.GameDB");
            var itemsType = RequireType(assembly, $"GameDB{GeneratedScope}.Items");
            var itemsTableType = RequireType(assembly, $"GameDB{GeneratedScope}.ItemsTable");
            var localizationGameDBType = RequireType(assembly, $"GameDB{LocalizationScope}.GameDB");
            var translationsType = RequireType(assembly, $"GameDB{LocalizationScope}.Translations");

            Assert.That(gameDBType.GetProperty("ItemsTable", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            AssertAsyncLoadMethods(gameDBType, false);
            AssertAsyncLoadMethods(localizationGameDBType, true);
            Assert.That(itemsTableType, Is.Not.Null);
            Assert.That(itemsType.GetProperty("PowerVal", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            AssertPropertyType(itemsType, "LongValueVal", typeof(long));
            AssertPropertyType(itemsType, "LongValuesVal",
                typeof(System.Collections.Generic.List<long>));
            AssertPropertyType(itemsType, "LongByKeyVal",
                typeof(System.Collections.Generic.Dictionary<string, long>));
            AssertPropertyType(itemsType, "DoubleValueVal", typeof(double));
            AssertPropertyType(itemsType, "DoubleValuesVal",
                typeof(System.Collections.Generic.List<double>));
            AssertPropertyType(itemsType, "DoubleByKeyVal",
                typeof(System.Collections.Generic.Dictionary<string, double>));
            Assert.That(itemsType.GetProperty("AttributesVal", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(itemsType.GetProperty("CategoryVal", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            AssertPropertyType(itemsType, "IconVal", typeof(UnityObjectReference));
            AssertPropertyType(itemsType, "IconGuidVal", typeof(string));
            AssertPropertyType(itemsType, "IconPathVal", typeof(string));
            AssertPropertyType(itemsType, "IconObjectVal", typeof(UnityEngine.Object));
            AssertPropertyType(itemsType, "IconsVal", typeof(System.Collections.Generic.List<UnityObjectReference>));
            AssertPropertyType(itemsType, "IconsGuidVal", typeof(System.Collections.Generic.List<string>));
            AssertPropertyType(itemsType, "IconsPathVal", typeof(System.Collections.Generic.List<string>));
            AssertPropertyType(itemsType, "IconsObjectVal", typeof(System.Collections.Generic.List<UnityEngine.Object>));
            AssertPropertyType(itemsType, "IconsBySlotVal",
                typeof(System.Collections.Generic.Dictionary<string, GameDBLibraryUnity.UnityObjectAccessor>));
            Assert.That(localizationGameDBType.GetProperty("LocalizationLanguage", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(translationsType.GetProperty("TranslatedVal", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(translationsType.GetProperty("LanguageVal", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
        }

        private IEnumerator AssertGeneratedAsyncLoadBehavior(Assembly assembly)
        {
            var gameDBType = RequireType(assembly, $"GameDB{GeneratedScope}.GameDB");
            var gameDB = Activator.CreateInstance(gameDBType, "GeneratedBehavior");
            var notifications = 0;
            ((GameDBBase)gameDB).OnDBLoaded += () => notifications++;

            var normalAwaitable = InvokeCustomLoader(gameDBType, gameDB,
                new InlineLoader(m_generatedDataJson), "normal", false);
            yield return Await(normalAwaitable);

            var itemsTable = gameDBType.GetProperty("ItemsTable").GetValue(gameDB);
            var sword = itemsTable.GetType().GetMethod("GetByKey")
                .Invoke(itemsTable, new object[] { "Sword" });
            Assert.That(sword.GetType().GetProperty("PowerVal").GetValue(sword),
                Is.EqualTo(12));
            Assert.That(notifications, Is.Zero);

            var localizationType = RequireType(assembly,
                $"GameDB{LocalizationScope}.GameDB");
            var localization = Activator.CreateInstance(localizationType,
                "LocalizationBehavior");
            string languageAtNotification = null;
            string translationAtNotification = null;
            ((GameDBBase)localization).OnDBLoaded += () =>
            {
                languageAtNotification = (string)localizationType
                    .GetProperty("LocalizationLanguage").GetValue(localization);
                translationAtNotification = GetTranslation(localizationType,
                    localization, "TranslatedVal");
            };

            var localizationAwaitable = InvokeCustomLoader(localizationType,
                localization, new InlineLoader(m_localizationDataJson),
                "localized", true, "French");
            yield return Await(localizationAwaitable);

            Assert.That(localizationType.GetProperty("LocalizationLanguage")
                .GetValue(localization), Is.EqualTo("French"));
            Assert.That(GetTranslation(localizationType, localization,
                "TranslatedVal"), Is.EqualTo("Bonjour"));
            Assert.That(languageAtNotification, Is.EqualTo("French"));
            Assert.That(translationAtNotification, Is.EqualTo("Bonjour"));
        }

        private static Awaitable InvokeCustomLoader(Type gameDBType, object gameDB,
            IGameDBDataLoader loader, string location, bool notify,
            string language = null)
        {
            var method = gameDBType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(candidate => candidate.Name == "LoadAsync"
                    && candidate.GetParameters().Any(parameter =>
                        parameter.ParameterType == typeof(IGameDBDataLoader)));
            var arguments = language == null
                ? new object[] { location, loader, notify, default(CancellationToken) }
                : new object[] { location, language, loader, notify,
                    default(CancellationToken) };
            return (Awaitable)method.Invoke(gameDB, arguments);
        }

        private static string GetTranslation(Type gameDBType, object gameDB,
            string propertyName)
        {
            var table = gameDBType.GetProperty("TranslationsTable")
                .GetValue(gameDB);
            var greeting = table.GetType().GetMethod("GetByKey")
                .Invoke(table, new object[] { "Greeting" });
            return (string)greeting.GetType().GetProperty(propertyName)
                .GetValue(greeting);
        }

        private static IEnumerator Await(Awaitable awaitable)
        {
            var awaiter = awaitable.GetAwaiter();
            for (var frame = 0; !awaiter.IsCompleted && frame < 300; frame++)
            {
                yield return null;
            }

            Assert.That(awaiter.IsCompleted, Is.True,
                "Generated async load timed out after 300 frames.");
            awaiter.GetResult();
        }

        private static void AssertAsyncLoadMethods(Type gameDBType, bool localization)
        {
            var methods = gameDBType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "LoadAsync")
                .ToArray();

            Assert.That(methods, Has.Length.EqualTo(2));
            Assert.That(methods.All(method => method.ReturnType == typeof(Awaitable)), Is.True);
            Assert.That(methods.Any(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(IGameDBDataLoader))), Is.True);
            Assert.That(methods.All(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(System.Threading.CancellationToken))), Is.True);
            Assert.That(methods.All(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(string))), Is.True);
            Assert.That(methods.All(method => method.GetParameters().Count(parameter =>
                parameter.ParameterType == typeof(string)) == (localization ? 2 : 1)), Is.True);
        }

        private static void AssertPropertyType(Type type, string propertyName,
            Type expectedType)
        {
            var property = type.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected property '{type.FullName}.{propertyName}'.");
            Assert.That(property.PropertyType, Is.EqualTo(expectedType));
        }

        private static Type RequireType(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName, false);
            Assert.That(type, Is.Not.Null, $"Expected generated type '{typeName}' in Assembly-CSharp.");
            return type;
        }

        private sealed class InlineLoader : IGameDBDataLoader
        {
            private readonly string m_json;

            internal InlineLoader(string json)
            {
                m_json = json;
            }

            public Awaitable<string> LoadAsync(string location,
                CancellationToken cancellationToken = default)
            {
                var completion = new AwaitableCompletionSource<string>();
                completion.SetResult(m_json);
                return completion.Awaitable;
            }
        }

        private string GetExportRoot()
        {
            Assert.That(m_assetFolderName, Is.Not.Null.And.Not.Empty);
            return Path.Combine(Application.dataPath, m_assetFolderName);
        }
    }
}
