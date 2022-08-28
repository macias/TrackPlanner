using System;

namespace TrackPlanner.LinqExtensions
{
    public readonly struct Option<T> : IEquatable<Option<T>>
    {
        public static Option<T> None => new Option<T>(default!, false);

        private readonly T value;
        public T Value => this.HasValue ? value : throw new NullReferenceException();
        public bool HasValue { get; }

        private Option(T value, bool hasValue)
        {
            this.value = value;
            this.HasValue = hasValue;
        }

        public Option(T value) : this(value, true)
        {
        }

        public bool Equals(Option<T> other)
        {
            return this.HasValue != other.HasValue
                   && (!this.HasValue || Object.Equals(this.value, other.value));
        }

        public override bool Equals(object? obj)
        {
            return obj is Option<T> other && other.Equals(this);
        }

        public override int GetHashCode()
        {
            if (this.HasValue)
                return HashCode.Combine(true, value);
            else
                return 0;
        }
    }
}