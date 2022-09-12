using System.Collections.Generic;
using System.IO;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Storage
{
    // this is basically a dictionary with disk offsets for a reader with reader as well 
    public sealed class ReaderOffsets<TKey> 
    where TKey:notnull
    {
        private readonly BinaryReader reader;
        private readonly CompactDictionaryFill<TKey, long> offsets;

        public long this[TKey key] => this.offsets[key];

        public IEnumerable<KeyValuePair<TKey, long>> Offsets => this.offsets;
            
        public ReaderOffsets(BinaryReader reader, int capacity)
        {
            this.reader = reader;
            this.offsets = new CompactDictionaryFill<TKey, long>(capacity: capacity);
        }

        public void Deconstruct(out BinaryReader reader, out IReadOnlyDictionary<TKey, long> offsets)
        {
            reader = this.reader;
            offsets = this.offsets;
        }

        public void AddReaderOffset(TKey key)
        {
            this.offsets.Add(key, this.reader.ReadInt64());
        }

      /*  public IEnumerator<KeyValuePair<TKey, long>> GetEnumerator()
        {
            return this.offsets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }*/
    }
}