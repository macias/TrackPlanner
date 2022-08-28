using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    [Flags]
    public enum PlaceKind
    {
        Prestart = 1,
        UserPoint = 2,
        Cross = 4,

        Node = 8,

        FinalBlob = 16, // for true start/end, not for middle user points
        Snapped = 32,
        
        Aggregate = 64,
    }


}
