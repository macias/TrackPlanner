using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;

namespace TrackPlanner.Data
{
    public static class UnitLinqer
    {
        public static TimeSpan Sum(this IEnumerable<TimeSpan> enumerable)
        {
                return enumerable.Aggregate(TimeSpan.Zero, (acc, it) => acc + it);

        }
        public static Length Sum(this IEnumerable<Length> enumerable)
        {
            return enumerable.Aggregate(Length.Zero, (acc, it) => acc + it);

        }
        public static Speed Sum(this IEnumerable<Speed> enumerable)
        {
            return enumerable.Aggregate(Speed.Zero, (acc, it) => acc + it);

        }
    }
}