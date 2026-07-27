using NUnit.Framework;
using System;

namespace GameDBLibrary.Tests
{
    public class UnityObjectReferenceTests
    {
        private const string Guid = "0123456789abcdef0123456789abcdef";
        private const string OtherGuid = "fedcba9876543210fedcba9876543210";
        private const string Path = "Assets/Game/Resources/Items/Sword.asset";
        private const string OtherPath = "Assets/Game/Resources/Items/Shield.asset";
        private const string AddressablePath = "Assets/Game/Items/Sword.asset";

        [Test]
        public void UnityObjectReference_EqualityIncludesGuidAndPath()
        {
            var first = new UnityObjectReference(Guid, Path);
            var equivalent = new UnityObjectReference(Guid, Path);

            Assert.That(first, Is.EqualTo(equivalent));
            Assert.That(first == equivalent, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(new UnityObjectReference(Guid, OtherPath)));
            Assert.That(first, Is.Not.EqualTo(new UnityObjectReference(OtherGuid, Path)));
            Assert.That(first == null, Is.False);
            Assert.That(first != null, Is.True);
        }

        [Test]
        public void UnityObjectReference_EmptyIsCanonical()
        {
            Assert.That(UnityObjectReference.Empty.Guid, Is.Empty);
            Assert.That(UnityObjectReference.Empty.Path, Is.Empty);
            Assert.That(UnityObjectReference.Empty.IsEmpty, Is.True);
            Assert.That(new UnityObjectReference(string.Empty, string.Empty),
                Is.EqualTo(UnityObjectReference.Empty));
            Assert.Throws<ArgumentException>(() =>
                new UnityObjectReference(Guid, string.Empty));
            Assert.Throws<ArgumentException>(() =>
                new UnityObjectReference(string.Empty, Path));
        }

        [Test]
        public void CoreUnityObjectAccessor_ProjectsReferenceGuidAndPath()
        {
            var reference = new UnityObjectReference(Guid, Path);
            var accessor = new UnityObjectAccessor(reference);

            Assert.That(accessor.GetValue(), Is.SameAs(reference));
            Assert.That(accessor.GetGuid(), Is.EqualTo(Guid));
            Assert.That(accessor.GetPath(), Is.EqualTo(Path));
            Assert.Throws<ArgumentException>(() => new UnityObjectAccessor(Path));
            Assert.Throws<ArgumentException>(() => new UnityObjectAccessor(null));
        }

        [Test]
        public void UnityUnityObjectAccessor_EmptyReferenceReturnsNull()
        {
            var accessor = new GameDBLibraryUnity.UnityObjectAccessor(
                UnityObjectReference.Empty);

            Assert.That(accessor.GetValue(), Is.SameAs(UnityObjectReference.Empty));
            Assert.That(accessor.GetGuid(), Is.Empty);
            Assert.That(accessor.GetPath(), Is.Empty);
            Assert.That(accessor.GetObject(), Is.Null);
        }

        [TestCase(AddressablePath)]
        [TestCase("Assets/Game/resources/Items/Sword.asset")]
        [TestCase("Assets/Resources/Nested/Resources/Sword.asset")]
        public void UnityUnityObjectAccessor_NonResourcesReferenceRequiresAddressables(
            string path)
        {
            var reference = new UnityObjectReference(Guid, path);
            var accessor = new GameDBLibraryUnity.UnityObjectAccessor(reference);

            var exception = Assert.Throws<InvalidOperationException>(() => accessor.GetObject());

            Assert.That(exception.Message, Does.Contain("Addressables")
                .And.Contain("non-Resources"));
        }

        [Test]
        public void RuntimeImport_RejectsPathStringsAndMalformedObjectsWithoutPublishingRows()
        {
            var gameDB = new RuntimeTestGameDB();
            var canonicalJson = RuntimeJson(
                $"{{\"guid\":\"{Guid}\",\"path\":\"{Path}\"}}");

            Assert.That(gameDB.Import(canonicalJson), Is.Null);
            Assert.That(gameDB.Icon, Is.EqualTo(new UnityObjectReference(Guid, Path)));

            Assert.That(gameDB.Import(RuntimeJson($"\"{Path}\"")), Is.TypeOf<FormatException>());
            Assert.That(gameDB.Icon, Is.EqualTo(new UnityObjectReference(Guid, Path)));

            Assert.That(gameDB.Import(RuntimeJson($"{{\"guid\":\"{Guid}\"}}")),
                Is.TypeOf<FormatException>());
            Assert.That(gameDB.Icon, Is.EqualTo(new UnityObjectReference(Guid, Path)));
        }

        [TestCase("Assets/Game/Sword.asset")]
        [TestCase("Assets/Game/resources/Sword.asset")]
        [TestCase("Assets/Resources/Nested/Resources/Sword.asset")]
        public void UnityObjectReference_AcceptsValidProjectAssetPaths(string path)
        {
            Assert.That(new UnityObjectReference(Guid, path).Path, Is.EqualTo(path));
        }

        [TestCase("Packages/com.example/Asset.asset")]
        [TestCase("Assets")]
        [TestCase("Assets/Game/Asset")]
        [TestCase("Assets/Game/.asset")]
        [TestCase("Assets/Game/Asset.")]
        [TestCase("Assets//Asset.asset")]
        [TestCase("Assets/Game/./Asset.asset")]
        [TestCase("Assets/Game/../Asset.asset")]
        [TestCase("Assets\\Game\\Asset.asset")]
        public void UnityObjectReference_RejectsInvalidProjectAssetPaths(string path)
        {
            Assert.Throws<ArgumentException>(() => new UnityObjectReference(Guid, path));
        }

        private static string RuntimeJson(string iconJson)
        {
            return "{\"tables\":{\"Items\":{\"Sword\":{\"Icon\":"
                + iconJson + "}}}}}";
        }

        private sealed class RuntimeTestGameDB : GameDBBase
        {
            internal RuntimeTestGameDB()
                : base("RuntimeTest", "RuntimeTest")
            {
                var table = new TableBase("Items", KeyType.@string, null,
                    key => new RowBase(key));
                table.Fields.Add("Icon", new FieldBase("Icon", FieldType.unityObject, false));
                RegisterTable("Items", table);
            }

            internal UnityObjectReference Icon =>
                (UnityObjectReference)m_internal.Tables["Items"].GetByKeyRaw("Sword").GetValue("Icon");
        }
    }
}
