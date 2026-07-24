using UnityEngine;

namespace GameDBEditorLibrary
{
    internal abstract class ValueComponent
    {
        public virtual bool ComplexEditable { get; } = false;
        public virtual Rect ArrayPopupRect { get; } = new Rect(100, 100, 202, 200);
        public virtual Rect ComplexPopupRect { get; } = new Rect(100, 100, 202, 200);

        public abstract object RenderField(object value, int fieldWidth, RenderState state = RenderState.Standard);
    }

    internal enum RenderState
    {
        Standard,
        InlineReadOnly,
        Inline,
        Popup,
        PopupArray,
        Dictionary
    }
}