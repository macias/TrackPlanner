using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Structures
{
    public interface IReadOnlyBasicMap<in TKey, TValue> 
        where TKey : notnull
    {
        TValue this[TKey index] { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    }

    }