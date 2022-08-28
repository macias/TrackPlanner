using MathUnit;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace TrackPlanner.Data.Serialization
{
    public class AngleNewtonConverter : JsonConverter<Angle>
    {
        public override Angle ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Angle existingValue, bool hasExistingValue,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            var text = serializer.Deserialize<string>(reader);
            if (text == null)
                return default;
            else
                return Angle.FromDegrees(double.Parse(text, CultureInfo.InvariantCulture));
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Angle value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteValue(value.Degrees.ToString(CultureInfo.InvariantCulture));
        }

    }
}
