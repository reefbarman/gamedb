using System.Linq;

namespace GameDBLibrary
{
    public static class GameDBEditorInvoker
    {
        public static void AddRuntimeDB(GameDBBase gameDB)
        {
            Invoke("AddRuntimeDB", new[] { gameDB });
        }

        private static void Invoke(string methodName, object[] parameters)
        {
            System.Reflection.Assembly editorAssembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("GameDBEditorLibrary"));
            var gameDBEditorType = editorAssembly.GetTypes().FirstOrDefault(t => t.Namespace == "GameDBEditorLibrary" && t.FullName.EndsWith(".GameDBEditor"));
            var method = gameDBEditorType.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method.Invoke(null, parameters);
        }
    }
}
