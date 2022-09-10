using System.Collections.Generic;

namespace TrackPlanner.Storage.Data
{

    public interface IReadOnlyEnumerableDictionary<TKey, TValue> : IReadOnlyBasicMap<TKey,TValue>,IEnumerable<KeyValuePair<TKey,TValue>>
        where TKey : notnull
    {
    }
}