using System.Collections;
using System.Collections.Generic;
using System.IO;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class ReaderOffsets<TKey> : IEnumerable<KeyValuePair<TKey, long>>
    where TKey:notnull
    {
        private readonly BinaryReader reader;
        private readonly CompactDictionaryFirst<TKey, long> offsets;

        public long this[TKey key] => this.offsets[key];

        public ReaderOffsets(BinaryReader reader, int capacity)
        {
            this.reader = reader;
            this.offsets = new CompactDictionaryFirst<TKey, long>(capacity: capacity);
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

        public IEnumerator<KeyValuePair<TKey, long>> GetEnumerator()
        {
            return this.offsets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}