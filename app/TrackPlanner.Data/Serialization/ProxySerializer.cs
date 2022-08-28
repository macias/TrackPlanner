using Newtonsoft.Json;

namespace TrackPlanner.Data.Serialization
{
    public sealed class ProxySerializer
    {
        private readonly JsonSerializerSettings jsonOptions;

        public ProxySerializer()
        {
            this.jsonOptions = NewtonOptionsFactory.BuildJsonOptions(compact: false);
        }

        public string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, jsonOptions);
        }

        public T? Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, jsonOptions);
        }

    }
}