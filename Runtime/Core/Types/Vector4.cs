
using System;
using System.Globalization;

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
            this.x = RequireFinite(x);
            this.y = RequireFinite(y);
            this.z = RequireFinite(z);
            this.w = RequireFinite(w);
        }

        public Vector4(string vecStr)
        {
            var aParts = vecStr.Split(',');

            x = ParseComponent(aParts[0]);
            y = ParseComponent(aParts[1]);
            z = ParseComponent(aParts[2]);
            w = ParseComponent(aParts[3]);
        }

        public override string ToString()
        {
            return string.Join(",",
                RequireFinite(x).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(y).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(z).ToString("R", CultureInfo.InvariantCulture),
                RequireFinite(w).ToString("R", CultureInfo.InvariantCulture));
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
