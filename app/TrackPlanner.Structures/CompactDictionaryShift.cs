using System.Collections.Generic;
namespace TrackPlanner.Structures
{
    public sealed class CompactDictionaryShift<TKey, TValue> : CompactDictionary<TKey,TValue>, ICompactDictionary<TKey, TValue> 
        where TKey : notnull
    {
        protected override int notUsed => 0;

        public CompactDictionaryShift(int capacity = 0) : base(capacity)
        {
        }

        public CompactDictionaryShift(IEqualityComparer<TKey> comparer, int capacity = 0) : base(comparer,capacity)
        {
        }
        
        protected override int indexToTarget(int index)
        {
            return (index+1);
        }
        protected  override  int targetToIndex(int target)
        {
            return (target & indexMask) -1;
        }
    }
}