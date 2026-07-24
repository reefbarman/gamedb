using GameDBEditorLibrary;
using System;
using UnityEditor;

public partial class GameDBEditorWindow : EditorWindow {
    private Action _initMethod;

    [MenuItem("Window/GameDB/Open Editor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(GameDBEditorWindow), false, "GameDB");
    }

    private void OnEnable() {
        if (_initMethod != null) {
            _initMethod();
        }
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
