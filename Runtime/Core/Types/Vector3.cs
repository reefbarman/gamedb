
using System;

namespace GameDBLibrary
{
    public class Vector3
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = Convert.ToSingle(aParts[0]);
            y = Convert.ToSingle(aParts[1]);
            z = Convert.ToSingle(aParts[2]);
        }

        public override string ToString()
        {
            return $"{x},{y},{z}";
        }
    }
}
