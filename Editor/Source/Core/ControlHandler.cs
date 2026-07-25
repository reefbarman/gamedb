using GameDBLibrary;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class ControlHandler : Singleton<ControlHandler>
    {
        private Event m_currentEvent = null;

        public void Update()
        {
            m_currentEvent = Event.current;
        }

        public bool GetKeyPressed(KeyCode code)
        {
            return m_currentEvent.isKey && m_currentEvent.keyCode == KeyCode.Return;
        }
    }
}
