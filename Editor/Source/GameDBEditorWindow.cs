using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using System;
using UnityEditor;

public class GameDBEditorWindow : EditorWindow
{
    [NonSerialized] private GameDBEditorWorkspace m_workspace;
    [NonSerialized] private GameDBEditorWindowController m_controller;
    [NonSerialized] private bool m_hooksRegistered;
    [NonSerialized] private bool m_shuttingDown;

    internal GameDBEditorWorkspace Workspace => m_workspace;

    [MenuItem("Window/GameDB/Open Editor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(GameDBEditorWindow), false, "GameDB");
    }

    private void OnEnable()
    {
        if (m_workspace != null || m_shuttingDown)
        {
            return;
        }

        m_workspace = new GameDBEditorWorkspace();
        RegisterHooks();
        TryBindWorkspace();
    }

    public void CreateGUI()
    {
        try
        {
            DisposeController();
            GameDBEditorUiAssets.Build(rootVisualElement);
            TryBindWorkspace();
        }
        catch (Exception exception)
        {
            DisposeControllerSafely();
            GameDBEditorUiAssets.ShowError(rootVisualElement, exception);
        }
    }

    private void OnFocus()
    {
        if (m_workspace == null || m_shuttingDown)
        {
            return;
        }

        if (!m_workspace.MarkFocused())
        {
            UnityEngine.Debug.LogWarning(
                "The GameDB editor workspace could not be marked active.");
            return;
        }
        m_workspace.RequestDiskProbe();
    }

    private void OnDisable()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void BeforeAssemblyReload()
    {
        Shutdown();
    }

    private void TryBindWorkspace()
    {
        if (m_workspace == null || m_shuttingDown
            || !GameDBEditorUiAssets.IsBuilt(rootVisualElement))
        {
            return;
        }

        if (m_controller == null)
        {
            m_controller = new GameDBEditorWindowController(
                rootVisualElement, m_workspace);
        }
    }

    private void DisposeController()
    {
        m_controller?.Dispose();
        m_controller = null;
    }

    private void DisposeControllerSafely()
    {
        try
        {
            DisposeController();
        }
        catch (Exception exception)
        {
            m_controller = null;
            LogShutdownFailure("detach the GameDB editor controller", exception);
        }
    }

    private void RegisterHooks()
    {
        if (m_hooksRegistered)
        {
            return;
        }

        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        m_hooksRegistered = true;
    }

    private void UnregisterHooks()
    {
        if (!m_hooksRegistered)
        {
            return;
        }

        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        m_hooksRegistered = false;
    }

    private void Shutdown()
    {
        if (m_shuttingDown || m_workspace == null)
        {
            return;
        }

        m_shuttingDown = true;
        var workspace = m_workspace;
        m_workspace = null;
        DisposeControllerSafely();
        try
        {
            UnregisterHooks();
        }
        catch (Exception exception)
        {
            LogShutdownFailure("unregister GameDB editor hooks", exception);
        }
        try
        {
            workspace.Dispose();
        }
        catch (Exception exception)
        {
            LogShutdownFailure("shut down the GameDB editor workspace", exception);
        }
        finally
        {
            m_shuttingDown = false;
        }
    }

    private static void LogShutdownFailure(string operation, Exception exception)
    {
        UnityEngine.Debug.LogError("Failed to " + operation + ".");
        UnityEngine.Debug.LogException(exception);
    }
}
