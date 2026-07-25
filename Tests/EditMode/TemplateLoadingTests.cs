using GameDBEditorLibrary;
using NUnit.Framework;

namespace GameDBLibrary.Tests
{
    public class TemplateLoadingTests
    {
        [TestCase("gameDB")]
        [TestCase("gameDBUnity")]
        [TestCase("unityLoad")]
        [TestCase("unityLocalizationLoad")]
        public void LoadTemplate_ReadsTemplatesFromResolvedPackagePath(string templateName)
        {
            Assert.That(CSharpExporter.LoadTemplate(templateName), Is.Not.Empty);
        }
    }
}
