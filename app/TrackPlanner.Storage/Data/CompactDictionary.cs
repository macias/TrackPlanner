using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TrackPlanner.Storage.Data
{
    public abstract class CompactDictionary<TKey, TValue> : ICompactDictionary<TKey, TValue>
        where TKey : notnull
    {
        protected abstract int notUsed { get; }

        private const double growthRatio = 1.6;

        protected const int indexMask = int.MaxValue;
        protected const int redirectedBit = int.MinValue;

        private readonly IEqualityComparer<TKey> comparer;

        // anything below is for sure occupied, anything above maybe
        // in other words when searching for free slot it makes sense
        // to look only above
        private int occupied;

        public int Count { get; private set; }
        public int Capacity => this.keys.Length;

        private TKey[] keys = default!;
        private TValue[] values = default!;
        private int[] targets = default!;

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
                if (!tryAdd(key, key.GetHashCode(), value, overwrite: true, out _))
                    throw new NotSupportedException();
            }
        }

        public IEnumerable<TKey> Keys => this.iterate().Select(it => it.Key);
        public IEnumerable<TValue> Values => this.iterate().Select(it => it.Value);

        protected CompactDictionary(int capacity) : this(EqualityComparer<TKey>.Default, capacity)
        {
        }

        protected CompactDictionary(IEqualityComparer<TKey> comparer, int capacity = 0)
        {
            this.comparer = comparer;

            initData(capacity);
        }

        private void initData(int capacity)
        {
            if (capacity == int.MaxValue || capacity < 0)
                throw new NotSupportedException($"{nameof(capacity)} {capacity}");

            this.keys = new TKey[capacity];
            this.values = new TValue[capacity];
            this.targets = new int[capacity];

            this.occupied = 0;
            this.Count = 0;
            if (notUsed != 0)
                Array.Fill(this.targets, notUsed);
        }

        public void Clear()
        {
            this.occupied = 0;
            this.Count = 0;
            Array.Fill(this.targets, notUsed);
            // we have to clear those as well because we could have hanging references
            // i.e. without these two calls GC could not reclaim memory
            Array.Fill(this.keys, default(TKey));
            Array.Fill(this.values, default(TValue));
        }

        public void Add(TKey key, TValue value)
        {
            if (!tryAdd(key, key.GetHashCode(), value, overwrite: false, out _))
                throw new ArgumentException($"Key {key} already exists.");
        }

        public bool TryAdd(TKey key, TValue value, out TValue? existing)
        {
            return tryAdd(key, key.GetHashCode(), value, overwrite: false, out existing);
        }

        public void TrimExcess()
        {
            resize(this.Count);
        }

        public void Expand()
        {
            resize((int) Math.Round((this.keys.Length + 1) * growthRatio));
        }

        // https://stackoverflow.com/a/51018529/210342
        protected int mod(int k, int n) // always returns positive value
        {
            return ((k %= n) < 0) ? k + n : k;
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

                if (!tryAdd(source_keys[i], source_keys[i].GetHashCode(), source_values[i], overwrite: false, out _))
                    throw new InvalidOperationException();
                --DEBUG;

            }

            if (DEBUG != 0)
            {
                DEBUG_DUMP(source_targets, source_keys);
                throw new InvalidOperationException($"Invalid resize {DEBUG} not copied");
            }
        }

        public void DEBUG_DUMP()
        {
            DEBUG_DUMP(this.targets, this.keys);
        }

        private void DEBUG_DUMP(int[] dumpTargets, TKey[] dumpKeys)
        {
            int size = dumpTargets.Length;
            Console.WriteLine($"current size {size}");
            for (int i = 0; i < size; ++i)
            {
                var target = dumpTargets[i];
                if (target == notUsed)
                    Console.WriteLine($"[{i}] Not used");
                else
                    Console.WriteLine($"[{i}] {dumpKeys[i]} -- {mod(dumpKeys[i].GetHashCode(), size)} -- {targetToIndex(target)}{((target & redirectedBit) != 0 ? " R" : "")}");
            }

            Console.WriteLine("======================");
        }

        private bool tryAdd(TKey key, int pureHash, TValue value, bool overwrite, out TValue? existing)
        {
            if (this.keys.Length == 0)
                initData(2);

            int hash_index = mod(pureHash, this.keys.Length);

            // this slot is taken and it is taken with out-of-sync hash, thus we need to move
            // this entry somewhere else
            if (this.targets[hash_index] < 0)
            {
                if (Count == this.keys.Length)
                {
                    Expand();
                    return tryAdd(key, pureHash, value, overwrite, out existing);
                }

                int index = findReferer(hash_index);

                // looking for some free entry
                while (this.targets[this.occupied] != notUsed)
                {
                    ++this.occupied;
                }

                // we move this entry to free one
                this.keys[occupied] = this.keys[hash_index];
                this.values[this.occupied] = this.values[hash_index];
                // and reorganize the indices
                this.targets[this.occupied] = this.targets[hash_index]; // redirect bit was already set
                this.targets[index] = indexToTarget(this.occupied) | (this.targets[index] & redirectedBit); // we have to preserve redirect bit, not blindly set it
                this.targets[hash_index] = notUsed;
            }

            if (this.targets[hash_index] == notUsed)
            {
                if (Count == this.keys.Length)
                {
                    // some time later -- if we have unused slot, this condition is then false, correct?
                    throw new Exception("REMOVE ME");
                    Expand();
                    return tryAdd(key, pureHash, value, overwrite, out existing);
                }

                this.keys[hash_index] = key;
                this.values[hash_index] = value;
                this.targets[hash_index] = indexToTarget(hash_index); // this is valid (in-sync) slot so do not set redirection bit 
            }
            else
            {
                {
                    int index = hash_index;
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

                        index = targetToIndex(this.targets[index]);

                    } while (index != hash_index);
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
                this.targets[data_index] = this.targets[hash_index] | redirectedBit;
                this.targets[hash_index] = indexToTarget(data_index); // this is valid (in-sync) slot so do not set redirection bit 
            }


            ++Count;

            existing = default;
            return true;
        }

        protected abstract int targetToIndex(int target);
        protected abstract int indexToTarget(int index);


        private int findReferer(int index)
        {
            int curr_index = index;
            // searching the slot which directs to this entry
            while (true)
            {
                var dest = targetToIndex(this.targets[curr_index]);
                if (dest == index)
                    break;
                curr_index = dest;
            }

            return curr_index;
        }

        private bool tryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value, out int index, out int hashIndex)
        {
            if (Count == 0)
            {
                value = default;
                index = default;
                hashIndex = default;
                return false;
            }

            hashIndex = mod(key.GetHashCode(), this.keys.Length);

            // checking if slot is occupied and whether at least hash matches
            if (this.targets[hashIndex] != notUsed && this.targets[hashIndex] >= 0)
            {
                index = hashIndex;
                do
                {
                    if (this.comparer.Equals(keys[index], key))
                    {
                        value = this.values[index];
                        return true;
                    }

                    index = targetToIndex(this.targets[index]);
                } while (index != hashIndex);
            }

            value = default;
            index = default;
            hashIndex = default;
            return false;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return tryGetValue(key, out value, out _, out _);
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

        protected IEnumerable<KeyValuePair<TKey, TValue>> iterate()
        {
            int idx = -1;
            foreach (var target in this.targets)
            {
                ++idx;
                if (target == notUsed)
                    continue;
                yield return KeyValuePair.Create(this.keys[idx], this.values[idx]);
            }
        }

        public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (!tryGetValue(key, out value, out int del_index, out int hash_index))
                return false;

            if (del_index != targetToIndex(this.targets[del_index])) // we have multiple entries for given hash
            {
                int referer_index;

                // we are removing out-of-sync entry, so all it takes
                // is to find who is linking to this entry
                if (hash_index != del_index)
                {
                    referer_index = findReferer(del_index);
                }
                // if we are about to remove entry from its true slot (in sync)
                // we have to move something to this place,
                // because we cannot leave hole in true slot
                else
                {
                    // copy stuff from slot we are pointing to
                    var curr_dest_index = targetToIndex(this.targets[del_index]);
                    keys[del_index] = keys[curr_dest_index];
                    this.values[del_index] = this.values[curr_dest_index];
                    // and then set indices for removal to the other slot
                    referer_index = del_index;
                    del_index = curr_dest_index;
                }

                // closing the same-hash-loop/ring
                {
                    var curr_target = this.targets[del_index];
                    if (referer_index == hash_index) // if we are at in-sync position we have to clear redirect flag
                        curr_target &= indexMask;
                    else
                        curr_target |= redirectedBit;
                    this.targets[referer_index] = curr_target;
                }
            }

            this.targets[del_index] = notUsed;
            // set key and value to default so GC could reclaim their memory
            this.keys[del_index] = default(TKey)!;
            this.values[del_index] = default(TValue)!;

            --Count;
            this.occupied = Math.Min(this.occupied, del_index);

            return true;
        }
    }
}