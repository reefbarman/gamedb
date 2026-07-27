using System;

namespace GameDBLibrary
{
    public sealed class GameDBDataLoadException : Exception
    {
        public string Location { get; }
        public Type LoaderType { get; }

        public GameDBDataLoadException(string location, Type loaderType,
            Exception innerException)
            : base(CreateMessage(location, loaderType), innerException)
        {
            Location = location;
            LoaderType = loaderType;
        }

        private static string CreateMessage(string location, Type loaderType)
        {
            var loaderName = loaderType == null ? "unknown loader" : loaderType.FullName;
            return $"Failed to load GameDB data from '{location}' using {loaderName}.";
        }
    }
}
