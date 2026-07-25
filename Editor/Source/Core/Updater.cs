using System;
using GameDBLibrary;

namespace GameDBEditorLibrary
{
    internal class Updater : Singleton<Updater>
    {
        public Action OnUpdate = null;

        public void Update()
        {
            OnUpdate?.Invoke();
        }
    }
}
