using GameDBEditorLibrary.Documents;
using UnityEditor;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBDestructiveActionRequest
    {
        internal GameDBCommandKind? Kind { get; }
        internal string AssetPath { get; }
        internal string Title { get; }
        internal string Message { get; }
        internal string ConfirmLabel { get; }

        internal GameDBDestructiveActionRequest(GameDBCommandKind? kind,
            string assetPath, string title, string message, string confirmLabel)
        {
            Kind = kind;
            AssetPath = assetPath;
            Title = title;
            Message = message;
            ConfirmLabel = confirmLabel;
        }
    }

    internal interface IGameDBEditorDestructiveActionPolicy
    {
        bool Confirm(GameDBDestructiveActionRequest request);
    }

    internal sealed class GameDBEditorDestructiveActionDialogPolicy
        : IGameDBEditorDestructiveActionPolicy
    {
        public bool Confirm(GameDBDestructiveActionRequest request)
        {
            return EditorUtility.DisplayDialog(request.Title, request.Message,
                request.ConfirmLabel, "Cancel");
        }
    }
}
