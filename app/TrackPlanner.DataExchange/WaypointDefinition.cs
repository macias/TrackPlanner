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
    public readonly record struct WaypointDefinition
    {
        public GeoZPoint Point { get; }
        public string? Name { get; }
        public string? Description { get; }
        public PointIcon? Icon { get; }

        public WaypointDefinition(GeoZPoint point, string? name, string? description, PointIcon? icon)
        {
            this.Point = point;
            this.Name = name;
            this.Description = description;
            this.Icon = icon;
        }
        
        public void Deconstruct(out GeoZPoint point, out string? name, out string? description, out PointIcon? icon)
        {
            point = Point;
            name = Name;
            description = Description;
            icon = Icon;
        }

    }

    
}
