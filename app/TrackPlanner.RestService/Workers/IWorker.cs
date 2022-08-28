using System.Diagnostics.CodeAnalysis;
using TrackPlanner.Data;

namespace TrackPlanner.RestService.Workers
{
    public interface IWorker
    {
        bool TryComputeTrack(PlanRequest request, [MaybeNullWhen(false)] out TrackPlan plan);
    }
}