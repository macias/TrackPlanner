using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TrackPlanner.Mapping.Data
{
    public sealed class CompactDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        // similarly to original Dictionary we keep an array of indices, initially key+value is placed as their hash
        // position -- this is in-sync position, and we indicate it with keeping last bit 0 (so the number is positive)

        // if we add another key with the same hash, we place it somewhere else, so we puth the index from hash to that
        // placement and from the other placement back to hash. However back-index is at non-hash position thus we mark
        // it with last bit set = 1

        // EXAMPLE: 
        // adding key A with local hash = 2 
        // indices   keys
        // [not used] [ ]
        // [not used] [ ]
        // [2]  -->   [A]
        // let's add B with the same hash, we cannot add at the same spot, so we find first not-occupied, and then set the indices
        // [2 | FLAG] [B]
        // [not used] [ ]
        // [0]        [A]
        // please note first row has the out-of-sync set because it is not "its" slot, while the last one does not have such flag
        // the last entry (in-sync) will never be rellocated. Also, once starting from the hash position, you can loop around
        // and iterate over all entries with the same hash (here hash: 2 -> 0 -> 2)

        // ok, let's add C with hash 0, this slot is occupied by out-of sync-entry so first we have to move it
        // [not used] [ ]
        // [2 | FLAG] [B]
        // [1]        [A]
        // and then add new entry
        // [0]        [C]
        // [2 | FLAG] [B]
        // [1]        [A]



        // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

        // https://blog.markvincze.com/back-to-basics-dictionary-part-2-net-implementation/

        // https://docs.microsoft.com/en-us/previous-versions/ms379570(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379571(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379572(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379573(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379574(v=vs.80)
        // https://docs.microsoft.com/en-us/previous-versions/ms379575(v=vs.80)

        // https://stackoverflow.com/questions/1100311/what-is-the-ideal-growth-rate-for-a-dynamically-allocated-array
        // https://stackoverflow.com/questions/24831998/lists-double-their-space-in-c-sharp-when-they-need-more-room-at-some-point-does
        private const double growthRatio = 1.6;

        private TKey[] keys = default!;
        private TValue[] values = default!; 
        private int[] targets = default!;

        private readonly IEqualityComparer<TKey> comparer;
        private int occupied;

        public int Count { get; private set; }
        public int Capacity => this.keys.Length;

        private const int notUsed = 0;
        private const int shift = 1;
        private const int indexMask = int.MaxValue;
        private const int redirectedBit = int.MinValue;

        public TValue this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out TValue? value))
                    throw new ArgumentException($"Key {key} does not exist.");

                return value;
            }
            set
            {
                if (!tryAdd(key, getHash(key), value, overwrite: true, out _))
                    throw new NotSupportedException();
            }
        }

        public IEnumerable<TKey> Keys => this.iterate().Select(it => it.Key);
        public IEnumerable<TValue> Values => this.iterate().Select(it => it.Value);

        public CompactDictionary(int capacity = 0) : this(EqualityComparer<TKey>.Default, capacity)
        {
        }

        public CompactDictionary(IEqualityComparer<TKey> comparer, int capacity = 0)
        {
            this.comparer = comparer;

            initData(capacity);
        }

        private void initData(int capacity)
        {
            if (capacity == int.MaxValue || capacity<0)
                throw new NotSupportedException($"{nameof(capacity)} {capacity}");

            this.keys = new TKey[capacity];
            this.values = new TValue[capacity];
            this.targets = new int[capacity];

            Clear();
        }

        public void Clear()
        {
            this.occupied = 0;
            this.Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getHash(TKey key)
        {
            var hash = key.GetHashCode();
            if (hash == int.MinValue)
                return int.MaxValue - 1;
            // we cannot use max value because we use 0 as not-used marker/value
            // and so we incremenet all indices/hashes, so we would fall out of range
            else if (hash == int.MaxValue)
                return 0;
            else if (hash < 0)
                return hash + int.MaxValue;
            else
                return hash;
        }

        public void Add(TKey key, TValue value)
        {
            if (!tryAdd(key, getHash(key), value, overwrite: false, out _))
                throw new ArgumentException($"Key {key} already exists.");
        }

        public bool TryAdd(TKey key, TValue value, out TValue? existing)
        {
            return tryAdd(key, getHash(key),value, overwrite: false, out existing);
        }

        public void TrimExcess()
        {
            resize(this.Count);
        }
        
        public void Expand()
        {
            resize( (int) Math.Round((this.keys.Length + 1) * growthRatio));
        }

        private void resize(int capacity)
        {
            int DEBUG = Count;

            var source_keys = keys;
            var source_values = values;
            var source_targets = targets;

            initData(capacity);

            var source_modulo = source_keys.Length;

            for (int i = source_keys.Length - 1; i >= 0; --i)
            {
                if (source_targets[i] == notUsed)
                    continue;

                if (!tryAdd(source_keys[i], getHash(source_keys[i]), source_values[i], overwrite: false, out _))
                    throw new InvalidOperationException();
                --DEBUG;

            }
            if (DEBUG != 0)
            {
                DEBUG_DUMP(source_targets);
                throw new InvalidOperationException($"Invalid resize {DEBUG} not copied");
            }
        }

        public void DEBUG_DUMP()
        {
            DEBUG_DUMP(this.targets);
        }

        private static void DEBUG_DUMP(int[] targets)
        {
            int size = targets.Length;
            Console.WriteLine($"current size {size}");
            foreach (var target in targets)
                if (target == notUsed)
                    Console.WriteLine("Not used");
                else
                    Console.WriteLine($"{target} -- {(target & indexMask)-shift} -- {((target & indexMask)-shift) % size}");
            Console.WriteLine("======================");
        }
        
        private bool tryAdd(TKey key, int pureHash, TValue value, bool overwrite,  out TValue? existing)
        {
            if (this.keys.Length == 0)
                initData(2);

            int hash = pureHash % this.keys.Length;
            
            // this slot is taken and it is taken with out-of-sync hash, thus we need to move
            // this entry somewhere else
            if (this.targets[hash] < 0)
            {
                if (Count == this.keys.Length)
                {
                    Expand();
                    return tryAdd(key, pureHash, value, overwrite, out existing);
                }

                int index = hash;

                // searching the slot which directs to this entry
                while (true)
                {
                    var dest = (this.targets[index] & indexMask) - shift;
                    if (dest == hash)
                        break;
                    index = dest;
                }

                // looking for some free entry
                while (this.targets[this.occupied] != notUsed)
                {
                    ++this.occupied;
                }

                // we move this entry to free one
                this.keys[occupied] = this.keys[hash];
                this.values[this.occupied] = this.values[hash];
                // and reorganize the indices
                this.targets[this.occupied] = this.targets[hash]; // redirect bit was already set
                this.targets[index] = (this.occupied+shift) | (this.targets[index] & redirectedBit); // we have to preserve redirect bit, not blindly set it
                this.targets[hash] = notUsed;
            }

            if (this.targets[hash] == notUsed)
            {
                if (Count == this.keys.Length)
                {
                    Expand();
                    return tryAdd(key,pureHash, value, overwrite, out existing);
                }

                this.keys[hash] = key;
                this.values[hash] = value;
                // preserve original hash, so on resize we won't rehash everything
                this.targets[hash] = hash+shift; // this is valid (in-sync) slot so do not set redirection bit 
            }
            else
            {
                {
                    int index = hash;
                    do
                    {
                        if (this.comparer.Equals(keys[index], key))
                        {
                            existing = values[index];
                            if (overwrite)
                            {
                                this.values[index] = value;
                                return true;
                            }
                            else
                                return false;
                        }

                        index = (this.targets[index] & indexMask) -shift;

                    } while (index != hash);
                }

                if (Count == this.keys.Length)
                {
                    Expand();
                    return tryAdd(key, pureHash, value, overwrite, out existing);
                }

                while (this.targets[this.occupied] != notUsed)
                {
                    ++this.occupied;
                }

                var data_index = this.occupied;
                this.keys[data_index] = key;
                this.values[data_index] = value;
                this.targets[data_index] = this.targets[hash] | redirectedBit;
                this.targets[hash] = data_index+shift; // this is valid (in-sync) slot so do not set redirection bit 
            }


            ++Count;

            existing = default;
            return true;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (Count == 0)
            {
                value = default;
                return false;
            }

            int hash = getHash(key) % this.keys.Length;

            // checking if slot is occupied and whether at least hash matches
            if (this.targets[hash] != notUsed && this.targets[hash] >= 0)
            {
                int index = hash;
                do
                {
                    if (this.comparer.Equals(keys[index], key))
                    {
                        value = this.values[index];
                        return true;
                    }

                    index = (this.targets[index] & indexMask)-shift;
                } while (index != hash);
            }

            value = default;
            return false;
        }

        private IEnumerable<KeyValuePair<TKey, TValue>> iterate()
        {
            int idx = -1;
            foreach (var target in this.targets)
            {
                ++idx;
                if (target==notUsed)
                    continue;
                yield return KeyValuePair.Create(this.keys[idx], this.values[idx]);
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return iterate().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }
    }
}