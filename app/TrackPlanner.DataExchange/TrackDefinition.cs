using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Shared;

namespace TrackPlanner.DataExchange
{
    public class TrackDefinition
    {
        public List<LineDefinition> Lines { get; set; }
        public List<WaypointDefinition> Waypoints { get; set; }

        public bool IsEmpty => this.Lines.Count == 0 && this.Waypoints.Count == 0;
        
        public TrackDefinition()
        {
            this.Lines = new List<LineDefinition>();
            this.Waypoints = new List<WaypointDefinition>();
        }

        public void AddLine(IEnumerable<GeoZPoint> points, string? name = null, KmlLineDecoration? style = null)
        {
            this.Lines.Add(new LineDefinition(points.ToArray(), name, description: null, style));
        }

        public void AddPoint(GeoZPoint point, string? label = null, string? comment = null, PointIcon? icon = null)
        {
            this.Waypoints.Add(new WaypointDefinition(point, label, comment, icon));
        }

        public void AddTurns(IEnumerable<TurnInfo>? turns,PointIcon? icon = null )
        {
            this.Waypoints.AddRange((turns ?? Enumerable.Empty<TurnInfo>())
                .Select(it => new WaypointDefinition(it.Point, it.GetLabel(), description: it.Reason,icon?? PointIcon.DotIcon)));
        }

    }

}