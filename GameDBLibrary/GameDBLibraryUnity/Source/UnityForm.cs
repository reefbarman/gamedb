using GameDBLibrary;
using UnityEngine;

namespace GameDBLibraryUnity
{
    public class UnityForm : IForm
    {
        private readonly WWWForm _form;

        public WWWForm Form => _form;

        public UnityForm()
        {
            _form = new WWWForm();
        }

        public void AddField(string key, string value)
        {
            Form.AddField(key, value);
        }

        public void AddBinaryData(string key, byte[] data, string fileName)
        {
            Form.AddBinaryData(key, data, fileName);
        }
    }
}
