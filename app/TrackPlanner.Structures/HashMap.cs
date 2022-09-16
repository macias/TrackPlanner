using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.Structures
{
    public sealed class HashMap<TKey, TValue> : IMap<TKey,TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> elements;

        public IEnumerable<TKey> Keys => this.elements.Keys;

        public int Count => this.elements.Count;

        public IEnumerable<TValue> Values => this.elements.Values;

        public TValue this[TKey index]
        {
            get { return this.elements[index]; }
            set { this.elements[index] = value; }
        }

        public HashMap() : this(0)
        {
        }

        public HashMap(int capacity) 
        {
            this.elements = new Dictionary<TKey, TValue>(capacity);
        }

        public HashMap(IEnumerable<KeyValuePair<TKey, TValue>> collection)
        {
            this.elements = new Dictionary<TKey, TValue>(collection);
        }

        public bool TryAdd(TKey key, TValue value, [MaybeNullWhen(true)] out TValue existing)
        {
            if (this.elements.TryGetValue(key, out existing))
                return false;
            else
            {
                this.elements.Add(key, value);
                return true;
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            return TryAdd(key, value, out _);
        }

        public bool TryGetValue(TKey index, [MaybeNullWhen(false)] out TValue value)
        {
            return this.elements.TryGetValue(index, out value);
        }

        public void Add(TKey index, TValue value)
        {
            this.elements.Add(index, value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.elements.GetEnumerator();
        }

        public bool ContainsKey(TKey idx)
        {
            return this.elements.ContainsKey(idx);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void ExceptWith(IEnumerable<TKey> indices)
        {
            foreach (TKey idx in indices)
                this.elements.Remove(idx);
        }

        public void TrimExcess()
        {
            this.elements.TrimExcess();
        }
    }
}