using GameDBLibrary;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    internal sealed class UnityDownloadHandler : IDownloadHandler
    {
        private readonly byte[] _data;
        private readonly string _text;

        public UnityDownloadHandler(DownloadHandler handler)
        {
            _data = handler?.data;
            _text = handler?.text;
        }

        public byte[] GetData()
        {
            return _data;
        }

        public string GetText()
        {
            return _text;
        }
    }
}
