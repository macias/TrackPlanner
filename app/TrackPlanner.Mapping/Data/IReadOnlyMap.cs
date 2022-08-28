using System.Collections.Generic;

namespace TrackPlanner.Mapping.Data
{
    public interface IReadOnlyMap<TKey, TValue> : IReadOnlyEnumerableDictionary<TKey,TValue>
        where TKey : notnull
    {
        int Count { get; }
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
    }
   
}