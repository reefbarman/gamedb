using System;
using System.Collections.Generic;
using GameDBLibrary;

namespace GameDBEditorLibrary
{
    internal class EventSystem : Singleton<EventSystem>
    {
        private Dictionary<Enum, Action<object[]>> m_eventHandlers = new Dictionary<Enum, Action<object[]>>();

        public void RegisterEvent(Enum eventType, Action<object[]> handler)
        {
            if (m_eventHandlers.ContainsKey(eventType))
            {
                m_eventHandlers[eventType] += handler;
            }
            else
            {
                m_eventHandlers[eventType] = handler;
            }
        }

        public void DeregisterEvent(Enum eventType, Action<object[]> handler)
        {
            if (m_eventHandlers.ContainsKey(eventType))
            {
                m_eventHandlers[eventType] -= handler;

                if (m_eventHandlers[eventType].GetInvocationList().Length == 0)
                {
                    m_eventHandlers.Remove(eventType);
                }
            }
        }

        public void TriggerEvent(Enum eventType, params object[] args) 
        {
            if (m_eventHandlers.ContainsKey(eventType))
            {
                m_eventHandlers[eventType](args);
            }
        }
    }
}
