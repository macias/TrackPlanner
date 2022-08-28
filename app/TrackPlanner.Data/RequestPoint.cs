using System.Collections.Generic;
using System.Linq;
using Geo;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public record struct RequestPoint
    {
        public GeoPoint UserPoint { get; }
        public bool AllowSmoothing { get; }

        public RequestPoint(GeoPoint userPoint, bool allowSmoothing)
        {
            UserPoint = userPoint;
            this.AllowSmoothing = allowSmoothing;
        }
    }
    }
