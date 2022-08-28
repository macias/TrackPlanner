using MathUnit;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace TrackPlanner.Data.Serialization
{
    public class LengthNewtonConverter : JsonConverter<Length>
    {
        public override Length ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Length existingValue, bool hasExistingValue,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            var text = serializer.Deserialize<string>(reader);
            if (text == null)
                return default;
            else
                return GetLength(text);
        }

        internal static Length GetLength(string text)
        {
            return Length.FromMeters(double.Parse(text, CultureInfo.InvariantCulture));
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Length value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteValue(PutLength(value));
        }

        internal static string PutLength(Length value)
        {
            return value.Meters.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class NullableLengthNewtonConverter : JsonConverter<Length?>
    {
        public override Length? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Length? existingValue, bool hasExistingValue,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            var text = serializer.Deserialize<string>(reader);
            if (text == null)
                return null;

            return LengthNewtonConverter.GetLength(text);
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Length? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else
                writer.WriteValue(LengthNewtonConverter.PutLength( value.Value));
        }

    }
}
