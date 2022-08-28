using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TrackPlanner.Mapping.Data
{
    internal sealed class BinaryMap<TKey, TValue> : IMap<TKey, TValue>
        where TKey : notnull
    {
        private sealed class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly BinaryMap<TKey, TValue> source;
            private int valueIndex;
            private readonly IEnumerator<TKey> iter;

            public KeyValuePair<TKey, TValue> Current => KeyValuePair.Create(this.iter.Current, source.values[this.valueIndex]);
            object IEnumerator.Current => this.Current;

            public Enumerator(BinaryMap<TKey, TValue> source)
            {
                this.source = source;
                // we use mixed iteration to protect ourselves from altering the keys, but allowing to modify the values
                this.valueIndex = -1;
                this.iter = source.Keys.GetEnumerator();
            }

            public void Dispose()
            {
                this.iter.Dispose();
            }

            public bool MoveNext()
            {
                ++this.valueIndex;
                this.iter.MoveNext();
                return this.valueIndex < source.Count;
            }

            public void Reset()
            {
                this.valueIndex = -1;
                this.iter.Reset();
            }
        }

        private readonly List<TKey> keys;
        private readonly List<TValue> values;
        private readonly IComparer<TKey> comparer;

        public IEnumerable<TKey> Keys => this.keys;
        public IEnumerable<TValue> Values => this.values;

        public int Count => this.keys.Count;

        public TValue this[TKey key]
        {
            get
            {
                if (tryGetIndex(key, out int index))
                    return values[index];
                throw new ArgumentOutOfRangeException($"Key {key} is not present.");
            }
            set
            {
                if (tryGetIndex(key, out int index))
                    this.values[index] = value;
                else
                {
                    insertAt(key, value, index);
                }
            }
        }

        public BinaryMap(IComparer<TKey> comparer) : this(comparer,0)
        {
        }

        public BinaryMap(IComparer<TKey> comparer, int capacity)
        {
            this.comparer = comparer;
            this.keys = new List<TKey>(capacity);
            this.values = new List<TValue>(capacity);
        }

        public BinaryMap(IComparer<TKey> comparer, IEnumerable<KeyValuePair<TKey, TValue>> sequence) : this(comparer)
        {
            foreach ((TKey key, TValue value) in sequence)
                Add(key, value);
        }

        public bool TryAdd(TKey key, TValue value, [MaybeNullWhen(true)] out TValue existing)
        {
            if (tryGetIndex(key, out int index))
            {
                existing = values[index];
                return false;
            }
            else
            {
                existing = default;
                insertAt(key, value, index);
                return true;
            }
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (!tryGetIndex(key, out int index))
            {
                value = default;
                return false;
            }

            value = this.values[index];
            return true;
        }

        public void Add(TKey key, TValue value)
        {
            if (tryGetIndex(key, out int index))
                throw new ArgumentException($"Key {key} already exists");

            insertAt(key, value, index);
        }

        private bool tryGetIndex(TKey key, out int index)
        {
            if (Count == 0)
            {
                index = 0;
                return false;
            }

            // keep this special case for O(1) append
            index = Count - 1;
            int comp = comparer.Compare(key, this.keys[index]);
             if (comp == 0)
                return true;
            else if (comp > 0)
            {
                ++index;
                return false;
            }

            int lower_bound = 0;
            int higher_bound = Math.Max(lower_bound, index - 1);

            int DEBUG = Count;
            while (true)
            {
                if (DEBUG == 0)
                    throw new ArgumentException("Too many iterations");
                --DEBUG;

                index = (lower_bound + higher_bound) / 2;
                comp = comparer.Compare(key, this.keys[index]);
                if (comp == 0)
                    return true;

                if (comp > 0) // key is greater than current element
                {
                    if (lower_bound == higher_bound)
                    {
                        ++index;
                        return false;
                    }
                    else
                    {
                        lower_bound = Math.Min(higher_bound, index + 1);
                    }
                }
                else
                {
                    if (lower_bound == higher_bound)
                    {
                        return false;
                    }
                    else
                    {
                        higher_bound = Math.Max(lower_bound, index - 1);
                    }
                }
            }

        }

        private void insertAt(TKey key, TValue value, int index)
        {
            this.keys.Insert(index, key);
            this.values.Insert(index, value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return tryGetIndex(key, out _);
        }

        public void ExceptWith(IEnumerable<TKey> keys)
        {
            int count = 0;
            foreach (TKey k in keys)
            {
                if (tryGetIndex(k, out int index))
                {
                    this.keys.RemoveAt(index);
                    this.values.RemoveAt(index);
                }

                 ++count;
            }
        }
    }
}