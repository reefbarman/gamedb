
namespace GameDBLibrary
{
    public class Color
    {
        public byte r { get; set; }
        public byte g { get; set; }
        public byte b { get; set; }
        public byte a { get; set; }

        public string Hex
        {
            get => $"#{r:X2}{g:X2}{b:X2}" + (a != 255 ? a.ToString("X2") : "");

            set
            {
                var hex = value.Replace("0x", "");  //in case the string is formatted 0xFFFFFF
                hex = hex.Replace("#", "");         //in case the string is formatted #FFFFFF
                a = 255;                            //assume fully visible unless specified in hex

                r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                //Only use alpha if the string has enough characters
                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                }
            }
        }

        public Color(string hex)
        {
            Hex = hex;
        }

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public override string ToString()
        {
            return Hex;
        }
    }
}
