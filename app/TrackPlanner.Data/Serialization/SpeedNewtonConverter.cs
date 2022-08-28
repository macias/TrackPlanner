using MathUnit;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace TrackPlanner.Data.Serialization
{
    public class SpeedNewtonConverter : JsonConverter<Speed>
    {
        public override Speed ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Speed existingValue, bool hasExistingValue,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            var text = serializer.Deserialize<string>(reader);
            if (text == null)
                return default;
            else
            return GetSpeed(text);
        }

        internal static Speed GetSpeed(string text)
        {
            return Speed.FromMetersPerSecond(double.Parse(text, CultureInfo.InvariantCulture));
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Speed value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteValue(PutSpeed(value));
        }

        internal static string PutSpeed(Speed value)
        {
            return value.MetersPerSecond.ToString(CultureInfo.InvariantCulture);
        }
    }

    
}
