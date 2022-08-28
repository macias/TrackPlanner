using System;
using Microsoft.AspNetCore.Components;
using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class BreakTimeInput : TimeInput
    {
        public RenderFragment BaseContent => builder => base.BuildRenderTree(builder);

        public BreakTimeInput()
        {
            CssStyle += ";width: 4em";
        }
    }
}