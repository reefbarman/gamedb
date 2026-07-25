using GameDBEditorLibrary;
using UnityEditor;

public class GameDBEditorWindow : EditorWindow
{
    [MenuItem("Window/GameDB/Open Editor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(GameDBEditorWindow), false, "GameDB");
    }

    private void OnEnable()
    {
        GameDBEditor.Init(this);
    }

    private void OnGUI()
    {
        GameDBEditor.OnGUI();
    }

    private void Update()
    {
        GameDBEditor.Update();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}
