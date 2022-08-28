using Microsoft.AspNetCore.Components;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class MarkerRow
    {
        [Parameter, EditorRequired] public Markers Parent { get; set; } = default!;
        [Parameter, EditorRequired] public int DayIndex { get; set; } 
        [Parameter, EditorRequired] public int AnchorIndex { get; set; }
    }
}