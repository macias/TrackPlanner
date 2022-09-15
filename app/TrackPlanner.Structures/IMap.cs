using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Structures
{
    public interface IMap<TKey, TValue> : IReadOnlyMap<TKey,TValue>
        where TKey : notnull
    {
        new TValue this[TKey key] { get; set; }

        void Add(TKey key, TValue value);
        void ExceptWith(IEnumerable<TKey> keys);
        bool TryAdd(TKey key, TValue value, [MaybeNullWhen(true)] out TValue existing);
    }
}