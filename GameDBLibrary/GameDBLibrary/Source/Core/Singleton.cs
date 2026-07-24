namespace GameDBLibrary
{
    /// <summary>
    /// A helper base class for creating singletons.
    /// Used internally but provided for utility reasons.
    /// </summary>
    /// <typeparam name="T">The type of the singleton class</typeparam>
    public class Singleton<T> where T : class, new()
    {
        private static T s_instance = null;

        /// <summary>
        /// Returns the singleton instance (and instantiates it if not already instantiated)
        /// </summary>
        /// <value>
        /// The singleton instance of the inherited class.
        /// </value>
        public static T Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new T();
                }

                return s_instance;
            }
        }
    }
}
