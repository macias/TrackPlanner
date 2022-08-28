using System.Collections.Generic;
using System.Globalization;

namespace TrackPlanner.Settings
{
    public sealed class LineDecoration
    {
        public string AbgrColor { get; set; }
        public int Width { get; set; }
        public string Label { get; set; }
        
        public LineDecoration()
        {
            Width = 1;
            AbgrColor = "";
            Label = "";
        }
        
        public int GetAbgrColor()
        {
            string color = this.AbgrColor.ToLowerInvariant();
            if (color.StartsWith("0x"))
            {
                if (int.TryParse(color.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int abgr))
                    return abgr;
            }
            else if (int.TryParse(color, NumberStyles.Integer, CultureInfo.InvariantCulture, out int abgr))
                return abgr;

            return 0xff << 24;
        }

        public void GetComponents(out int a, out int r, out int g, out int b)
        {
            int abgr = GetAbgrColor();
            a = abgr >> 24;
            b = (abgr >> 16) & 0xff;
            g = (abgr >> 8) & 0xff;
            r = abgr & 0xff;
        }

        public int GetArgbColor()
        {
            GetComponents(out int a, out int r, out int g, out int b);
         
            int argb = (a << 24)
                       | (r << 16)
                       | (g << 8)
                       | b
                ;

            return argb;
        }

    }
    
}

