using SharpKml.Dom;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.DataExchange
{
    public readonly record struct LineDefinition
    {
        public IReadOnlyList<GeoZPoint> Points { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public KmlLineDecoration? Style { get; init; }

        public LineDefinition(IReadOnlyList<GeoZPoint> points, string? name, string? description, KmlLineDecoration? style)
        {
            this.Points = points;
            this.Name = name;
            this.Style = style;
            this.Description = description;
        }
        
        public void Deconstruct(out IReadOnlyList<GeoZPoint> points, out string? name, out string? description, out KmlLineDecoration? style)
        {
            points = this.Points;
            name = this.Name;
            description = this.Description;
            style = this.Style;
        }
    }
}
