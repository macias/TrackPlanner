using SharpKml.Base;

// https://www.cloudsavvyit.com/12336/serving-dynamic-files-with-blazor-in-asp-net/

namespace TrackPlanner.DataExchange
{
    public sealed class PointIcon
    {
        public static PointIcon DotIcon { get; }
        public static PointIcon CircleIcon { get; }
        public static PointIcon ParkingIcon { get; }
        public static PointIcon StarIcon { get; }

        static PointIcon()
        {
            DotIcon = new PointIcon("icon-1739-DB4436-nodesc", new Color32(0xff, 0x36, 0x44, 0xdb), "https://www.gstatic.com/mapspro/images/stock/503-wht-blank_maps.png");
            CircleIcon = new PointIcon("icon-1499-0288D1-nodesc", new Color32(0xff, 0xd1, 0x88, 0x02), "https://www.gstatic.com/mapspro/images/stock/503-wht-blank_maps.png");
            ParkingIcon = new PointIcon("icon-1644-0288D1-nodesc", new Color32(0xff, 0xd1, 0x88, 0x02), "https://www.gstatic.com/mapspro/images/stock/503-wht-blank_maps.png");
            StarIcon = new PointIcon("icon-1502-C2185B-nodesc", new Color32(0xff, 0x5b, 0x18, 0xc2), "https://www.gstatic.com/mapspro/images/stock/503-wht-blank_maps.png");
        }

        public string Id { get; }
        public Color32 Color { get; }
        public string ImageUrl { get; }

        public PointIcon(string id, Color32 color, string imageUrl)
        {
            Id = id;
            Color = color;
            ImageUrl = imageUrl;
        }
    }

}
