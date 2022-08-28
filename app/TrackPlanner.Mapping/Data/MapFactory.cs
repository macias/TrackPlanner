using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TrackPlanner.Mapping.Data
{
    public static class MapFactory
    {
        public static IMap<TKey, TValue> CreateFast<TKey, TValue>()
            where TKey : IComparable<TKey>
        {
            return new HashMap<TKey, TValue>();
        }

        public static IMap<TKey, TValue> CreateFast<TKey, TValue>(int capacity)
            where TKey : IComparable<TKey>
        {
            return new HashMap<TKey, TValue>(capacity);
        }

        public static IMap<TKey, TValue> CreateFast<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> sequence)
            where TKey : IComparable<TKey>
        {
            return new HashMap<TKey, TValue>(sequence);
        }

        public static IMap<TKey, TValue> CreatePacked<TKey, TValue>()
            where TKey : IComparable<TKey>
        {
            return new BinaryMap<TKey, TValue>(Comparer<TKey>.Default);
        }

        public static IMap<TKey, TValue> CreatePacked<TKey, TValue>(int capacity)
            where TKey : IComparable<TKey>
        {
            return new BinaryMap<TKey, TValue>(Comparer<TKey>.Default, capacity);
        }

        public static IMap<TKey, TValue> CreatePacked<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> sequence)
            where TKey : IComparable<TKey>
        {
            return new BinaryMap<TKey, TValue>(Comparer<TKey>.Default, sequence);
        }
    }

}