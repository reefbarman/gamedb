using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;

namespace GameDBEditorLibrary.Automation
{
    public static class GameDBDocumentationService
    {
        private static readonly GameDBDocumentationEntry[] Catalog =
        {
            new GameDBDocumentationEntry("index", "GameDB documentation", "Documentation~/index.md"),
            new GameDBDocumentationEntry("readme", "Package overview and quick start", "README.md"),
            new GameDBDocumentationEntry("editor-authoring", "Editor authoring", "Documentation~/editor-authoring.md"),
            new GameDBDocumentationEntry("runtime", "Runtime use and hot reload", "Documentation~/runtime.md"),
            new GameDBDocumentationEntry("addressables", "Optional Addressables integration", "Documentation~/addressables.md"),
            new GameDBDocumentationEntry("api-reference", "Supported API reference", "Documentation~/api-reference.md"),
            new GameDBDocumentationEntry("automation", "Agent and editor automation", "Documentation~/automation.md"),
            new GameDBDocumentationEntry("google-sheets", "Optional Google Sheets interoperability", "Documentation~/google-sheets.md"),
            new GameDBDocumentationEntry("basic-sample", "Basic GameDB sample", "Samples~/Basic/README.md"),
            new GameDBDocumentationEntry("changelog", "Changelog", "CHANGELOG.md")
        };

        public static GameDBDocumentationCatalog ListDocuments()
        {
            return new GameDBDocumentationCatalog
            {
                Success = true,
                Message = $"Found {Catalog.Length} GameDB document(s).",
                Documents = Catalog.Select(entry => entry.Copy()).ToList()
            };
        }

        public static GameDBDocumentationResult ReadDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return Failure(documentId, "Document ID is required.");
            }

            var entry = Catalog.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, documentId, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                return Failure(documentId, $"Unknown document ID: {documentId}");
            }

            try
            {
                var packageInfo = PackageInfo.FindForAssembly(typeof(GameDBDocumentationService).Assembly);
                if (packageInfo == null)
                {
                    return Failure(entry.Id, "Unable to resolve the installed GameDB package path.");
                }

                var absolutePath = Path.GetFullPath(Path.Combine(packageInfo.resolvedPath, entry.RelativePath));
                var packageRoot = Path.GetFullPath(packageInfo.resolvedPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!absolutePath.StartsWith(packageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(entry.Id, "Documentation path resolves outside the GameDB package.");
                }

                return new GameDBDocumentationResult
                {
                    Success = true,
                    DocumentId = entry.Id,
                    Title = entry.Title,
                    RelativePath = entry.RelativePath,
                    Content = File.ReadAllText(absolutePath),
                    Message = "Document loaded."
                };
            }
            catch (Exception exception)
            {
                return Failure(entry.Id, exception.Message);
            }
        }

        private static GameDBDocumentationResult Failure(string documentId, string message)
        {
            return new GameDBDocumentationResult
            {
                Success = false,
                DocumentId = documentId,
                Message = message
            };
        }
    }

    public sealed class GameDBDocumentationCatalog
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public List<GameDBDocumentationEntry> Documents { get; internal set; } = new List<GameDBDocumentationEntry>();
    }

    public sealed class GameDBDocumentationEntry
    {
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string RelativePath { get; internal set; }

        internal GameDBDocumentationEntry(string id, string title, string relativePath)
        {
            Id = id;
            Title = title;
            RelativePath = relativePath;
        }

        internal GameDBDocumentationEntry Copy()
        {
            return new GameDBDocumentationEntry(Id, Title, RelativePath);
        }
    }

    public sealed class GameDBDocumentationResult
    {
        public bool Success { get; internal set; }
        public string DocumentId { get; internal set; }
        public string Title { get; internal set; }
        public string RelativePath { get; internal set; }
        public string Content { get; internal set; }
        public string Message { get; internal set; }
    }
}
