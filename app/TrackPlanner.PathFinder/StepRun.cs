using MathUnit;
using System;

namespace TrackPlanner.PathFinder
{
    public readonly struct StepRun
    {
        public Placement Place { get; }
        public long IncomingRoadMapIndex { get; } // in case of starting point it is actually the outgoing road (because there is no incoming one)
        public RoadCondition IncomingCondition { get; }
        public Length IncomingDistance { get; }
        public TimeSpan IncomingTime { get; }

        public StepRun(Placement place, long incomingRoadMapIndex, RoadCondition incomingCondition, Length incomingDistance, TimeSpan incomingTime)
        {
            Place = place;
            IncomingRoadMapIndex = incomingRoadMapIndex;
            IncomingCondition = incomingCondition;
            IncomingDistance = incomingDistance;
            IncomingTime = incomingTime;
        }

        public static StepRun RecreateAsInitial(in StepRun firstStep, in StepRun nextStep)
        {
            return new StepRun(firstStep.Place, nextStep.IncomingRoadMapIndex, nextStep.IncomingCondition, Length.Zero, TimeSpan.Zero);
        }
    }
}