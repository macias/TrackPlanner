using System;
using System.Diagnostics.CodeAnalysis;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;

namespace TrackPlanner.RestService.Workers
{
    public sealed class DummyWorker : IWorker
    {
        private readonly ILogger? logger;
        private readonly DraftHelper helper;

        internal DummyWorker(ILogger? logger)
        {
            this.logger = logger ?? throw  new ArgumentNullException(nameof(logger));
            this.helper = new DraftHelper(new ApproximateCalculator());
            logger.Info($"Starting {this}");
        }

        public bool TryComputeTrack(PlanRequest request, [MaybeNullWhen(false)] out TrackPlan plan)
        {
            plan = this.helper.BuildDraftPlan(request);
            return true;
        }
    
    }

}