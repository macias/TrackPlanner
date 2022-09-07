using System;
using MathUnit;

namespace TrackPlanner.Data
{
    public static class DataFormat
    {
        public static string FormatEvent(int count, TimeSpan duration)
        {
            return $"{Format(duration)}{(count==1?"":$" ({count})")}";
        }
        public static string Format(TimeSpan time)
        {
            return time.ToString(@"hh\:mm");
        }
        public static string Format(Angle angle)
        {
            return angle.Degrees.ToString("0")+"°";
        }
        public static string Format(Length distance,bool withUnit)
        {
            return distance.Kilometers.ToString("0.#")+(withUnit?"km":"");
        }
        public static string Format(Speed speed,bool withUnit)
        {
            return speed.KilometersPerHour.ToString("0.#")+(withUnit?"km/h":"");
        }

        public static string Adjust(int number, int range)
        {
            if (number > range)
                throw new ArgumentOutOfRangeException($"{nameof(number)}={number}, {nameof(range)}={range}.");
            int total_width =1+(range==0? 0: (int)Math.Floor( Math.Log(range, 10)));
            return number.ToString().PadLeft(total_width, '0');
        }
    }

}