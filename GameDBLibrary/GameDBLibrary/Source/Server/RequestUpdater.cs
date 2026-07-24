using System;

namespace GameDBLibrary
{
    /// <summary>
    /// RequestUpdater is a class that allows an update method to be hooked into an update loop,
    /// to allow constant updating of a requests asynchronous handling mechanisms
    /// </summary>
#if FREE_VERSION
    internal
#else
    public
#endif
    class RequestUpdater
    {
        /// <summary>
        /// A callback to call each Update
        /// </summary>
        public Action OnUpdate = null;

        /// <summary>
        /// Updates the RequestUpdater. Call often to keep the associated callback receiving regular updates.
        /// </summary>
        public void Update()
        {
            OnUpdate?.Invoke();
        }
    }
}
