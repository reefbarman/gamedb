using System.Collections.Generic;
using System.Text;

namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    class PostForm : IForm
    {
        public enum ParamType
        {
            Field,
            File
        }

        public class Param
        {
            public Param(string name, string value, ParamType type)
            {
                Name = name;
                Value = value;
                Type = type;
            }

            public Param(string name, string filename, string value, ParamType type)
            {
                Name = name;
                Value = value;
                FileName = filename;
                Type = type;
            }

            public string Name;
            public string FileName;
            public string Value;
            public ParamType Type;
        }

        // Change this if you need to, not necessary
        public static string Boundary = "AaB03x";

        private readonly List<Param> _params = new List<Param>();

        public string GetPostData()
        {
            var sb = new StringBuilder();
            foreach (var p in _params)
            {
                sb.AppendLine("--" + Boundary);

                if (p.Type == ParamType.File)
                {
                    sb.AppendLine($"Content-Disposition: file; name=\"{p.Name}\"; filename=\"{p.FileName}\"");
                    sb.AppendLine("Content-Type: application/octet-stream");
                    sb.AppendLine();
                    sb.AppendLine(p.Value);
                }
                else
                {
                    sb.AppendLine($"Content-Disposition: form-data; name=\"{p.Name}\"");
                    sb.AppendLine();
                    sb.AppendLine(p.Value);
                }
            }

            sb.AppendLine("--" + Boundary + "--");

            return sb.ToString();
        }

        public void AddField(string key, string value)
        {
            _params.Add(new Param(key, value, ParamType.Field));
        }

        public void AddBinaryData(string key, byte[] data, string fileName)
        {
            _params.Add(new Param(key, fileName, Encoding.UTF8.GetString(data), ParamType.File));
        }
    }
}
