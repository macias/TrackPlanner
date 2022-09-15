using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Structures
{
    public interface ICompactDictionary<TKey, TValue>     : IMap<TKey,TValue>,IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        int Capacity { get; }
        void Clear();
        void TrimExcess();
        void Expand();
        void DEBUG_DUMP();
        bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value);
    }
}