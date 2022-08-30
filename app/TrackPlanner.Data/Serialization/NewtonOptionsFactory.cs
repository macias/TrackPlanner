
using MathUnit;
using Newtonsoft.Json;

namespace TrackPlanner.Data.Serialization
{
    public static class NewtonOptionsFactory
    {
        public static void CustomizeJsonOptions(JsonSerializerSettings options)
        {
            options.Converters.Add(new AngleNewtonConverter());
            options.Converters.Add(new LengthNewtonConverter());
            options.Converters.Add(new SpeedNewtonConverter());
            options.Converters.Add(new NullableLengthNewtonConverter());
          //  options.Converters.Add(new DictionaryNewtonConverter<SpeedMode,Speed>());
        }
        
        public static JsonSerializerSettings BuildJsonOptions(bool compact)
        {
            var options = new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = compact? Formatting.None: Formatting.Indented,
            };

            CustomizeJsonOptions(options);

            return options;
        }
    }
}
