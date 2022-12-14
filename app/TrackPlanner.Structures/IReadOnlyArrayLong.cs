using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TrackPlanner.Structures
{
    public interface IReadOnlyArrayLong<TValue> : IReadOnlyMap<long, TValue>
    {
         // this interface guarantees linear indexing (like array)
    }
}