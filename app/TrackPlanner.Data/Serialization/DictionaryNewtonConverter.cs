using MathUnit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrackPlanner.Data.Serialization
{
    public class DictionaryNewtonConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override Dictionary<TKey, TValue>? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Dictionary<TKey, TValue>? existingValue, bool hasExistingValue,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            KeyValuePair<TKey, TValue>[]? pairs = serializer.Deserialize<KeyValuePair<TKey, TValue>[]>(reader);
            if (pairs == null)
                return null;
            else
                return pairs.ToDictionary(kv => kv.Key, kv => kv.Value);
        }


        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Dictionary<TKey, TValue>? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else
                serializer.Serialize(writer, value.ToArray());
        }

    }
}
