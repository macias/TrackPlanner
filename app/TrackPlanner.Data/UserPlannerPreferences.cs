using MathUnit;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace TrackPlanner.Data
{
    public sealed class UserPlannerPreferences
    {
        //[JsonConverter(typeof(CustomDictionaryConverter<SpeedMode, Speed>))]
        public Dictionary<SpeedMode, Speed> Speeds { get; set; }

        public double AddedMotorDangerousTrafficFactor { get; set; }
        public double AddedMotorUncomfortableTrafficFactor { get; set; }
        
        public double AddedBikeFootHighTrafficFactor { get; set; }
        // if the user places out middle point (not start/end) on some highway assume before and after user is comfy with it
        public Length TrafficSuppression { get; set; }

        public TimeSpan JoiningHighTraffic { get; set; }
        public bool HACK_ExactToTarget { get; set; }
        
        public double CompactingAngleDeviation { get; set; } // degrees
        public double CompactingDistanceDeviation { get; set; } // meters
        public double HourlyStamina { get; set; }
        public bool UseStableRoads { get; set; }

        public TimeSpan JourneyStart { get; set; }
        public TimeSpan NextDayStart { get; set; }
        public Dictionary<TripEvent,TimeSpan> EventDuration { get; set; }
        public TimeSpan ShoppingInterval { get; set; }
        public TimeSpan CampLandingTime { get; set; }
        public TimeSpan HomeLandingTime { get; set; }
        public TimeSpan CheckpointIntervalLimit { get; set; }
        public Dictionary<string ,TimeSpan > Breaks { get; set; }
        public TimeSpan DefaultAnchorBreak { get; set; }
        public UserTripEvent[] UserEvents { get; set; }

        public UserPlannerPreferences()
        {
            this.DefaultAnchorBreak = TimeSpan.FromMinutes(10);
            // todo: change it to dictionary
            this.Breaks = new Dictionary<string, TimeSpan>()
            {
                {"None", TimeSpan.Zero},
                {"Tiny", TimeSpan.FromMinutes(10)},
                {"Short", TimeSpan.FromMinutes(30)},
                {"Medium", TimeSpan.FromMinutes(60)},
                {"Long", TimeSpan.FromHours(2)},
                {"Epic", TimeSpan.FromHours(5)},
            };

            this.UserEvents = new[]
            {
                new UserTripEvent()
                {
                    Label ="tires",
                    ClassIcon  = "fas fa-tire",
                     Duration = TimeSpan.FromMinutes(15),
                     EveryDay = 14,
                },
                new UserTripEvent()
                {
                Label ="chain",
                ClassIcon  = "fas fa-link",
                Duration = TimeSpan.FromMinutes(7),
                EveryDay = 2,
                },
                new UserTripEvent()
                {
                    Label ="shower",
                    ClassIcon  = "fas fa-shower",
                    Duration = TimeSpan.FromMinutes(15),
                    EveryDay = 5,
                    Opportunity = TimeSpan.FromHours(12),
                },
                new UserTripEvent()
                {
                    Label ="laundry",
                    ClassIcon  = "fas fa-tint",
                    Duration = TimeSpan.FromMinutes(30),
                    Opportunity = TimeSpan.FromHours(11),
                },
                new UserTripEvent()
                {
                    Label ="lunch",
                    ClassIcon  = "fas fa-utensils",
                    Duration = TimeSpan.FromMinutes(15),
                    Opportunity = TimeSpan.FromHours(13),
                },
            };

            CheckpointIntervalLimit = TimeSpan.FromMinutes(100);
            CampLandingTime = TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(30));
            HomeLandingTime = TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(30));
            JourneyStart = TimeSpan.FromHours(9);
            this.NextDayStart = JourneyStart.Add(TimeSpan.FromMinutes(30));
            this.Speeds = new Dictionary<SpeedMode, Speed>();
            this.ShoppingInterval = TimeSpan.FromHours(2);

            this.EventDuration = new Dictionary<TripEvent, TimeSpan>()
            {
                {TripEvent.Resupply, TimeSpan.FromMinutes(20)},
                {TripEvent.SnackTime, TimeSpan.FromMinutes(10)},
            };

            CompactingAngleDeviation = 12;
            CompactingDistanceDeviation = 15;
            HourlyStamina = 0.95;
        }

        public UserPlannerPreferences Complete()
        {
            var prefs = this;
            
            if (CompactingAngleDeviation < 0 || CompactingAngleDeviation >= (Angle.PI / 4).Degrees)
                throw new ArgumentOutOfRangeException($"{nameof(CompactingAngleDeviation)} = {CompactingAngleDeviation}");
            if (CompactingDistanceDeviation<0 || CompactingDistanceDeviation>=100)
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
        
        // it makes not sense riding at lower speeds than carrying bike
        public Speed GetLowRidingSpeedLimit()
        {
            return this.Speeds[SpeedMode.CarryBike];
        }
    }
}


