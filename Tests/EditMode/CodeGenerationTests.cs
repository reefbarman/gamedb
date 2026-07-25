using GameDBEditorLibrary;
using NUnit.Framework;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class CodeGenerationTests
    {
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

        [TestCase(false, "public Exception Load(string path, bool notify = true)")]
        [TestCase(true, "public Exception Load(string path, string language, bool notify = true)")]
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
    }
}
