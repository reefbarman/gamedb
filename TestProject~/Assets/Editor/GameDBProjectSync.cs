using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class GameDBProjectSync
{
    public static void Sync()
    {
        var integrationAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Unity.VisualStudio.Editor");

        if (integrationAssembly == null)
        {
            throw new InvalidOperationException("Unity Visual Studio Editor integration is not loaded.");
        }

        var factoryType = integrationAssembly.GetType("Microsoft.Unity.VisualStudio.Editor.GeneratorFactory", true);
        var styleType = integrationAssembly.GetType("Microsoft.Unity.VisualStudio.Editor.GeneratorStyle", true);
        var sdkStyle = Enum.Parse(styleType, "SDK");
        var getInstance = factoryType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var generator = getInstance.Invoke(null, new[] { sdkStyle });
        var sync = generator.GetType().GetMethod("Sync", BindingFlags.Public | BindingFlags.Instance);

        sync.Invoke(generator, null);
        Debug.Log("Generated SDK-style Unity solution files.");
    }
}
