using System;

namespace GameDBLibraryAddressables
{
    public sealed class AddressableAssetLoadException : Exception
    {
        public string AssetGuid { get; }
        public string AssetPath { get; }
        public Type RequestedType { get; }

        internal AddressableAssetLoadException(string assetGuid, string assetPath,
            Type requestedType, string detail, Exception innerException = null)
            : base(
                $"Failed to load Addressable asset GUID '{assetGuid}' at '{assetPath}' as {requestedType.FullName}. {detail}",
                innerException)
        {
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            RequestedType = requestedType;
        }
    }
}
