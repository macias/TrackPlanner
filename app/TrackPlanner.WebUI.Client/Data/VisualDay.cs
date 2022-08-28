using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client.Data
{
    public sealed class VisualDay<TAnchorVisual,TDayVisual> : IDay<VisualAnchor<TAnchorVisual>>
    {
        private bool isModified;
        public bool IsModified {
            get
            {
                if (!this.isModified && this.Anchors.Any(it => it.IsModified))
                    this.isModified = true;
                return this.isModified;
            }
            set
            {
                this.isModified = value;
                if (!value)
                    foreach (var anchor in Anchors)
                        anchor.IsModified = false;
                {
                        
                }
            }
        }
        
        private static int DEBUG_idCounter;
        private readonly int DEBUG_id = DEBUG_idCounter++;
        
        private TDayVisual debugVisual;
        public TDayVisual Visual
        {
            get { return this.debugVisual; }
            set
            {
                Console.WriteLine($"DEBUG Setting Visual for day id {this.DEBUG_id} to {value}");
                this.debugVisual = value;
            }
        }

        public List<VisualAnchor<TAnchorVisual>> Anchors { get;  }
        IReadOnlyList<IReadOnlyAnchor> IReadOnlyDay.Anchors => this.Anchors;
        
        private TimeSpan start;
        public TimeSpan Start
        {
            get { return this.start; }
            set
            {
                this.IsModified = true;
                this.start = value;
            }
        }
        
        public VisualDay() 
        {
            this.Anchors = new List<VisualAnchor<TAnchorVisual>>();
            this.debugVisual= default!;
        }
        public VisualDay(TimeSpan start,List<VisualAnchor<TAnchorVisual>> anchors)
        {
            this.Start = start;
            this.Anchors = anchors;
                this.debugVisual= default!;
        }

        public void DEBUG_SetVisual(TDayVisual visual)
        {
            this.debugVisual = visual;
        }
    }
}