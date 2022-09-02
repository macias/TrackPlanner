using System;

namespace TrackPlanner.Data
{
    public static class TripEventExtension
    {
        public static string GetLabel(this TripEvent tripEvent)
        {
            switch (tripEvent)
            {
                case TripEvent.Laundry: return "laundry";
                case TripEvent.Lunch: return "lunch";
                case TripEvent.Resupply: return "resupply";
                case TripEvent.SnackTime: return "snacks";
                case TripEvent.Maintenance: return "maintenance";
                case TripEvent.Shower: return "shower";
                default: throw new ArgumentOutOfRangeException(tripEvent.ToString());
            }
        }
        
        public static string GetClassIcon(this TripEvent tripEvent)
        {
            return  "fas fa-" + tripEvent switch{
                TripEvent.Laundry=>"tint",
                TripEvent.Lunch=>"utensils",
                TripEvent.Resupply=>"shopping-cart",
                TripEvent.SnackTime=>"carrot",     
                TripEvent.Maintenance=>"wrench",
                TripEvent.Shower=>"shower",
                _ => throw new ArgumentOutOfRangeException(tripEvent.ToString()),
            };
        }
    }
}
