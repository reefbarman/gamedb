using GameDBEditorLibrary;
using GameDBEditorLibrary.Automation;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class CodeGenerationTests
    {
        public enum GeneratedEnumKey
        {
            Normal,
            @class
        }

        public class GenericEnumContainer<T>
        {
            public enum NestedEnum
            {
                Value
            }
        }

        [Test]
        public void Export_RoundTripsDictionaryFieldSchema()
        {
            var id = Guid.NewGuid().ToString("N");
            var assetFolderName = $"GameDBTests_{id}";
            var assetFolderPath = $"Assets/{assetFolderName}";
            var assetFolderAbsolutePath = Path.Combine(Application.dataPath, assetFolderName);
            var outputPath = Path.Combine(Path.GetTempPath(), assetFolderName);

            Directory.CreateDirectory(assetFolderAbsolutePath);
            Directory.CreateDirectory(outputPath);

            try
            {
                var gameDB = GameDB.Instance;
                var databasePath = $"{assetFolderName}/database.json";
                gameDB.Create(databasePath);
                gameDB.ScopeName = "DictionaryTest";
                Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);

                var table = (TableModel)gameDB.Tables["Items"];
                var dictionaryType = new DictionaryType(KeyType.@string, null, FieldType.@int, null);
                Assert.That(table.AddField("Attributes", FieldType.dictionary, false, dictionaryType), Is.True);
                Assert.That(gameDB.Save(), Is.True);
                Assert.That(gameDB.Load(databasePath), Is.True);

                new CSharpExporter().Export(outputPath, gameDB, true);

                var generatedCode = File.ReadAllText(Path.Combine(outputPath, "DictionaryTest", "Items.cs"));
                Assert.That(generatedCode, Does.Contain("Dictionary<"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolderPath);
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
            }
        }

        [Test]
        public void Export_GeneratesImmutablePartialTypesAndEscapedKeys()
        {
            var gameDB = CreateInMemoryDatabase("HardenedOutput");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var table = (TableModel)gameDB.Tables["Items"];
            Assert.That(table.AddField("DisplayName", FieldType.@string, false), Is.True);
            Assert.That(table.AddKey("Line\nBreak\tKey"), Is.True);

            var outputPath = CreateOutputPath();
            try
            {
                new CSharpExporter().Export(outputPath, gameDB, true);
                var scopePath = Path.Combine(outputPath, gameDB.ScopeName);
                var gameDBCode = File.ReadAllText(Path.Combine(scopePath, "GameDB.cs"));
                var rowCode = File.ReadAllText(Path.Combine(scopePath, "Items.cs"));
                var schemaCode = File.ReadAllText(Path.Combine(scopePath, "ItemsSchema.cs"));
                var tableCode = File.ReadAllText(Path.Combine(scopePath, "ItemsTable.cs"));

                Assert.That(gameDBCode, Does.Contain("public partial class GameDB"));
                Assert.That(rowCode, Does.Contain("public partial class Items"));
                Assert.That(tableCode, Does.Contain("public partial class ItemsTable"));
                Assert.That(schemaCode, Does.Contain("public const string TableName"));
                Assert.That(schemaCode, Does.Contain("public const string FieldDisplayName"));
                Assert.That(schemaCode, Does.Contain("public const string KeyLineBreakKey = \"Line\\nBreak\\tKey\";"));
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void Validate_AggregatesGeneratedNameCollisionsAndWritesNothing()
        {
            var gameDB = CreateInMemoryDatabase("class");
            Assert.That(gameDB.AddTable("GameDB", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("ItemsSchema", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("items", KeyType.@string), Is.True);

            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Icon", FieldType.unityObject, false), Is.True);
            Assert.That(items.AddField("IconPath", FieldType.@string, false), Is.True);
            Assert.That(items.AddKey("Iron Sword"), Is.True);
            Assert.That(items.AddKey("IronSword"), Is.True);
            Assert.That(items.AddKey("A-B"), Is.True);

            var issues = CSharpExporter.Validate(gameDB, true);
            var outputPath = CreateOutputPath();
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => new CSharpExporter().Export(outputPath, gameDB, true));

                Assert.That(issues.Select(issue => issue.Code), Does.Contain("scope.identifier.invalid"));
                Assert.That(issues.Select(issue => issue.Code), Does.Contain("type.name.collision"));
                Assert.That(issues.Select(issue => issue.Code), Does.Contain("file.name.collision"));
                Assert.That(issues.Select(issue => issue.Code), Does.Contain("member.name.collision"));
                Assert.That(issues.Select(issue => issue.Code), Does.Contain("row.identifier.collision"));
                Assert.That(issues.Select(issue => issue.Code), Does.Contain("row.identifier.invalid"));
                Assert.That(exception.Message, Does.Contain("C# generation validation failed"));
                Assert.That(Directory.Exists(Path.Combine(outputPath, gameDB.ScopeName)), Is.False);
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void Validate_RejectsAccessorAndContainingTypeCollisions()
        {
            var gameDB = CreateInMemoryDatabase("AccessorCollisions");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("IconPathVal", KeyType.@string), Is.True);

            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Icon", FieldType.unityObject, false), Is.True);
            Assert.That(items.AddField("IconPath", FieldType.@string, false), Is.True);
            Assert.That(items.AddField("IconObject", FieldType.@string, false), Is.True);

            var containingType = (TableModel)gameDB.Tables["IconPathVal"];
            Assert.That(containingType.AddField("Icon", FieldType.unityObject, false), Is.True);

            var issues = CSharpExporter.Validate(gameDB, true);

            Assert.That(issues.Count(issue => issue.Code == "member.name.collision"), Is.GreaterThanOrEqualTo(3));
            Assert.That(issues.Any(issue => issue.Message.Contains("IconPathVal") && issue.TableName == "Items"), Is.True);
            Assert.That(issues.Any(issue => issue.Message.Contains("IconObjectVal") && issue.TableName == "Items"), Is.True);
            Assert.That(issues.Any(issue => issue.Message.Contains("containing row type") && issue.TableName == "IconPathVal"), Is.True);
        }

        [Test]
        public void Validate_RejectsUnknownEnumRowsAndEscapesKeywordMembers()
        {
            var gameDB = CreateInMemoryDatabase("EnumKeys");
            Assert.That(gameDB.AddTable("States", KeyType.@enum, typeof(GeneratedEnumKey)), Is.True);
            var table = (TableModel)gameDB.Tables["States"];
            Assert.That(table.AddKey("class"), Is.True);
            Assert.That(table.AddKey("Missing"), Is.True);

            var issues = CSharpExporter.Validate(gameDB, true);
            Assert.That(issues.Single(issue => issue.Code == "row.enumMember.invalid").RowKey, Is.EqualTo("Missing"));

            Assert.That(table.RemoveKey("Missing"), Is.True);
            var outputPath = CreateOutputPath();
            try
            {
                new CSharpExporter().Export(outputPath, gameDB, true);
                var schemaCode = File.ReadAllText(Path.Combine(outputPath, gameDB.ScopeName, "StatesSchema.cs"));
                Assert.That(schemaCode, Does.Contain("Keyclass = global::GameDBLibrary.Tests.CodeGenerationTests.GeneratedEnumKey.@class;"));
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void Validate_RejectsEnumTypesNestedInGenericTypes()
        {
            var enumType = typeof(GenericEnumContainer<int>.NestedEnum);
            var gameDB = CreateInMemoryDatabase("GenericEnumTypes");
            Assert.That(gameDB.AddTable("EnumRows", KeyType.@enum, enumType), Is.True);
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("State", FieldType.@enum, false, enumType), Is.True);
            Assert.That(items.AddField("Lookup", FieldType.dictionary, false,
                new DictionaryType(KeyType.@enum, enumType, FieldType.@enum, enumType)), Is.True);

            var issues = CSharpExporter.Validate(gameDB, true);

            Assert.That(issues.Select(issue => issue.Code), Does.Contain("table.enumType.invalid"));
            Assert.That(issues.Select(issue => issue.Code), Does.Contain("field.enumType.invalid"));
            Assert.That(issues.Select(issue => issue.Code), Does.Contain("field.dictionaryKeyEnumType.invalid"));
            Assert.That(issues.Select(issue => issue.Code), Does.Contain("field.dictionaryValueEnumType.invalid"));
            Assert.That(issues.All(issue => issue.Message.Contains("cannot be emitted as a C# type reference")), Is.True);
        }

        [Test]
        public void Validate_RejectsMissingTableReferenceTargetsBeforeWriting()
        {
            var gameDB = CreateInMemoryDatabase("MissingReferences");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)gameDB.Tables["Items"];
            Assert.That(items.AddField("Direct", FieldType.tableRef, false, "MissingDirect"), Is.True);
            Assert.That(items.AddField("Dictionary", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.tableRef, "MissingDictionary")), Is.True);

            var issues = CSharpExporter.Validate(gameDB, true);
            var outputPath = CreateOutputPath();
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => new CSharpExporter().Export(outputPath, gameDB, true));

                Assert.That(issues.Count(issue => issue.Code == "tableRef.table.missing"), Is.EqualTo(2));
                Assert.That(issues.Select(issue => issue.FieldName), Is.EquivalentTo(new[] { "Direct", "Dictionary" }));
                Assert.That(exception.Message, Does.Contain("MissingDirect"));
                Assert.That(exception.Message, Does.Contain("MissingDictionary"));
                Assert.That(Directory.Exists(Path.Combine(outputPath, gameDB.ScopeName)), Is.False);
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void Export_RejectsCaseOnlyScopeDirectoryConflict()
        {
            var gameDB = CreateInMemoryDatabase("CaseScope");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            var outputPath = CreateOutputPath();
            Directory.CreateDirectory(Path.Combine(outputPath, "casescope"));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => new CSharpExporter().Export(outputPath, gameDB, true));
                Assert.That(exception.Message, Does.Contain("conflicts with existing directory"));
                Assert.That(Directory.GetFiles(Path.Combine(outputPath, "casescope"), "*.cs"), Is.Empty);
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void Export_GeneratesQualifiedLocalizationAndReferenceOutput()
        {
            var gameDB = CreateInMemoryDatabase("ReferenceOutput");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Recipes", KeyType.@string), Is.True);
            var recipes = (TableModel)gameDB.Tables["Recipes"];
            Assert.That(recipes.AddField("Result", FieldType.tableRef, false, "Items"), Is.True);
            Assert.That(recipes.AddField("Rewards", FieldType.dictionary, false,
                new DictionaryType(KeyType.@string, null, FieldType.tableRef, "Items")), Is.True);

            var outputPath = CreateOutputPath();
            try
            {
                new CSharpExporter().Export(outputPath, gameDB, true);
                var recipesCode = File.ReadAllText(Path.Combine(outputPath, gameDB.ScopeName, "Recipes.cs"));
                var tableCode = File.ReadAllText(Path.Combine(outputPath, gameDB.ScopeName, "RecipesTable.cs"));
                Assert.That(recipesCode, Does.Contain("global::GameDBLibrary.TableReferenceAccessor<string, global::GameDBReferenceOutput.Items>"));
                Assert.That(recipesCode, Does.Contain("global::GameDBLibrary.DictionaryAccessor<string, global::GameDBLibrary.TableReferenceAccessor<string, global::GameDBReferenceOutput.Items>>"));
                Assert.That(tableCode, Does.Contain("new global::GameDBLibrary.DictionaryType"));
            }
            finally
            {
                DeleteDirectory(outputPath);
            }

            var localization = CreateInMemoryDatabase("LocalizationOutput");
            localization.LocalizationDB = true;
            Assert.That(localization.AddTable("Lines", KeyType.@string), Is.True);
            var lines = (TableModel)localization.Tables["Lines"];
            Assert.That(lines.AddField("English", FieldType.@string, false), Is.True);
            outputPath = CreateOutputPath();
            try
            {
                new CSharpExporter().Export(outputPath, localization, true);
                var gameDBCode = File.ReadAllText(Path.Combine(outputPath, localization.ScopeName, "GameDB.cs"));
                var linesCode = File.ReadAllText(Path.Combine(outputPath, localization.ScopeName, "Lines.cs"));
                Assert.That(gameDBCode, Does.Contain("public global::System.Exception Load(string path, string language"));
                Assert.That(linesCode, Does.Contain("public string TranslatedVal"));
                Assert.That(linesCode, Does.Contain("global::System.Convert.ChangeType"));
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [Test]
        public void GenerateCSharp_DryRunReportsExporterValidationIssuesWithoutWriting()
        {
            var id = Guid.NewGuid().ToString("N");
            var assetFolderName = $"GameDBTests_{id}";
            var assetFolderPath = $"Assets/{assetFolderName}";
            var assetFolderAbsolutePath = Path.Combine(Application.dataPath, assetFolderName);
            Directory.CreateDirectory(assetFolderAbsolutePath);

            try
            {
                var gameDB = GameDB.Instance;
                var databasePath = $"{assetFolderName}/database.json";
                gameDB.Create(databasePath);
                gameDB.ScopeName = "AutomationCodegen";
                Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
                var items = (TableModel)gameDB.Tables["Items"];
                Assert.That(items.AddField("Icon", FieldType.unityObject, false), Is.True);
                Assert.That(items.AddField("IconPath", FieldType.@string, false), Is.True);
                Assert.That(gameDB.Save(), Is.True);

                var outputAssetPath = $"{assetFolderPath}/Generated";
                var result = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
                {
                    DatabasePath = $"{assetFolderPath}/database.json",
                    OutputDirectory = outputAssetPath,
                    Options = new GameDBOperationOptions { DryRun = true }
                });

                Assert.That(result.Success, Is.False);
                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("member.name.collision"));
                Assert.That(Directory.Exists(Path.Combine(assetFolderAbsolutePath, "Generated")), Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolderPath);
            }
        }

        [Test]
        public void Export_ReplacesScopeAndPreservesCurrentMetadata()
        {
            var gameDB = CreateInMemoryDatabase("ReplacementTest");
            Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
            Assert.That(gameDB.AddTable("Obsolete", KeyType.@string), Is.True);

            var outputPath = CreateOutputPath();
            try
            {
                var exporter = new CSharpExporter();
                exporter.Export(outputPath, gameDB, true);
                var scopePath = Path.Combine(outputPath, gameDB.ScopeName);
                File.WriteAllText(Path.Combine(scopePath, "Items.cs.meta"), "items-guid");
                File.WriteAllText(Path.Combine(scopePath, "Obsolete.cs.meta"), "obsolete-guid");
                File.WriteAllText(Path.Combine(scopePath, "Sentinel.txt"), "stale");

                Assert.That(gameDB.RemoveTable("Obsolete"), Is.True);
                exporter.Export(outputPath, gameDB, true);

                Assert.That(Directory.GetFiles(scopePath, "*.cs").Select(Path.GetFileName), Is.EquivalentTo(new[]
                {
                    "GameDB.cs",
                    "Items.cs",
                    "ItemsSchema.cs",
                    "ItemsTable.cs"
                }));
                Assert.That(File.ReadAllText(Path.Combine(scopePath, "Items.cs.meta")), Is.EqualTo("items-guid"));
                Assert.That(File.Exists(Path.Combine(scopePath, "Obsolete.cs")), Is.False);
                Assert.That(File.Exists(Path.Combine(scopePath, "Obsolete.cs.meta")), Is.False);
                Assert.That(File.Exists(Path.Combine(scopePath, "Sentinel.txt")), Is.False);
                Assert.That(Directory.GetDirectories(outputPath, ".ReplacementTest.*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                DeleteDirectory(outputPath);
            }
        }

        [TestCase(false, "public global::System.Exception Load(string path, bool notify = true)")]
        [TestCase(true, "public global::System.Exception Load(string path, string language, bool notify = true)")]
        public void Export_GeneratesJsonOnlyUnityLoader(bool localization, string expectedLoader)
        {
            var id = Guid.NewGuid().ToString("N");
            var assetFolderName = $"GameDBTests_{id}";
            var assetFolderPath = $"Assets/{assetFolderName}";
            var assetFolderAbsolutePath = Path.Combine(Application.dataPath, assetFolderName);
            var outputPath = Path.Combine(Path.GetTempPath(), assetFolderName);

            Directory.CreateDirectory(assetFolderAbsolutePath);
            Directory.CreateDirectory(outputPath);

            try
            {
                var gameDB = GameDB.Instance;
                gameDB.Create($"{assetFolderName}/database.json");
                gameDB.ScopeName = "GeneratedTest";
                gameDB.LocalizationDB = localization;
                Assert.That(gameDB.AddTable("Items", KeyType.@string), Is.True);
                Assert.That(gameDB.Save(), Is.True);

                new CSharpExporter().Export(outputPath, gameDB, true);

                var generatedCode = File.ReadAllText(Path.Combine(outputPath, "GeneratedTest", "GameDB.cs"));
                Assert.That(generatedCode, Does.Contain(expectedLoader));
                Assert.That(generatedCode, Does.Contain("gameDBResource.text"));
                Assert.That(generatedCode, Does.Not.Contain("BinaryGameDB"));
                Assert.That(generatedCode, Does.Not.Contain("WebRequestHelper.Request"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolderPath);
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
            }
        }

        private static GameDB CreateInMemoryDatabase(string scopeName)
        {
            var gameDB = new GameDB();
            gameDB.CreateInMemory($"CodeGenerationTests_{Guid.NewGuid():N}/database.json");
            gameDB.ScopeName = scopeName;
            return gameDB;
        }

        private static string CreateOutputPath()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"GameDBCodeGenerationTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
