using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using UnityEditor.Compilation;
using UnityEngine;
using ReflectionAssembly = System.Reflection.Assembly;

namespace GameDBEditorLibrary
{
    internal class AssemblyExplorer : Singleton<AssemblyExplorer>
    {
        private readonly List<ReflectionAssembly> m_gameAssemblies = new List<ReflectionAssembly>();
        private IReadOnlyList<Type> m_enumTypes = Array.Empty<Type>();

        public IReadOnlyList<Type> EnumTypes => m_enumTypes;

        public void Load()
        {
            var loadedAssemblies = IndexLoadedAssemblies(
                AppDomain.CurrentDomain.GetAssemblies());

            m_gameAssemblies.Clear();

            foreach (var compilationAssembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (!HasProjectSource(compilationAssembly)
                    || !loadedAssemblies.TryGetValue(compilationAssembly.name, out var candidates))
                {
                    continue;
                }

                var assembly = ResolveLoadedAssembly(candidates,
                    EditorAssemblyPath(ProjectRoot(), compilationAssembly.name));
                if (assembly != null)
                {
                    m_gameAssemblies.Add(assembly);
                }
                else
                {
                    Debug.LogWarning($"GameDB could not resolve loaded project assembly '{compilationAssembly.name}' from duplicate candidates: {string.Join(", ", candidates.Select(DescribeLocation))}");
                }
            }

            m_enumTypes = m_gameAssemblies
                .SelectMany(GetLoadableTypes)
                .Where(type => type.IsEnum && (type.IsPublic || type.IsNestedPublic))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        public Type GetType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return m_gameAssemblies
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
        }

        internal static IReadOnlyDictionary<string, ReflectionAssembly[]> IndexLoadedAssemblies(
            IEnumerable<ReflectionAssembly> assemblies)
        {
            return (assemblies ?? Array.Empty<ReflectionAssembly>())
                .Where(assembly => assembly != null && !assembly.IsDynamic)
                .GroupBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        }

        internal static ReflectionAssembly ResolveLoadedAssembly(
            IEnumerable<ReflectionAssembly> candidates, string expectedPath)
        {
            var loaded = candidates?.Where(candidate => candidate != null).ToArray()
                ?? Array.Empty<ReflectionAssembly>();
            if (loaded.Length <= 1)
            {
                return loaded.FirstOrDefault();
            }

            var matchIndex = FindMatchingLocation(
                loaded.Select(GetLocation).ToArray(), expectedPath);
            return matchIndex < 0 ? null : loaded[matchIndex];
        }

        internal static int FindMatchingLocation(
            IReadOnlyList<string> candidatePaths, string expectedPath)
        {
            if (candidatePaths == null || string.IsNullOrWhiteSpace(expectedPath))
            {
                return -1;
            }

            var expected = NormalizePath(expectedPath);
            if (expected == null)
            {
                return -1;
            }

            for (var index = 0; index < candidatePaths.Count; index++)
            {
                var candidate = NormalizePath(candidatePaths[index]);
                if (candidate != null && string.Equals(candidate, expected,
                    PathComparison()))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static string EditorAssemblyPath(string projectRoot, string assemblyName)
        {
            return System.IO.Path.Combine(projectRoot, "Library", "ScriptAssemblies",
                assemblyName + ".dll");
        }

        private static string ProjectRoot()
        {
            return System.IO.Path.GetDirectoryName(Application.dataPath);
        }

        private static string DescribeLocation(ReflectionAssembly assembly)
        {
            var location = GetLocation(assembly);
            return string.IsNullOrWhiteSpace(location) ? "<in-memory>" : location;
        }

        private static string GetLocation(ReflectionAssembly assembly)
        {
            try
            {
                return assembly.Location;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return System.IO.Path.GetFullPath(path)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar,
                        System.IO.Path.AltDirectorySeparatorChar);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (System.IO.IOException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        private static StringComparison PathComparison()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        private static bool HasProjectSource(UnityEditor.Compilation.Assembly assembly)
        {
            return assembly.sourceFiles.Any(sourceFile =>
            {
                var normalizedPath = sourceFile.Replace('\\', '/');
                return normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || normalizedPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private static IEnumerable<Type> GetLoadableTypes(ReflectionAssembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
