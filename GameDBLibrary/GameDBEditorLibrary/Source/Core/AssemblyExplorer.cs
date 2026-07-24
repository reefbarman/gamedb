using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class AssemblyExplorer : Singleton<AssemblyExplorer>
    {
        private Assembly m_gameAssembly = null;

        private IEnumerable<Type> m_enumTypes = null;

        public IEnumerable<Type> EnumTypes => m_enumTypes;

        public void Load() {
            var assemblyPath = Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll";

            if (File.Exists(assemblyPath))
            {
                m_gameAssembly = Assembly.LoadFile(assemblyPath);

                m_enumTypes = GetAllTypes(m_gameAssembly).Where(t => t.IsEnum && (t.IsPublic || t.IsNestedPublic));
            }
        }

        public Type GetType(string type) {
            return m_gameAssembly?.GetType(type);
        }

        private Type[] GetAllTypes(Assembly assembly)
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                types.Concat(GetNestedTypes(type));
            }

            Array.Sort(types, (type, type1) => type.ToString().CompareTo(type1.ToString()));

            return types;
        }

        private Type[] GetNestedTypes(Type type)
        {
            var nestedTypes = type.GetNestedTypes();

            foreach (var nestedType in nestedTypes)
            {
                nestedTypes.Concat(GetNestedTypes(nestedType));
            }

            return nestedTypes;
        }
    }
}
