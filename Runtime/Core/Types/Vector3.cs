
using System;
using System.Globalization;

namespace GameDBLibrary
{
    public class Vector3
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public Vector3(float x, float y, float z)
        {
            this.x = RequireFinite(x);
            this.y = RequireFinite(y);
            this.z = RequireFinite(z);
        }

        public Vector3(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = ParseComponent(aParts[0]);
            y = ParseComponent(aParts[1]);
            z = ParseComponent(aParts[2]);
        }

        public override string ToString()
        {
            return string.Join(",",
                RequireFinite(x).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(y).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(z).ToString("R", CultureInfo.InvariantCulture));
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
