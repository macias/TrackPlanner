using MathUnit;
using System;

namespace TrackPlanner.Turner.Implementation

{
    internal readonly struct GraphPathWeight : IComparable<GraphPathWeight>
    {
        private static int DEBUG_COUNT = 0;
        public int DEBUG_ID { get; }

        private readonly Length anchorSnapDistance;
        private readonly Length snapDistance;
        private readonly Length motorDistance;
        private readonly Length cycleDistance;
        private readonly double angles;
        private readonly int roadSwitches;
        private readonly double roadDiffLevels2;
        private readonly int cycleCrossings;
        private readonly string DEBUG_INFO;

        public double TotalWeight
        {
            get
            {
                /*            return
                                this.travelDistance.Meters
                                + 
                                this.cycleDistance.Meters*1.05
                                +
                                this.snapDistance.Meters * 0.025
                                +
                                roadSwitches * 0
                                ;*/
                // we add penalty for using bicycle path or footpath in order to simplify routing when there are bike/foot-paths along the roads, and they constantly switches on road sides
                return
                    this.motorDistance.Meters
                    +
                    cycleDistanceWeight
                    +
                    // currently anchor snap distance leaks here as well, because first/last nodes have the same snap distance as the crosspoints
                    this.snapDistance.Meters * 0//0.025
                    +
                    // not counting start/end snap distance at all is a mistake, because program can make crazy jump to same weird location in order to save the distance made by that jump
                    anchorSnapWeight
                    +
                    roadSwitchesWeight//
                    +
                    roadDiffLevelsWeight
                    +
                    Math.Sqrt(angles) * 0// 0.01 // 0.001 was ok
                    +
                    cycleCrossingsWeight
                    ;

                // mozna jeszcze by doliczac kary za powroty na juz uzywane drogi
            }
        }

        private int cycleCrossingsWeight => cycleCrossings * 15;
        private int roadSwitchesWeight => roadSwitches * 80;
        private double roadDiffLevelsWeight => Math.Sqrt(roadDiffLevels2) * 0;
        private double anchorSnapWeight => this.anchorSnapDistance.Meters * 0.5;
        private double cycleDistanceWeight => this.cycleDistance.Meters * 1;//1.05;

        private GraphPathWeight(Length anchorSnapDistance, Length snapDistance, Length motorDistance, Length cycleDistance, double angles, int switches, 
            double roadDiffLevels2, int cycleCrossings, string debugInfo)
        {
            if (cycleCrossings < 0)
                throw new ArgumentOutOfRangeException($"{nameof(cycleCrossings)} {cycleCrossings}");

            this.anchorSnapDistance = anchorSnapDistance;
            this.snapDistance = snapDistance;
            this.motorDistance = motorDistance;
            this.cycleDistance = cycleDistance;
            this.angles = angles;
            this.roadSwitches = switches;
            this.roadDiffLevels2 = roadDiffLevels2;
            this.cycleCrossings = cycleCrossings;
            this.DEBUG_ID = ++DEBUG_COUNT;
            this.DEBUG_INFO = debugInfo;
        }

        internal GraphPathWeight(bool isAnchor, Length snapDistance, Length travelDistance, Angle angle,
            bool isMotorRoad, bool isRoadSwitch, int roadDiffLevels, int cycleCrossings, string debugInfo)
        {
            if (cycleCrossings < 0)
                throw new ArgumentOutOfRangeException($"{nameof(cycleCrossings)} {cycleCrossings}");

            angle = angle <= Angle.PI ? angle : Angle.FullCircle - angle;
            angle = Angle.PI - angle;

            this.anchorSnapDistance = snapDistance * (isAnchor ? 1 : 0);
            this.snapDistance = snapDistance;
            this.motorDistance = travelDistance * (isMotorRoad ? 1 : 0);
            this.cycleDistance = travelDistance * (isMotorRoad ? 0 : 1);
            this.angles = Math.Pow(angle.Degrees, 2);
            this.roadSwitches = (isRoadSwitch ? 1 : 0);
            this.roadDiffLevels2 = Math.Pow(roadDiffLevels, 2);
            this.cycleCrossings = cycleCrossings;
            this.DEBUG_INFO = debugInfo;
            this.DEBUG_ID = ++DEBUG_COUNT;
        }

        public int CompareTo(GraphPathWeight other)
        {
            return this.TotalWeight.CompareTo(other.TotalWeight);
        }

        internal GraphPathWeight Add(in GraphPathWeight other)
        {
            return new GraphPathWeight(
                this.anchorSnapDistance + other.anchorSnapDistance,
                this.snapDistance + other.snapDistance,
                motorDistance: this.motorDistance + other.motorDistance,
                cycleDistance: this.cycleDistance + other.cycleDistance,
                angles + other.angles,
                this.roadSwitches + other.roadSwitches,
                roadDiffLevels2: this.roadDiffLevels2 + other.roadDiffLevels2,
                this.cycleCrossings + other.cycleCrossings,
                other.DEBUG_INFO
                );
        }

        public override string ToString()
        {
            return $"{TotalWeight} = road {(motorDistance.Meters.ToString("0.##"))}, bike {(cycleDistance.Meters.ToString("0.##"))}({(cycleDistanceWeight.ToString("0.##"))}), anchor {(this.anchorSnapDistance.Meters.ToString("0.##"))}({(anchorSnapWeight.ToString("0.##"))}), snap {(snapDistance.Meters.ToString("0.##"))}, switch {roadSwitches}({(roadSwitchesWeight.ToString("0.##"))}), levels {(roadDiffLevels2.ToString("0"))}({(roadDiffLevelsWeight.ToString("0.##"))}), cross {cycleCrossings}({(cycleCrossingsWeight.ToString("0.##"))}), angle {(Math.Sqrt(angles).ToString("0.##"))} : {DEBUG_INFO}";
        }
    }
}