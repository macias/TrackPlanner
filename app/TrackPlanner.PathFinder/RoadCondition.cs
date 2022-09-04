using TrackPlanner.Data.Stored;
using System;
using TrackPlanner.Data;
using TrackPlanner.Settings;

namespace TrackPlanner.PathFinder
{
    public readonly struct RoadCondition : IEquatable<RoadCondition>
    {
        [Flags]
        private enum FragnentMode
        {
            None = 0,
            Snap = 1, // AKA magnet, between user point and cross point on actual road
            Forbidden = 2,
        }

        public SpeedMode Mode { get; }
        public Risk Risk { get; }
        public bool IsForbidden => this.fragment.HasFlag(FragnentMode.Forbidden);
        public bool IsSnap => this.fragment.HasFlag(FragnentMode.Snap);
        private readonly FragnentMode fragment;

        public RoadCondition(SpeedMode speedMode, Risk risk, bool isForbidden, bool isSnap)
        {
            Mode = speedMode;
            Risk = risk;
            this.fragment = (isForbidden ? FragnentMode.Forbidden : FragnentMode.None)
                            | (isSnap ? FragnentMode.Snap : FragnentMode.None);
        }

        public void Deconstruct(out SpeedMode mode, out Risk risk, out bool isForbidden,out bool isSnap)
        {
            risk = this.Risk;
            mode = this.Mode;
            isForbidden = this.IsForbidden;
            isSnap = this.IsSnap;
        }

        public override bool Equals(object? obj)
        {
            if (obj is RoadCondition other)
                return Equals(other);
            else
                return false;
        }

        public bool Equals(RoadCondition other)
        {
            return Mode == other.Mode 
                   && Risk == other.Risk
                   && fragment == other.fragment;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mode, fragment,this.Risk);
        }

        public static bool operator ==(RoadCondition left, RoadCondition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoadCondition left, RoadCondition right)
        {
            return !(left == right);
        }
    }
}