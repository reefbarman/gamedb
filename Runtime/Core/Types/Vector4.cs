
using System;

namespace GameDBLibrary
{
    public class Vector4
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float w { get; set; }

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vector4(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = Convert.ToSingle(aParts[0]);
            y = Convert.ToSingle(aParts[1]);
            z = Convert.ToSingle(aParts[2]);
            w = Convert.ToSingle(aParts[3]);
        }

        public override string ToString()
        {
            return $"{x},{y},{z},{w}";
        }
    }
}
