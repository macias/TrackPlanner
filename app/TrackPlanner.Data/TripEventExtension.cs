using System;

namespace TrackPlanner.Data
{
    public static class TripEventExtension
    {
        public static string GetLabel(this TripEvent tripEvent)
        {
            switch (tripEvent)
            {
                case TripEvent.Resupply: return "resupply";
                case TripEvent.SnackTime: return "snacks";
                default: throw new ArgumentOutOfRangeException(tripEvent.ToString());
            }
        }
        
        public static string GetClassIcon(this TripEvent tripEvent)
        {
            return  "fas fa-" + tripEvent switch{
                TripEvent.Resupply=>"shopping-cart",
                TripEvent.SnackTime=>"carrot",     
                _ => throw new ArgumentOutOfRangeException(tripEvent.ToString()),
            };
        }
    }
}
