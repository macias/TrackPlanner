using System;
using System.Collections;
using System.Collections.Generic;

namespace TrackPlanner.PathFinder
{
    public static class DictionaryViewSet
    {
        public static DictionaryViewSet<TKey, TValue> Create<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            return new DictionaryViewSet<TKey, TValue>(dictionary);
        }
    }

    public sealed class DictionaryViewSet<TKey, TValue> : IReadOnlySet<TKey>
    {
        private readonly IReadOnlyDictionary<TKey, TValue> dictionary;

        public DictionaryViewSet(IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary;
        }

        public IEnumerator<TKey> GetEnumerator()
        {
            return this.dictionary.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => this.dictionary.Count;

        public bool Contains(TKey item)
        {
            return this.dictionary.ContainsKey(item);
        }

        public bool IsProperSubsetOf(IEnumerable<TKey> other)
        {
            throw new System.NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<TKey> other)
        {
            throw new System.NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<TKey> other)
        {
            throw new System.NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<TKey> other)
        {
            throw new System.NotImplementedException();
        }

        public bool Overlaps(IEnumerable<TKey> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            foreach (var elem in other)
                if (Contains(elem))
                    return true;

            return false;
        }

        public bool SetEquals(IEnumerable<TKey> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            foreach (var elem in other)
                if (Contains(elem))
                    return true;

            return false;
        }
    }

}