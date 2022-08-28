using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class DiskDictionary<TKey,TValue> : IReadOnlyEnumerableDictionary<TKey, TValue>
        where TKey : notnull
    {
        // https://thargy.com/2014/03/memory-mapped-file-performance/
        
        private readonly CompactDictionaryFirst<TKey, TValue> cache;
        private readonly IReadOnlyList<ReaderOffsets<TKey>> source;
        private readonly int offsetPadding;
        private readonly Func<IReadOnlyList<BinaryReader>, TValue> loader;
        private readonly int limit;

        private int clearCount;

        public TValue this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out var result))
                    throw new ArgumentException($"Key not found {key}");

                return result;
            }
        }
        
        public DiskDictionary(IReadOnlyList<ReaderOffsets<TKey>> source, int offsetPadding, Func<IReadOnlyList<BinaryReader>,TValue> loader,int limit)
        {
            this.source = source;
            this.offsetPadding = offsetPadding;
            this.loader = loader;
            this.limit = limit;
            this.cache = new CompactDictionaryFirst<TKey, TValue>(capacity:limit);
        }

        public bool ContainsKey(TKey key)
        {
            if (this.cache.TryGetValue(key, out _))
                return true;

            foreach (var (_, offsets) in this.source)
            {
                if (offsets.TryGetValue(key, out _))
                    return true;
            }

            return false;
        }
        
        public unsafe bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (this.cache.TryGetValue(key, out value))
                return true;

            var active = new List<BinaryReader >(capacity: this.source.Count);

            foreach (var (reader, offsets) in this.source)
            {
                if (!offsets.TryGetValue(key, out long offset))
                    continue;

                active.Add(reader);
                reader.BaseStream.Seek(offset+this.offsetPadding, SeekOrigin.Begin);
            }

            if (active.Count == 0)
                return false;

            value = this.loader(active);

            if (this.cache.Count == limit)
            {
                ++this.clearCount;
                this.cache.Clear();
            }

            this.cache.Add(key, value);

            return true;
        }

        public string GetStats()
        {
            return $"{nameof(this.clearCount)} {this.clearCount}";
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}