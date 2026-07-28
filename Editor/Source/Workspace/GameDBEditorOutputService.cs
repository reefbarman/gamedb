using GameDBEditorLibrary.Automation;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary.Workspace
{
    internal sealed class GameDBEditorOutputResult
    {
        internal bool Success { get; }
        internal string Message { get; }
        internal string OutputPath { get; }
        internal bool RequiresConfirmation { get; }

        internal GameDBEditorOutputResult(bool success, string message,
            string outputPath = null, bool requiresConfirmation = false)
        {
            Success = success;
            Message = message;
            OutputPath = outputPath;
            RequiresConfirmation = requiresConfirmation;
        }
    }

    internal interface IGameDBEditorOutputService
    {
        GameDBEditorOutputResult Generate(GameDBEditorWorkspaceTab tab, string exportPath,
            bool allowDestructive = false);
        GameDBEditorOutputResult Build(GameDBEditorWorkspaceTab tab, string buildPath);
    }

    internal sealed class GameDBEditorOutputService : IGameDBEditorOutputService
    {
        public GameDBEditorOutputResult Generate(GameDBEditorWorkspaceTab tab,
            string exportPath, bool allowDestructive = false)
        {
            var invalid = Validate(tab, exportPath, "generation");
            if (invalid != null)
            {
                return invalid;
            }
            var output = ToAssetDirectory(exportPath);
            var scopePath = Path.Combine(Application.dataPath,
                NormalizeRelativePath(exportPath), tab.Session.CreateSnapshot().ScopeName);
            if (!allowDestructive && Directory.Exists(scopePath)
                && Directory.EnumerateFileSystemEntries(scopePath).Any())
            {
                return new GameDBEditorOutputResult(false,
                    "Generation will replace the existing scope output directory.",
                    output, true);
            }
            var saved = tab.Session.Save();
            if (!saved.Success)
            {
                return new GameDBEditorOutputResult(false, saved.Message);
            }
            var result = GameDBAutomationService.GenerateCSharp(new GameDBGenerateRequest
            {
                DatabasePath = tab.Session.AssetPath,
                OutputDirectory = output,
                IncludeUnityLoader = true,
                Options = new GameDBOperationOptions
                {
                    ExpectedRevision = tab.Session.GetState().CurrentRevision,
                    AllowDestructive = allowDestructive
                }
            });
            return new GameDBEditorOutputResult(result.Success, result.Message,
                result.Success ? output : null);
        }

        public GameDBEditorOutputResult Build(GameDBEditorWorkspaceTab tab, string buildPath)
        {
            var invalid = Validate(tab, buildPath, "build");
            if (invalid != null)
            {
                return invalid;
            }
            var saved = tab.Session.Save();
            if (!saved.Success)
            {
                return new GameDBEditorOutputResult(false, saved.Message);
            }
            try
            {
                var relativeDirectory = NormalizeRelativePath(buildPath);
                var fileName = Path.GetFileNameWithoutExtension(tab.Session.AssetPath) + ".json";
                var assetPath = "Assets/" + relativeDirectory.TrimEnd('/') + "/" + fileName;
                var absolutePath = Path.Combine(Application.dataPath,
                    relativeDirectory, fileName);
                if (File.Exists(absolutePath))
                {
                    return new GameDBEditorOutputResult(false,
                        "Build output already exists. Choose an empty output directory.", assetPath);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllText(absolutePath, tab.Session.SerializeCurrent().DataJson);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                return new GameDBEditorOutputResult(true, "Data-only JSON built.", assetPath);
            }
            catch (Exception exception)
            {
                return new GameDBEditorOutputResult(false, exception.Message);
            }
        }

        private static GameDBEditorOutputResult Validate(GameDBEditorWorkspaceTab tab,
            string path, string operation)
        {
            if (tab == null || tab.Session.IsDisposed)
            {
                return new GameDBEditorOutputResult(false, "No active GameDB document is available.");
            }
            if (tab.HasPlayModeState)
            {
                return new GameDBEditorOutputResult(false,
                    $"Code {operation} is unavailable while editing a runtime GameDB.");
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                return new GameDBEditorOutputResult(false,
                    $"Configure the {operation} output path in GameDB settings.");
            }
            try
            {
                NormalizeRelativePath(path);
                return null;
            }
            catch (Exception exception)
            {
                return new GameDBEditorOutputResult(false, exception.Message);
            }
        }

        private static string ToAssetDirectory(string path)
        {
            return "Assets/" + NormalizeRelativePath(path);
        }

        private static string NormalizeRelativePath(string path)
        {
            var normalized = path.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Output path must be a project-relative directory under Assets.");
            }
            normalized = normalized.Trim('/');
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                normalized = string.Empty;
            }
            else if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Assets/".Length).Trim('/');
            }
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized)
                || normalized.Split('/').Any(segment => segment == ".."))
            {
                throw new ArgumentException("Output path must be a project-relative directory under Assets.");
            }
            return normalized;
        }
    }
}
