using System.Text;

namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public 
#endif
    class HTTPDownloadHandler : IDownloadHandler
    {
        private byte[] _data;

        public HTTPDownloadHandler(byte[] data)
        {
            _data = data;
        }

        public byte[] GetData()
        {
            return _data;
        }

        public string GetText()
        {
            return Encoding.UTF8.GetString(_data);
        }
    }
}
