using System;
using System.Collections.Generic;
using MathUnit;
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    // leg are the segments from anchor to anchor
    public sealed  class DirectoryData
    {
        public string[] Directories { get; set; } = default!;
        public string[] Files { get; set; } = default!;

        public DirectoryData()
        {
        }
    }
}
