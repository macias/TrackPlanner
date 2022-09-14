using System.Collections.Generic;

namespace TrackPlanner.Storage.Data
{
    public sealed class CompactDictionaryFill<TKey, TValue> :CompactDictionary<TKey,TValue>
        where TKey : notnull
    { 
        protected override int notUsed => int.MaxValue;

        public CompactDictionaryFill(int capacity = 0) : base( capacity)
        {
        }

        public CompactDictionaryFill(IEqualityComparer<TKey> comparer, int capacity = 0) : base(comparer,capacity)
        {
        }

        protected override int indexToTarget(int index)
        {
            return index;
        }
        protected  override  int targetToIndex(int target)
        {
            return target & indexMask;
        }
    }
}