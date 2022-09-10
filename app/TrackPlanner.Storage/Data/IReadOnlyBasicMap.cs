using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Storage.Data
{
    public interface IReadOnlyBasicMap<TKey, TValue> 
        where TKey : notnull
    {
        TValue this[TKey index] { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    }

    }