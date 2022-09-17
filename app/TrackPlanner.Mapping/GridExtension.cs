using MathUnit;
using System.Collections.Generic;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public static class GridExtension
    {
        public static CellIndex GetCellIndex(this IGrid grid,Angle latitude, Angle longitude)
        {
            return new CellIndex(latitudeGridIndex: (int) (latitude.Degrees * grid.CellSize),
                longitudeGridIndex: (int) (longitude.Degrees * grid.CellSize));
        }
    }
}