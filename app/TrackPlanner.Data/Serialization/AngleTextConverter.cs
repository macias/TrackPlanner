using MathUnit;
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrackPlanner.Data.Serialization
{
    public class AngleTextConverter : System.Text.Json.Serialization.JsonConverter<Angle>
    {
        public override Angle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (text == null)
                return default;
            else
                return Angle.FromDegrees(double.Parse(text, CultureInfo.InvariantCulture));
        }

        public override void Write(Utf8JsonWriter writer, Angle value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Degrees.ToString(CultureInfo.InvariantCulture));
        }
    }

}
