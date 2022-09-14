using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Storage.Data
{
    public interface ICompactDictionary<TKey, TValue>     : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        int Capacity { get; }
        new TValue this[TKey key] { get; set; }
        void Clear();
        void Add(TKey key, TValue value);
        bool TryAdd(TKey key, TValue value, out TValue? existing);
        void TrimExcess();
        void Expand();
        void DEBUG_DUMP();
        bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value);
    }
}