using MathUnit;
using System;
using System.Runtime.InteropServices;

namespace TrackPlanner.PathFinder
{
    // A* algorithm with pairing heap
    // https://en.wikipedia.org/wiki/A*_search_algorithm
    // https://brilliant.org/wiki/pairing-heap/
    // https://en.wikipedia.org/wiki/Pairing_heap

    [StructLayout(LayoutKind.Auto)]
    public readonly struct TravelCost : IEquatable<TravelCost>, IComparable<TravelCost>
    {
        public static TravelCost Create(TimeSpan time, double costScale) 
        {
            if (time < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException($"{nameof(time)} = {time}");
            if (costScale < 0)
                throw new ArgumentOutOfRangeException($"{nameof(costScale)} = {costScale}");

            return new TravelCost(time.TotalSeconds * costScale);
        }

        public static TravelCost Zero => default;
        public static TravelCost MaxValue => new TravelCost(double.MaxValue);

        private readonly double cost;
        public int EquivalentInMinutes => (int)Math.Round(cost / 60);

        private TravelCost(double cost)
        {
            this.cost = cost;
        }

        public static TravelCost operator +(TravelCost left, TravelCost right)
        {
            return new TravelCost(left.cost + right.cost);
        }

        public static TravelCost operator *(TravelCost left, double scale)
        {
            return new TravelCost(left.cost * scale);
        }

        public override bool Equals(object? obj)
        {
            return obj is TravelCost cost && Equals(cost);
        }

        public bool Equals(TravelCost other)
        {
            return cost == other.cost;
        }

        public override int GetHashCode()
        {
            return cost.GetHashCode();
        }

        public int CompareTo(TravelCost other)
        {
            return this.cost.CompareTo(other.cost);
        }

        public static bool operator ==(TravelCost left, TravelCost right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TravelCost left, TravelCost right)
        {
            return !(left == right);
        }

        public static bool operator <(TravelCost left, TravelCost right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(TravelCost left, TravelCost right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(TravelCost left, TravelCost right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(TravelCost left, TravelCost right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

}