using System;

namespace TrackPlanner.LinqExtensions
{
    public struct CacheGetter<T>
    {
        private readonly Func<T> getter;
        private Option<T> cache;

        public T Value
        {
            get
            {
                if (!this.cache.HasValue)
                    this.cache = new Option<T>(this.getter());

                return this.cache.Value;
            }
        }

        public CacheGetter(Func<T> getter)
        {
            this.getter = getter;
            this.cache = new Option<T>();
        }

        public void Reset()
        {
            this.cache = new Option<T>();
        }
    }
}