using System;
using BlazorLeaflet.Models;
using Geo;
using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client.Data
{
    public sealed class VisualAnchor<TVisual> : IAnchor
    {
        public bool IsModified { get; set; }
        
        public TVisual Visual { get; set; }

        private TimeSpan _break;
        public TimeSpan Break
        {
            get { return this._break; }
            set
            {
                this.IsModified = true;
                this._break = value;
            }
        }

        private string label;
        public string Label
        {
            get { return this.label; }
            set
            {
                    this.IsModified= true;
                this.label = value;
            }
        }
        
        private bool isPinned;
        public bool IsPinned
        {
            get { return this.isPinned; }
            set
            {
                this.IsModified = true;
                this.isPinned = value;
            }
        }

        public VisualAnchor(TVisual visual)
        {
            Visual = visual;
            label = "";
        }

    }

    public static class VisualAnchorExtension
    {
        public static ScheduleAnchor ToScheduleAnchor(this VisualAnchor<Marker> source)
        {
            return new ScheduleAnchor()
            {
                Label = source.Label,
                Break = source.Break,
                IsPinned = source.IsPinned,
                UserPoint = GeoPoint.FromDegrees(source.Visual.Position.Lat, source.Visual.Position.Lng),
            };
        }

        public static VisualAnchor<Marker> ToVisualAnchor(this ScheduleAnchor source,Marker marker)
        {
            return new VisualAnchor<Marker>(marker)
            {
                IsPinned = source.IsPinned,
                Break = source.Break, 
                Label = source.Label
            };
        }
    }
}