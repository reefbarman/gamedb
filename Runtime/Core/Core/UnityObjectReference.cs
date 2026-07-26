using System;
using System.Collections.Generic;

namespace GameDBLibrary
{
    public sealed class UnityObjectReference : IEquatable<UnityObjectReference>
    {
        public static readonly UnityObjectReference Empty = new UnityObjectReference(string.Empty, string.Empty);

        public string Guid { get; }
        public string Path { get; }
        public bool IsEmpty => Guid.Length == 0;

        public UnityObjectReference(string guid, string path)
        {
            if (!IsValid(guid, path))
            {
                throw new ArgumentException("Unity object references require either empty values or a lowercase 32-character asset GUID and an Assets path beneath one Resources directory.");
            }

            Guid = guid;
            Path = path;
        }

        public bool Equals(UnityObjectReference other)
        {
            return !(other is null)
                && string.Equals(Guid, other.Guid, StringComparison.Ordinal)
                && string.Equals(Path, other.Path, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as UnityObjectReference);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Guid != null ? Guid.GetHashCode() : 0) * 397)
                    ^ (Path != null ? Path.GetHashCode() : 0);
            }
        }

        public static bool operator ==(UnityObjectReference left, UnityObjectReference right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(UnityObjectReference left, UnityObjectReference right)
        {
            return !Equals(left, right);
        }

        internal static bool IsValid(string guid, string path)
        {
            if (guid == null || path == null)
            {
                return false;
            }

            if (guid.Length == 0 || path.Length == 0)
            {
                return guid.Length == 0 && path.Length == 0;
            }

            return IsCanonicalGuid(guid) && TryGetResourcesPath(path, out _);
        }

        internal static bool TryGetResourcesPath(string assetPath, out string resourcesPath)
        {
            resourcesPath = null;
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || assetPath.IndexOf('\\') >= 0)
            {
                return false;
            }

            var segments = assetPath.Split('/');
            var resourcesIndex = -1;
            for (var index = 0; index < segments.Length; index++)
            {
                if (!string.Equals(segments[index], "Resources", StringComparison.Ordinal))
                {
                    continue;
                }

                if (resourcesIndex >= 0)
                {
                    return false;
                }

                resourcesIndex = index;
            }

            if (resourcesIndex < 1 || resourcesIndex >= segments.Length - 1)
            {
                return false;
            }

            var fileName = segments[segments.Length - 1];
            var extensionIndex = fileName.LastIndexOf('.');
            if (extensionIndex <= 0 || extensionIndex == fileName.Length - 1)
            {
                return false;
            }

            segments[segments.Length - 1] = fileName.Substring(0, extensionIndex);
            resourcesPath = string.Join("/", segments, resourcesIndex + 1,
                segments.Length - resourcesIndex - 1);
            return resourcesPath.Length > 0;
        }

        private static bool IsCanonicalGuid(string guid)
        {
            if (guid.Length != 32)
            {
                return false;
            }

            for (var index = 0; index < guid.Length; index++)
            {
                var character = guid[index];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class UnityObjectReferenceWire
    {
        internal static bool TryParse(object value, out UnityObjectReference reference)
        {
            reference = null;
            if (!(value is IDictionary<string, object> dictionary)
                || dictionary.Count != 2
                || !dictionary.TryGetValue("guid", out var guidValue)
                || !dictionary.TryGetValue("path", out var pathValue)
                || !(guidValue is string guid)
                || !(pathValue is string path)
                || !UnityObjectReference.IsValid(guid, path))
            {
                return false;
            }

            reference = guid.Length == 0
                ? UnityObjectReference.Empty
                : new UnityObjectReference(guid, path);
            return true;
        }

        internal static UnityObjectReference Parse(object value)
        {
            if (!TryParse(value, out var reference))
            {
                throw new FormatException("Unity object value must be an exact {guid, path} object containing either empty strings or a lowercase asset GUID and Resources asset path.");
            }

            return reference;
        }

        internal static IDictionary<string, object> Serialize(UnityObjectReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            return new Dictionary<string, object>
            {
                { "guid", reference.Guid },
                { "path", reference.Path }
            };
        }
    }
}
