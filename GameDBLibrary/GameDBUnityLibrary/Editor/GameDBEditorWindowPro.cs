using GameDBLibrary;

public partial class GameDBEditorWindow
{
    public GameDBEditorWindow() {
        _initMethod = Init;
    }

    private void Init() {
        WebRequestHelper.Request = new GameDBLibraryUnity.WebRequest();
        WebRequestHelper.FormFactory = new GameDBLibraryUnity.FormFactory();
    }
}
