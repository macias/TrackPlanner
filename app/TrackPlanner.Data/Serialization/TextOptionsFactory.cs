using System.Text.Json;

namespace TrackPlanner.Data.Serialization
{
    public static class TextOptionsFactory
    {
        public static void CustomizeJsonOptions(JsonSerializerOptions options)
        {
            options.Converters.Add(new AngleTextConverter());
        }
        public static JsonSerializerOptions BuildJsonOptions(bool compact)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = !compact,
            };

            CustomizeJsonOptions(options);

            return options;
        }
    }
}
