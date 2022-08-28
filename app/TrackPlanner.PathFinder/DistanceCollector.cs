using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;


namespace TrackPlanner.PathFinder
{
    internal sealed class DistanceEstimator 
    {
        private readonly IGeoCalculator calc;
        private readonly IReadOnlyList<double> predictions;
        private readonly bool exactDistances;

        public DistanceEstimator( IGeoCalculator calc,IReadOnlyList<double> predictions, bool exactDistances)
        {
            this.calc = calc;
            this.predictions = predictions;
            this.exactDistances = exactDistances;
        }

        public void Predict(in GeoZPoint a, in GeoZPoint b,out Length directDistance,out Length routePrediction)
        {
            directDistance = this.calc.GetDistance(a,b);
            routePrediction = Predict( directDistance);
        }

        public Length Predict(Length directDistance)
        {
            if (this.exactDistances || directDistance <= Length.FromKilometers(5))
                return directDistance;

            var index = Math.Min(this.predictions.Count - 1, DistanceCollector.DirectDistanceIndexOf(directDistance));

            return directDistance * this.predictions[index];
        }
    }

    public readonly struct LinearCoefficients<TInput>
    {
        //public static LinearCoefficients Identty => new LinearCoefficients(1, 0);
        public static LinearCoefficients<TInput> Constant(double c) => new LinearCoefficients<TInput>(0, c, _ => 0);
        
        private readonly double m;
        private readonly double b;
        private readonly Func<TInput, double> selector;

        public LinearCoefficients(double m, double b,Func<TInput,double> selector)
        {
            this.m = m;
            this.b = b;
            this.selector = selector;
        }

        public double Compute(TInput x)
        {
            return m * this.selector(x) + this.b;
        }

        public override string ToString()
        {
            return $"m: {m}, b: {b}";
        }
    }
    internal sealed class LeastSquares
    {
        // https://www.mathsisfun.com/data/least-squares-regression.html
        private double xSum;
        private double ySum;
        private double xySum;
        private double x2Sum;
        private int count;

        public LeastSquares()
        {
            
        }

        public void Add(double x, double y)
        {
            this.xSum += x;
            this.ySum += y;
            this.xySum += x * y;
            this.x2Sum += x * x;
            ++this.count;
        }

        public void Compute(out double m, out double b)
        {
            m = (this.count * this.xySum - this.xSum * this.ySum) / (this.count * this.x2Sum - this.xSum * this.xSum);
            b = (this.ySum - m * this.xSum) / this.count;
        }
    }
    internal sealed class DistanceCollector 
    {
        // indices are in kilometers (direct distances), values are minimal ratios
        private readonly List<double?> storage;
        private readonly LeastSquares leastSquares;

        public DistanceCollector()
        {
            this.storage = new List<double?>();
            this.leastSquares = new LeastSquares();
        }

        public List<double> BuildPredictions(out LinearCoefficients<Length> coefficients)
        {
            this.leastSquares.Compute(out double m, out double b);
            coefficients = new LinearCoefficients<Length>(m,b,l => l.Meters);
            
            var result = new List<double>();
            result.Add(1);
            foreach (var ratio in this.storage.Skip(1))
                if (ratio==null)
                    result.Add(result.Last());
                else
                    result.Add(ratio.Value);

            return result;
        }

        public void Collect(Length directDistance, Length routeDistance)
        {
            double ratio;
            if (directDistance>Length.Zero)
                ratio = Math.Max(1.0, routeDistance / directDistance);
            else if (routeDistance == Length.Zero)
                ratio = 1.0;
            else
                throw new ArgumentException($"Invalid arguments, direct {directDistance}, route {routeDistance}");

            this.leastSquares.Add(directDistance.Meters ,ratio);
            
            var index = DirectDistanceIndexOf(directDistance);
            if (index >= this.storage.Count)
                this.storage.Expand(index + 1);

                
            if (this.storage[index] == null)
                this.storage[index] = ratio;
            else
                this.storage[index] = Math.Min(ratio, this.storage[index]!.Value);
        }

        public string GetSummary()
        {
            var active = this.storage.Where(it => it.HasValue).Select(it => it!.Value).OrderBy(it => it).ToArray();
            return $"min: {active.First()}, max {active.Max()}, med: {active[active.Length/2]}"+Environment.NewLine
                                                                                               +String.Join(", ",this.storage.Select(it => it.HasValue?it.Value.ToString("0.##"):"n"));
        }
        
        internal static int DirectDistanceIndexOf(Length directDistance)
        {
            // we try to err on safe side, thus we increase the direct distance
            return (int)Math.Ceiling(directDistance.Kilometers);
        }
    }

}