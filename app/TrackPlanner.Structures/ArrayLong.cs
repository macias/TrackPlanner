using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TrackPlanner.Structures

{
    public sealed class ArrayLong<TValue> : IReadOnlyArrayLong<TValue>
    {
        private readonly List<TValue> data;

        public IEnumerable<TValue> Values => this.data;
        public IEnumerable<long> Keys => Enumerable.Range(0,this.data.Count).Select(x => (long)x);

        public TValue this[long index]
        {
            get { return this.data[(int) index]; }
            set { this.data[(int) index] = value; }
        }

        public int Count => this.data.Count;

        public ArrayLong(int capacity)
        {
            this.data = new List<TValue>(capacity);
        }

        public void TrimExcess()
        {
            this.data.TrimExcess();
        }

        public void Add(TValue value)
        {
            this.data.Add(value);
        }
        
        public bool ContainsKey(long key)
        {
            return key < this.Count;
        }

        public bool TryGetValue(long key, [MaybeNullWhen(false)] out TValue value)
        {
            if (key >= this.Count)
            {
                value = default;
                return false;
            }

            value = this[key];
            return true;
        }

        public IEnumerable<KeyValuePair<long, TValue>> iteratePairs()
        {
            long i = 0;
            foreach (var elem in this.data)
            {
                yield return KeyValuePair.Create(i, elem);
                ++i;
            }
        }

        public IEnumerator<KeyValuePair<long, TValue>> GetEnumerator()
        {
            return iteratePairs().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}