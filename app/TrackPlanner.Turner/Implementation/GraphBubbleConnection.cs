using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;


namespace TrackPlanner.Turner.Implementation
{
    internal sealed class GraphBubbleConnection
    {
        private static int DEBUG_COUNTER;
        private readonly int DEBUG_ID = DEBUG_COUNTER++;

        // two points can be connected by two roads
        // https://www.openstreetmap.org/way/361920778
        // https://www.openstreetmap.org/way/361777752
        // and not always shorter is the best -- shorter can mean service road with lower priority, so "saving" on few meters is lost when waiting on getting back to the main road

        // road id -> distance
        private readonly Dictionary<long, Length> dict;

        // in-place "movement" are possible, because some nodes are shared between bucket/layers
        private bool inPlace;

        public GraphBubbleConnection()
        {
            this.dict = new Dictionary<long, Length>();
        }

        internal void Add(long? roadId, Length onRoadTravelDistance)
        {
            if (roadId == null)
            {
                if (onRoadTravelDistance != Length.Zero)
                    throw new ArgumentException();
                if (this.dict.Any())
                    throw new ArgumentException();

                this.inPlace = true;
            }
            else
            {
                if (this.inPlace)
                    throw new ArgumentException();

                if (this.DEBUG_ID == 5)
                {
                    ;
                }
                if (!this.dict.TryGetValue(roadId.Value, out Length dist))
                {
                    this.dict.Add(roadId.Value, onRoadTravelDistance);
                }
                else if (dist != onRoadTravelDistance)
                    throw new ArgumentException($"#{DEBUG_ID}: {roadId} we have distance {dist}, new one is {onRoadTravelDistance}");
            }
        }

        internal IEnumerable<(long? roadId, Length onRoadTravelDistance)> GetEntries()
        {
            if (this.inPlace)
                return new[] { ((long?)null, Length.Zero) };
            else
                return this.dict.Select(it => ((long?)it.Key, it.Value));
        }

    }
}