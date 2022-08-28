using System;
using MathUnit;

namespace TrackPlanner.Data
{
    public static class DataHelper
    {
        public static TimeSpan CalcTrueTime(TimeSpan rollingTime, Length distance, TimeSpan rawTime, Speed lowSpeedLimit, double hourlyStamina)
        {
            if (distance==Length.Zero)
                return TimeSpan.Zero;
            
            var raw_speed = distance / rawTime;

            var start_speed = raw_speed * Math.Pow(hourlyStamina, rollingTime.TotalHours);
            // todo: from here math is bad, fix it
            var final_speed = raw_speed * Math.Pow(hourlyStamina, rollingTime.TotalHours + rawTime.TotalHours);

            final_speed = final_speed.Max( lowSpeedLimit); // don't allow absurdly low values
            
            var avg_speed = (start_speed + final_speed) / 2;

            TimeSpan true_time;
            try
            {
                true_time = distance / avg_speed;
            }
            catch
            {
                Console.WriteLine($"True time failed, distance {distance.Meters}, avg_speed {avg_speed.MetersPerSecond}");
                throw;
            }

            return true_time;
        }

    }
}
