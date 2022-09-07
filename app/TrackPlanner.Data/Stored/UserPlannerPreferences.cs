using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Data.Stored
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
        
        public Angle CompactingAngleDeviation { get; set; } 
        public Length CompactingDistanceDeviation { get; set; } 
        public double HourlyStamina { get; set; }
        public bool UseStableRoads { get; set; }

        public TimeSpan JourneyStart { get; set; }
        public TimeSpan NextDayStart { get; set; }
        public TimeSpan CampLandingTime { get; set; }
        public TimeSpan HomeLandingTime { get; set; }
        public TimeSpan CheckpointIntervalLimit { get; set; }
        public Dictionary<string ,TimeSpan > Breaks { get; set; }
        public TimeSpan DefaultAnchorBreak { get; set; }
        
        private TripEvent[] tripEvents = Array.Empty<TripEvent>();
        public TripEvent[] TripEvents
        {
            get { return this.tripEvents; }
            set
            {
                // first go events without interval, because those should be processed at once
                // think about snack time (it has intervals) and resupply (it does not)
                // resupply has priority over snack time (you can buy banana for sure when you are doing big shopping
                // but you cannot squeeze big shopping while purchasing banana)
                this.tripEvents = value.OrderBy(it => it.Interval != null).ToArray();
            }
        }

        public UserPlannerPreferences()
        {
            this.DefaultAnchorBreak = TimeSpan.FromMinutes(10);
            this.Breaks = new Dictionary<string, TimeSpan>()
            {
                {"None", TimeSpan.Zero},
                {"Tiny", TimeSpan.FromMinutes(10)},
                {"Short", TimeSpan.FromMinutes(30)},
                {"Medium", TimeSpan.FromMinutes(60)},
                {"Long", TimeSpan.FromHours(2)},
                {"Epic", TimeSpan.FromHours(5)},
            };

            this.TripEvents = new[]
            {
                new TripEvent()
                {
                    Label ="tires",
                    ClassIcon  = "fas fa-tire",
                     Duration = TimeSpan.FromMinutes(15),
                     ClockTime = TimeSpan.Zero,
                     EveryDay = 14,
                     SkipAfterHome = true,
                },
                new TripEvent()
                {
                Label ="chain",
                ClassIcon  = "fas fa-link",
                Duration = TimeSpan.FromMinutes(7),
                ClockTime = TimeSpan.Zero,
                EveryDay = 2,
                SkipAfterHome = true,
                },
                new TripEvent()
                {
                    Label ="shower",
                    ClassIcon  = "fas fa-shower",
                    Duration = TimeSpan.FromMinutes(15),
                    EveryDay = 5,
                    ClockTime = TimeSpan.FromHours(12),
                    SkipAfterHome = true,
                    SkipBeforeHome = true,
                },
                new TripEvent()
                {
                    Label ="laundry",
                    ClassIcon  = "fas fa-tshirt",
                    Duration = TimeSpan.FromMinutes(30),
                    ClockTime = TimeSpan.FromHours(11),
                    SkipAfterHome = true,
                    SkipBeforeHome = true,
                },
                new TripEvent()
                {
                    Label ="lunch",
                    ClassIcon  = "fas fa-utensils",
                    Duration = TimeSpan.FromMinutes(15),
                    ClockTime = TimeSpan.FromHours(13),
                },
                new TripEvent()
                {
                    Label = "snacks",
                    Category = "shopping",
                    ClassIcon  = "fas fa-carrot",
                 Duration    = TimeSpan.FromMinutes(10),
                 Interval = TimeSpan.FromHours(2),
                },
                new TripEvent()
                {
                Label = "resupply",
                Category = "shopping",
                ClassIcon  = "fas fa-shopping-cart",
                Duration    = TimeSpan.FromMinutes(20),
                // the first resupply of the day (food for breakfast and supper) 
                ClockTime = null, // setting null to avoid starter of the day
                SkipAfterHome = true,
                SkipBeforeHome = true, // when hitting home we don't need this
                },
                new TripEvent()
                {
                    Label = "resupply",
                    Category = "shopping",
                    ClassIcon  = "fas fa-shopping-cart",
                    Duration    = TimeSpan.FromMinutes(20),
                    // last resupply for the day, usually water for the evening
                    ClockTime = TimeSpan.FromHours(24),
                    SkipBeforeHome = true,
                },
            };

            CheckpointIntervalLimit = TimeSpan.FromMinutes(100);
            CampLandingTime = TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(30));
            HomeLandingTime = TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(30));
            JourneyStart = TimeSpan.FromHours(9);
            this.NextDayStart = JourneyStart.Add(TimeSpan.FromMinutes(30));
            this.Speeds = new Dictionary<SpeedMode, Speed>();

            CompactingAngleDeviation = Angle.FromDegrees( 12);
            CompactingDistanceDeviation = Length.FromMeters( 15);
            HourlyStamina = 0.95;
        }

        public UserPlannerPreferences Complete()
        {
            var prefs = this;
            
            if (CompactingAngleDeviation < Angle.Zero || CompactingAngleDeviation >= (Angle.PI / 4))
                throw new ArgumentOutOfRangeException($"{nameof(CompactingAngleDeviation)} = {CompactingAngleDeviation}");
            if (CompactingDistanceDeviation<Length.Zero || CompactingDistanceDeviation.Meters>=100)
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


