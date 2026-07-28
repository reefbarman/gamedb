using GameDBEditorLibrary;
using NUnit.Framework;
using System.Reflection;

namespace GameDBLibrary.Tests
{
    public class AssemblyExplorerTests
    {
        [Test]
        public void IndexLoadedAssemblies_DuplicateSimpleNamesAreGrouped()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var indexed = AssemblyExplorer.IndexLoadedAssemblies(new[] { assembly, assembly });

            Assert.That(indexed[assembly.GetName().Name], Is.EqualTo(new[] { assembly, assembly }));
        }

        [Test]
        public void FindMatchingLocation_DuplicateCandidatesSelectEditorAssemblyPath()
        {
            var match = AssemblyExplorer.FindMatchingLocation(new[]
            {
                string.Empty,
                "/project/Library/ScriptAssemblies/Gameplay.dll"
            }, "/project/Library/ScriptAssemblies/Gameplay.dll");

            Assert.That(match, Is.EqualTo(1));
        }

        [Test]
        public void FindMatchingLocation_DuplicateCandidatesWithoutOutputMatchAreSkipped()
        {
            var match = AssemblyExplorer.FindMatchingLocation(new[]
            {
                string.Empty,
                "/other/Gameplay.dll"
            }, "/project/Library/ScriptAssemblies/Gameplay.dll");

            Assert.That(match, Is.EqualTo(-1));
        }

        [Test]
        public void EditorAssemblyPath_UsesScriptAssembliesDirectory()
        {
            var path = AssemblyExplorer.EditorAssemblyPath("/project", "Gameplay");

            Assert.That(path, Is.EqualTo(
                "/project/Library/ScriptAssemblies/Gameplay.dll"));
        }

        [Test]
        public void ResolveLoadedAssembly_SingleCandidateRemainsCompatible()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var resolved = AssemblyExplorer.ResolveLoadedAssembly(
                new[] { assembly }, "/unrelated/Assembly.dll");

            Assert.That(resolved, Is.SameAs(assembly));
        }

        [Test]
        public void ResolveLoadedAssembly_EmptyCandidatesReturnNull()
        {
            var resolved = AssemblyExplorer.ResolveLoadedAssembly(
                System.Array.Empty<Assembly>(), "/project/Library/ScriptAssemblies/Gameplay.dll");

            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void ResolveLoadedAssembly_DuplicateCandidatesSelectLocationMatch()
        {
            var executing = Assembly.GetExecutingAssembly();
            var editor = typeof(AssemblyExplorer).Assembly;

            var resolved = AssemblyExplorer.ResolveLoadedAssembly(
                new[] { executing, editor }, editor.Location);

            Assert.That(resolved, Is.SameAs(editor));
        }
    }
}
