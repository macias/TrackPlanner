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
    public readonly struct Weight : IEquatable<Weight>//, IComparable<Weight>
    {
        private readonly double estimateRatio;
        private  readonly Length rawRemainingDistance;
        public Length ScaledRemainingDistance => this.rawRemainingDistance * this.estimateRatio;
        public TravelCost CurrentTravelCost { get; }

        // for forbidden parts it is better to use length instead of time, because if we pick such route it is most likely we need to
        // ride around it, or walk through, so then length and not speed (asphalt/sand) is important
        public Length CurrentForbiddenDistance { get; }
        public Length UnstableDistance { get; }

        private Weight(Length forbiddenDistance,Length unstableDistance, TravelCost currentTravelCost, Length rawRemainingDistance, double estimateRatio)
        {
            this.estimateRatio = estimateRatio;
            this.UnstableDistance = unstableDistance;
            CurrentTravelCost = currentTravelCost;
            this.rawRemainingDistance = rawRemainingDistance;
            CurrentForbiddenDistance = forbiddenDistance;
        }
        
        public Weight(Length forbiddenDistance,Length unstableDistance, TravelCost currentTravelCost, Length rawRemainingDistance, in LinearCoefficients<Length> estimateRatio)
        : this(forbiddenDistance,unstableDistance,currentTravelCost,rawRemainingDistance,estimateRatio.Compute(rawRemainingDistance))
        {
        }

        public static Weight Join(in Weight a, in Weight b)
        {
            if (a.estimateRatio != b.estimateRatio)
                throw new ArgumentException();
        
            return new Weight(a.CurrentForbiddenDistance + b.CurrentForbiddenDistance,
                a.UnstableDistance+b.UnstableDistance,
                a.CurrentTravelCost + b.CurrentTravelCost, 
                Length.Zero, 
                a.estimateRatio);
        }

        public TravelCost GetTotalTimeCost(double runningScale, double estimatedScale,Speed estimatedSpeed)
        {
            return this.CurrentTravelCost * runningScale 
                   + TravelCost.Create(ScaledRemainingDistance/estimatedSpeed,costScale: 1.0) * estimatedScale;
        }

        public override string ToString()
        {
            return $"W{CurrentTravelCost.EquivalentInMinutes}:{(ScaledRemainingDistance.Kilometers.ToString("0.#"))}({(this.estimateRatio.ToString("0.##"))}) F{(int)CurrentForbiddenDistance.Meters}";
        }

        public override bool Equals(object? obj)
        {
            return obj is Weight weight && Equals(weight);
        }

        public bool Equals(Weight other)
        {
            return ScaledRemainingDistance==other.ScaledRemainingDistance 
                && CurrentTravelCost == other.CurrentTravelCost
                && CurrentForbiddenDistance == other.CurrentForbiddenDistance;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ScaledRemainingDistance, CurrentTravelCost, CurrentForbiddenDistance);
        }

        public static bool operator ==(Weight left, Weight right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Weight left, Weight right)
        {
            return !(left == right);
        }
    }
}