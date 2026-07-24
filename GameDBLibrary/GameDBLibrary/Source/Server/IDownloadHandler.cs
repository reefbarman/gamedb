namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public 
#endif
    interface IDownloadHandler
    {
        byte[] GetData();
        string GetText();
    }
}
