namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    interface IForm
    {
        void AddField(string key, string value);
        void AddBinaryData(string key, byte[] data, string fileName);
    }
}
