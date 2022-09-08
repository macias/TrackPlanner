using MathUnit;
using System;
using System.Collections.Generic;

namespace TrackPlanner.Data.Stored
{
    public sealed class UserRouterPreferences
    {
        public Dictionary<SpeedMode, Speed> Speeds { get; set; }
        public double AddedMotorDangerousTrafficFactor { get; set; }
        public double AddedMotorUncomfortableTrafficFactor { get; set; }

        public double AddedBikeFootHighTrafficFactor { get; set; }

        // if the user places out middle point (not start/end) on some highway assume before and after user is comfy with it
        public Length TrafficSuppression { get; set; }
        public TimeSpan JoiningHighTraffic { get; set; }
        public bool HACK_ExactToTarget { get; set; }

        public Angle CompactingAngleDeviation { get; set; }
        public Length CompactingDistanceDeviation { get; set; }
        public bool UseStableRoads { get; set; }
        public TimeSpan CheckpointIntervalLimit { get; set; }
        public TimeSpan RoadSwitching { get; set; }

        public UserRouterPreferences()
        {
            this.Speeds = new Dictionary<SpeedMode, Speed>();
            CheckpointIntervalLimit = TimeSpan.FromMinutes(100);

            CompactingAngleDeviation = Angle.FromDegrees(12);
            CompactingDistanceDeviation = Length.FromMeters(15);
            RoadSwitching = TimeSpan.FromSeconds(18); // a little something, but still we prefer continuous ride
        }

        // it makes not sense riding at lower speeds than carrying bike
        public Speed GetLowRidingSpeedLimit()
        {
            return this.Speeds[SpeedMode.CarryBike];
        }

        public UserRouterPreferences Complete()
        {
            var prefs = this;

            if (CompactingAngleDeviation < Angle.Zero || CompactingAngleDeviation >= (Angle.PI / 4))
                throw new ArgumentOutOfRangeException($"{nameof(CompactingAngleDeviation)} = {CompactingAngleDeviation}");
            if (CompactingDistanceDeviation < Length.Zero || CompactingDistanceDeviation.Meters >= 100)
                throw new ArgumentOutOfRangeException($"{nameof(CompactingDistanceDeviation)} = {CompactingDistanceDeviation}");
            if (!prefs.Speeds.ContainsKey(SpeedMode.Paved))
                prefs.Speeds[SpeedMode.Paved] = prefs.Speeds[SpeedMode.HardBlocks];
            if (!prefs.Speeds.ContainsKey(SpeedMode.Unknown))
                prefs.Speeds[SpeedMode.Unknown] = prefs.Speeds[SpeedMode.Sand];
            if (!prefs.Speeds.ContainsKey(SpeedMode.UrbanSidewalk))
                // we expect asphalt or paving stones, so the ride should be good, but on the other hand we cannot go fast because of the people
                prefs.Speeds[SpeedMode.UrbanSidewalk] = prefs.Speeds[SpeedMode.Ground];

            return this;
        }
    }
}