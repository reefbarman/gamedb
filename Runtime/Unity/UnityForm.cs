using System.Collections.Generic;
using UnityEngine.Networking;

namespace GameDBLibraryUnity
{
    [System.Obsolete("The legacy GameDB remote/deployment API is unsupported and will be removed in GameDB 1.0.0. Use generated Load/Import with local JSON, or provide your own network transport. See Documentation~/runtime.md#intentionally-unsupported-surfaces.")]
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
