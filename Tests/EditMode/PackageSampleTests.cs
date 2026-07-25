using GameDBEditorLibrary;
using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;

namespace GameDBLibrary.Tests
{
    public class PackageSampleTests
    {
        [Test]
        public void BasicSample_LoadsAndGeneratesClasses()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(GameDB).Assembly);
            Assert.That(packageInfo, Is.Not.Null);

            var sampleDirectory = Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic", "Resources", "GameDBs");
            var dataJson = File.ReadAllText(Path.Combine(sampleDirectory, "basic.json"));
            var schemaJson = File.ReadAllText(Path.Combine(sampleDirectory, "basic.schema.json"));
            var outputDirectory = Path.Combine(Path.GetTempPath(), "GameDBSampleTests_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                var gameDB = new GameDB();
                Assert.That(gameDB.Import(dataJson, schemaJson), Is.True);
                Assert.That(gameDB.ScopeName, Is.EqualTo("Basic"));
                Assert.That(gameDB.Tables.Keys, Is.EquivalentTo(new[] { "Categories", "Items" }));

                var items = (TableModel)gameDB.Tables["Items"];
                var sword = items.Data["Sword"];
                Assert.That(sword.GetValue("DisplayName"), Is.EqualTo("Iron Sword"));
                Assert.That(sword.GetValue("Damage"), Is.EqualTo(12L));
                Assert.That(sword.GetValue("Category"), Is.EqualTo("Weapons"));

                new CSharpExporter().Export(outputDirectory, gameDB, true);

                var generatedDirectory = Path.Combine(outputDirectory, "Basic");
                Assert.That(Directory.GetFiles(generatedDirectory, "*.cs").Select(Path.GetFileName),
                    Is.EquivalentTo(new[]
                    {
                        "Categories.cs",
                        "CategoriesSchema.cs",
                        "CategoriesTable.cs",
                        "Items.cs",
                        "ItemsSchema.cs",
                        "ItemsTable.cs",
                        "GameDB.cs"
                    }));
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }
    }
}
