using SharpKml.Base;
using System;

// https://www.cloudsavvyit.com/12336/serving-dynamic-files-with-blazor-in-asp-net/

namespace TrackPlanner.DataExchange
{
    public sealed class KmlLineDecoration
    {
        public string Id { get; }
        public Color32 Color { get; }
        public double Width { get; }

        public KmlLineDecoration(Color32 color, double width)
        {
            Color = color;
            Width = width;
            Id = Guid.NewGuid().ToString();
        }
    }

}
