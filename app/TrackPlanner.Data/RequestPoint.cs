using System.Collections.Generic;
using System.Linq;
using Geo;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public record struct RequestPoint
    {
        public GeoPoint UserPoint { get; init;}
        public bool AllowSmoothing { get; init; }

        public RequestPoint(GeoPoint userPoint, bool allowSmoothing)
        {
            UserPoint = userPoint;
            this.AllowSmoothing = allowSmoothing;
        }
    }
    }
