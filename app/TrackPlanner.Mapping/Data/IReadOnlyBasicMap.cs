using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Transactions;

namespace TrackPlanner.Mapping.Data
{
    public interface IReadOnlyBasicMap<TKey, TValue> 
        where TKey : notnull
    {
        TValue this[TKey index] { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    }

    public interface IReadOnlyEnumerableDictionary<TKey, TValue> : IReadOnlyBasicMap<TKey,TValue>,IEnumerable<KeyValuePair<TKey,TValue>>
        where TKey : notnull
    {
    }
}