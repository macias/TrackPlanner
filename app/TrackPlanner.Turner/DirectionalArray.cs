using System;

namespace TrackPlanner.Turner

{

    public sealed class DirectionalArray<T>
    {
        public T this[int direction]
        {
            get
            {
                if (direction != -1 && direction != +1)
                    throw new ArgumentOutOfRangeException($"Direction {direction}");
                return direction == -1 ? Backward : Forward;
            }
            set
            {
                if (direction != -1 && direction != +1)
                    throw new ArgumentOutOfRangeException($"Direction {direction}");
                if (direction == -1)
                    Backward = value;
                else
                    Forward = value;
            }
        }

        private T? backward;
        internal T Backward
        {
            get { return backward ?? throw new NullReferenceException("Backward"); }
            set { backward = value; }
        }
        private T? forward;
        internal T Forward
        {
            get { return forward ?? throw new NullReferenceException("Forward"); }
            set { forward = value; }
        }


    }
}