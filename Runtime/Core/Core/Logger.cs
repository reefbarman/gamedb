using System;

namespace GameDBLibrary
{
    /// <summary>
    /// The logger class allows internal logging in the GameDB classes to be redirected to any
    /// destination required ie. Unity console, file on disk or terminal output.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Used to log general information from internal classes.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public virtual void Log(string message) {
            Console.WriteLine(message);
        }

        /// <summary>
        /// Used to log errors from internal classes.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public virtual void LogError(string message)
        {
            Console.WriteLine($"Error: {message}");
        }

        /// <summary>
        /// Used to log exceptions from internal classes.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        public virtual void LogException(Exception e)
        {
            Console.WriteLine($"Exception: {e.Message}");
            Console.WriteLine(e.StackTrace);
        }
    }
}
