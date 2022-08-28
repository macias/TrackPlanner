using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TrackPlanner.Mapping.Data
{
    // 1.5x slower than dictionary
    public sealed class SeededDictionary<TKey, TValue>
        where TKey : notnull
    {
        // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

        // https://blog.markvincze.com/back-to-basics-dictionary-part-2-net-implementation/

        // https://docs.microsoft.com/en-us/previous-versions/ms379570(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379571(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379572(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379573(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379574(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379575(v=vs.80)

        private readonly TKey[] keys;
        private readonly TValue[] values;
        private readonly int[] targets;

        private readonly IEqualityComparer<TKey> comparer;
        private int occupied;

        public int Count { get; private set; }

        private const int notUsed = -1;
        private const int terminal = int.MinValue;

        public SeededDictionary(IEqualityComparer<TKey> comparer, int capacity)
        {
            this.comparer = comparer;
            this.keys = new TKey[capacity];
            this.values = new TValue[capacity];
            this.targets = new int[capacity];

            Array.Fill(this.targets, notUsed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int mod(int k, int n)
        {
            var result = k % n;
            return (result < 0) ? result+n : result;
            
        }
        
        public void AddSeed(TKey key, TValue value)
        {
            int hash = mod( key.GetHashCode() , this.keys.Length);
            if (this.targets[hash] != notUsed)
                return;

            int data_index = hash;
            this.keys[data_index] = key;
            this.values[data_index] = value;
            this.targets[hash] = terminal;

            ++Count;
        }

        public void TryAdd(TKey key, TValue value)
        {
            int hash = mod(key.GetHashCode() , this.keys.Length);

            for (int index = hash; index != terminal; index = this.targets[index])
            {
                if (this.comparer.Equals(keys[index], key))
                    return;
            }

            while (this.targets[this.occupied] != notUsed)
                ++this.occupied;

            int data_index = this.occupied;
            this.keys[data_index] = key;
            this.values[data_index] = value;
            this.targets[data_index] = this.targets[hash];
            this.targets[hash] = data_index;

            ++Count;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            int hash = mod(key.GetHashCode() , this.keys.Length);

            for (int index = hash; index != terminal; index = this.targets[index])
            {
                if (this.comparer.Equals(keys[index], key))
                {
                    value = this.values[index];
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}