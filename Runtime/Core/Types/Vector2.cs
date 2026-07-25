
using System;
using System.Globalization;

namespace GameDBLibrary
{
    public class Vector2
    {
        public float x { get; set; }
        public float y { get; set; }

        public Vector2(float x, float y)
        {
            this.x = RequireFinite(x);
            this.y = RequireFinite(y);
        }

        public Vector2(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = ParseComponent(aParts[0]);
            y = ParseComponent(aParts[1]);
        }

        public override string ToString()
        {
            return string.Join(",",
                RequireFinite(x).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(y).ToString("R", CultureInfo.InvariantCulture));
        }

        private static float ParseComponent(string value)
        {
            return RequireFinite(float.Parse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture));
        }

        private static float RequireFinite(float component)
        {
            if (float.IsNaN(component) || float.IsInfinity(component))
            {
                throw new FormatException("Vector components must be finite.");
            }
            return component;
        }
    }
}
