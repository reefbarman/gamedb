using UnityEditor;
using UnityEngine;


public class ExportPackage
{
    [MenuItem("GameDB/Export Package")]
    private static void ExportGameDBPackage()
    {
        Debug.Log("Exporting Package");

        AssetDatabase.ExportPackage(new [] {
            "Assets/Plugins/GameDBLibrary/GoogleSheets/GoogleSheetWebApp.gs",
            "Assets/Plugins/GameDBLibrary/GameDBLibrary.dll",
            "Assets/Plugins/GameDBLibrary/GameDBLibrary.xml",
            "Assets/Plugins/GameDBLibrary/GameDBLibraryUnity.dll",
            "Assets/Plugins/GameDBLibrary/GameDBLibraryUnity.xml",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorLibrary.dll",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorLibrary.xml",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorWindow.cs",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorWindowPro.cs",
            "Assets/Plugins/GameDBLibrary/documentation.pdf",
            /* Debug files (REMOVE WHEN RELEASING) */
            "Assets/Plugins/GameDBLibrary/GameDBLibrary.dll.mdb",
            "Assets/Plugins/GameDBLibrary/GameDBLibrary.pdb",
            "Assets/Plugins/GameDBLibrary/GameDBLibraryUnity.dll.mdb",
            "Assets/Plugins/GameDBLibrary/GameDBLibraryUnity.pdb",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorLibrary.dll.mdb",
            "Assets/Plugins/GameDBLibrary/Editor/GameDBEditorLibrary.pdb",
        }, "GameDB.unitypackage", ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse);
    }
}
