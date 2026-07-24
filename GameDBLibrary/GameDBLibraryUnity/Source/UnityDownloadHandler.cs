using GameDBLibrary;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    public class UnityDownloadHandler : IDownloadHandler
    {
        private readonly DownloadHandler _handler;

        public UnityDownloadHandler(DownloadHandler handler)
        {
            _handler = handler;
        }

        public byte[] GetData()
        {
            return _handler.data;
        }

        public string GetText()
        {
            return _handler.text;
        }
    }
}
