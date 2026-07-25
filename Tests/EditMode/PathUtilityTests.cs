using GameDBEditorLibrary;
using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace GameDBLibrary.Tests
{
    public class PathUtilityTests
    {
        [Test]
        public void GetRelativeDataPath_ReturnsAssetsRelativePath()
        {
            var path = Path.Combine(Application.dataPath, "GameData", "database.json");

            Assert.That(GameDBEditorLibrary.Utils.GetRelativeDataPath(path), Is.EqualTo("GameData/database.json"));
        }

        [Test]
        public void GetRelativeDataPath_ReturnsDotForAssetsRoot()
        {
            Assert.That(GameDBEditorLibrary.Utils.GetRelativeDataPath(Application.dataPath), Is.EqualTo("."));
        }

        [Test]
        public void GetRelativeDataPath_RejectsPathsOutsideAssets()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Outside", "database.json"));

            Assert.That(GameDBEditorLibrary.Utils.GetRelativeDataPath(path), Is.Null);
        }

        [Test]
        public void GetRelativeDataPath_RejectsEmptyPaths()
        {
            Assert.That(GameDBEditorLibrary.Utils.GetRelativeDataPath(string.Empty), Is.Null);
        }
    }
}
