namespace GameDBLibrary
{
    [System.Obsolete(LegacyRemoteApi.Message)]
    public interface IDownloadHandler
    {
        byte[] GetData();
        string GetText();
    }
}
