using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Storage
{
    public sealed class DiskDictionary<TKey,TValue> : IReadOnlyEnumerableDictionary<TKey, TValue>
        where TKey : notnull
    {
        // https://thargy.com/2014/03/memory-mapped-file-performance/
        
        private readonly Dictionary<TKey, (int historyStamp,TValue value)> cache;
        private readonly IReadOnlyList<ReaderOffsets<TKey>> sourceReaders;
        private readonly Func<TKey, IReadOnlyList<BinaryReader>, TValue> loader;
        // we store offsets for given entities, but in this particular dictionary
        // we might be interested only in some part of it, thus special extra offset
        private readonly int extraOffset;
        private readonly int memoryLimit;
        private int historyIndex;
        
        private int DEBUG_clearCounter;
        

        public TValue this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out var result))
                    throw new ArgumentException($"Key not found {key}");

                return result;
            }
        }
        
        public DiskDictionary(IReadOnlyList<ReaderOffsets<TKey>> sourceReaders, int extraOffset, 
            Func<TKey,IReadOnlyList<BinaryReader>,TValue> loader,int memoryLimit)
        {
            this.sourceReaders = sourceReaders;
            this.extraOffset = extraOffset;
            this.loader = loader;
            this.memoryLimit = memoryLimit;
            this.cache = new Dictionary<TKey, (int,TValue)>(capacity:memoryLimit);
        }

        public bool ContainsKey(TKey key)
        {
            // do NOT use our TryGetValue, because it actually reads the value from the disk
            // and here we are interested only whether the key exists
            
            if (this.cache.TryGetValue(key, out _))
                return true;

            foreach (var (_, offsets) in this.sourceReaders)
            {
                if (offsets.TryGetValue(key, out _))
                    return true;
            }

            return false;
        }
        
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (this.cache.TryGetValue(key, out var value_entry))
            {
                value = value_entry.value;
                return true;
            }

            var active = new List<BinaryReader >(capacity: this.sourceReaders.Count);

            foreach (var (reader, offsets) in this.sourceReaders)
            {
                if (!offsets.TryGetValue(key, out long offset))
                    continue;

                active.Add(reader);
                reader.BaseStream.Seek(offset+this.extraOffset, SeekOrigin.Begin);
            }

            if (active.Count == 0)
            {
                value = default;
                return false;
            }

            value = this.loader(key,active);

            if (this.cache.Count == this.memoryLimit)
            {
                ++this.DEBUG_clearCounter;
                foreach (var entry in this.cache)
                {
                    if (entry.Value.historyStamp == this.historyIndex - this.memoryLimit)
                    {
                        this.cache.Remove(entry.Key);
                        break;
                    }
                }
            }

            this.cache.Add(key, (this.historyIndex, value));
            ++this.historyIndex;
            
            return true;
        }

        public string GetStats()
        {
            return $"{nameof(this.DEBUG_clearCounter)} {this.DEBUG_clearCounter}";
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