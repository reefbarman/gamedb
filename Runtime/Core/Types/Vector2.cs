
using System;

namespace GameDBLibrary
{
    public class Vector2
    {
        public float x { get; set; }
        public float y { get; set; }

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = Convert.ToSingle(aParts[0]);
            y = Convert.ToSingle(aParts[1]);
        }

        public override string ToString()
        {
            return $"{x},{y}";
        }
    }
}
