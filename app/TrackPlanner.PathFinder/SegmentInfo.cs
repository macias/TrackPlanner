using TrackPlanner.Data.Stored;
using MathUnit;
using System;
using TrackPlanner.Data;

namespace TrackPlanner.PathFinder
{
    public readonly record struct SegmentInfo
    {
        public static SegmentInfo Create(Length segmentLength, bool isForbidden, bool isStable, bool isSnap)
        {
            return new SegmentInfo()
            {
                IsForbidden = isForbidden,
                UnstableLength = !isStable && !isSnap?segmentLength: Length.Zero,
                IsSnap = isSnap,
                SegmentLength = segmentLength,
                ForbiddenLength = isForbidden && !isSnap ? segmentLength : Length.Zero,
            };
        }

        public Length UnstableLength { get; init; }
        public bool IsForbidden { get; private init; }
        public SpeedMode SpeedMode { get; private init; }
        public Risk RiskInfo { get; init; }
        public bool IsSnap { get; private init; }
        public TravelCost Cost { get; init; }
        public TimeSpan Time { get; init; }
        public Length SegmentLength { get; private init; }
        public Length ForbiddenLength { get; private init;}

        
        public SegmentInfo ExtendWith(SegmentInfo extension)
        {
            return extension with
            {
                Cost = this.Cost + extension.Cost,
                Time = this.Time + extension.Time,
                SegmentLength = this.SegmentLength + extension.SegmentLength,
                ForbiddenLength = this.ForbiddenLength+extension.ForbiddenLength,
                UnstableLength = this.UnstableLength+extension.UnstableLength,
            };
        }

        public SegmentInfo WithSpeedMode(SpeedMode mode)
        {
            return this with {SpeedMode = mode};
        }
    }
}