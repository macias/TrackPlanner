using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client.Pages
{
    public readonly record struct LayerReference(LegPlan LegRef, LegFragment FragmentRef)
    {
    }
}