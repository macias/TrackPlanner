using MathUnit;
using System;
using System.Collections.Generic;
using TrackPlanner.Data;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Settings
{
    public static class UserPlannerPreferencesHelper
    {
        public static UserPlannerPreferences CreateBikeOriented()
        {
            return new UserPlannerPreferences()
            {
                AddedMotorDangerousTrafficFactor = 0.80,
                AddedBikeFootHighTrafficFactor = 0.75,
                AddedMotorUncomfortableTrafficFactor = 0.2,
                TrafficSuppression = Length.FromKilometers(3),

                // this is actual help to avoid high-traffic road crossing, but works also well to avoid "shortcuts" while planning
                // without it program woud prefer to make "shortcut" (avoiding high-traffic) on short service road just to return to the main road after 30 meters or so
                JoiningHighTraffic = TimeSpan.FromMinutes(3),
            };
        }

        public static UserPlannerPreferences SetCustomSpeeds(this UserPlannerPreferences prefs)
        {
            prefs.Speeds = new Dictionary<SpeedMode, Speed>()
            {
                [SpeedMode.Asphalt] = MathUnit.Speed.FromKilometersPerHour(16.5),
                [SpeedMode.HardBlocks] = MathUnit.Speed.FromKilometersPerHour(7.5),

                [SpeedMode.Ground] = MathUnit.Speed.FromKilometersPerHour(13),
                [SpeedMode.Sand] = MathUnit.Speed.FromKilometersPerHour(6),

                [SpeedMode.CableFerry] = MathUnit.Speed.FromKilometersPerHour(4.5),

                [SpeedMode.CarryBike] = Speed.FromKilometersPerHour(1.6),
                [SpeedMode.Walk] = MathUnit.Speed.FromKilometersPerHour(5.5),
            };

            return prefs.Complete();
        }

        public static UserPlannerPreferences SetUniformSpeeds(this UserPlannerPreferences prefs)
        {
            foreach (var mode in Enum.GetValues<SpeedMode>())
            {
                prefs.Speeds[mode] = MathUnit.Speed.FromKilometersPerHour(13);
            }

            return prefs.Complete();
        }

    }

}