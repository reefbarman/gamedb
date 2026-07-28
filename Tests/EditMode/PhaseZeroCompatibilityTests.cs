using GameDBEditorLibrary;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Reflection;

namespace GameDBLibrary.Tests
{
    public class PhaseZeroCompatibilityTests
    {
        private const string RemovalVersion = "GameDB 1.0.0";

        [TestCase("GameDBLibrary.Remote")]
        [TestCase("GameDBLibrary.RequestUpdater")]
        [TestCase("GameDBLibrary.WebRequestHelper")]
        [TestCase("GameDBLibrary.ServerResponse")]
        [TestCase("GameDBLibrary.IDownloadHandler")]
        [TestCase("GameDBLibrary.RequestMethod")]
        [TestCase("GameDBLibraryUnity.UnityForm")]
        [TestCase("GameDBLibraryUnity.UnityWebRequestTransport")]
        public void LegacyRemoteTypes_AreWarningOnlyObsolete(string typeName)
        {
            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    break;
                }
            }

            Assert.That(type, Is.Not.Null, typeName);
            AssertWarningOnlyObsolete(type, typeName);
        }

        [Test]
        public void LegacyRemoteMembers_AreWarningOnlyObsolete()
        {
            var importFromServer = typeof(GameDBBase).GetMethod("ImportFromServer", BindingFlags.Instance | BindingFlags.Public);
            var getChecksum = typeof(Utils).GetMethod("GetChecksum", BindingFlags.Static | BindingFlags.Public);
            var promotionCallback = typeof(GameDBEditor).GetMethod("RegisterRevisionPromotionCallback", BindingFlags.Static | BindingFlags.Public);

            AssertWarningOnlyObsolete(importFromServer, "GameDBBase.ImportFromServer");
            AssertWarningOnlyObsolete(getChecksum, "Utils.GetChecksum");
            AssertWarningOnlyObsolete(promotionCallback, "GameDBEditor.RegisterRevisionPromotionCallback");
        }

        [Test]
        public void EditorAssembly_RemovesServerUiButKeepsDataBuild()
        {
            var editorAssembly = typeof(GameDBEditor).Assembly;

            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.BuildComponent"), Is.Null);
            Assert.That(typeof(GameDBEditorOutputService).GetMethod("Build",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.DeploymentComponent"), Is.Null);
            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.DeploymentPickerComponent"), Is.Null);
            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.ServerManagementComponent"), Is.Null);
            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.ServerDeploymentsDataSource"), Is.Null);
            Assert.That(editorAssembly.GetType("GameDBEditorLibrary.DownloadHelper"), Is.Null);
        }

        private static void AssertWarningOnlyObsolete(MemberInfo member, string memberName)
        {
            Assert.That(member, Is.Not.Null, memberName);

            var attribute = member.GetCustomAttribute<ObsoleteAttribute>();
            Assert.That(attribute, Is.Not.Null, memberName);
            Assert.That(attribute.IsError, Is.False, memberName);
            Assert.That(attribute.Message, Does.Contain(RemovalVersion), memberName);
        }
    }
}
