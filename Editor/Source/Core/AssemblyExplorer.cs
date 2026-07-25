using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Compilation;
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
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .ToDictionary(assembly => assembly.GetName().Name, StringComparer.Ordinal);

            m_gameAssemblies.Clear();

            foreach (var compilationAssembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (!HasProjectSource(compilationAssembly) || !loadedAssemblies.TryGetValue(compilationAssembly.name, out var assembly))
                {
                    continue;
                }

                m_gameAssemblies.Add(assembly);
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
