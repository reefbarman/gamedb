using GameDBEditorLibrary.Automation;
using NUnit.Framework;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBDocumentationTests
    {
        [Test]
        public void ListDocuments_ReturnsStableAgentCatalog()
        {
            var result = GameDBDocumentationService.ListDocuments();

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Documents.Select(document => document.Id), Is.EquivalentTo(new[]
            {
                "index",
                "readme",
                "editor-authoring",
                "runtime",
                "api-reference",
                "automation",
                "google-sheets",
                "basic-sample",
                "changelog"
            }));
            Assert.That(result.Documents.All(document => !string.IsNullOrWhiteSpace(document.Title)), Is.True);
        }

        [TestCase("index", "# GameDB documentation")]
        [TestCase("editor-authoring", "# Editor authoring")]
        [TestCase("runtime", "# Runtime use")]
        [TestCase("api-reference", "# GameDB API reference")]
        [TestCase("automation", "# GameDB editor automation")]
        [TestCase("basic-sample", "# Basic GameDB sample")]
        public void ReadDocument_LoadsBundledMarkdown(string documentId, string expectedHeading)
        {
            var result = GameDBDocumentationService.ReadDocument(documentId);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Content, Does.StartWith(expectedHeading));
            Assert.That(result.RelativePath, Does.Not.Contain(".."));
        }

        [Test]
        public void ReadDocument_AutomationDescribesBatchContract()
        {
            var result = GameDBDocumentationService.ReadDocument("automation");

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Content, Does.Contain("ApplyBatch"));
            Assert.That(result.Content, Does.Contain("GameDBBatchRequest"));
            Assert.That(result.Content, Does.Contain("AllowedDestructiveOperations"));
            Assert.That(result.Content, Does.Contain("FailedOperationIndex"));
            Assert.That(result.Content, Does.Contain("PostSavePending"));
        }

        [Test]
        public void ListDocuments_EveryEntryCanBeRead()
        {
            var catalog = GameDBDocumentationService.ListDocuments();

            foreach (var document in catalog.Documents)
            {
                var result = GameDBDocumentationService.ReadDocument(document.Id);

                Assert.That(result.Success, Is.True, $"{document.Id}: {result.Message}");
                Assert.That(result.Content, Is.Not.Empty, document.Id);
                Assert.That(result.RelativePath, Is.EqualTo(document.RelativePath), document.Id);
            }
        }

        [TestCase("../README.md")]
        [TestCase("not-a-document")]
        [TestCase("")]
        public void ReadDocument_RejectsValuesOutsideCatalog(string documentId)
        {
            var result = GameDBDocumentationService.ReadDocument(documentId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Content, Is.Null);
        }
    }
}
