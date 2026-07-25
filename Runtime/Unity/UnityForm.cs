using System.Collections.Generic;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    public sealed class UnityForm
    {
        private readonly List<IMultipartFormSection> _sections = new List<IMultipartFormSection>();

        internal List<IMultipartFormSection> Sections => _sections;

        public void AddField(string key, string value)
        {
            _sections.Add(new MultipartFormDataSection(key, value));
        }

        public void AddBinaryData(string key, byte[] data, string fileName)
        {
            _sections.Add(new MultipartFormFileSection(key, data, fileName, "application/octet-stream"));
        }
    }
}
