using TrackPlanner.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackPlanner.Turner;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;

namespace TrackPlanner.RestService.Workers
{
    public sealed class RealWorker : IWorker
    {
        private readonly ILogger logger;
        private readonly RouteManager manager;
        private readonly NodeTrackTurner turner;

        internal RealWorker(ILogger? logger, RouteManager? manager)
        {
            this.logger = logger ?? throw  new ArgumentNullException(nameof(logger));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.turner = new NodeTrackTurner(logger, manager.Map, manager.DebugDirectory!);
            logger.Info($"Starting {this}");
        }

        public bool TryComputeTrack(PlanRequest request, [MaybeNullWhen(false)] out TrackPlan plan)
        {
            //foreach (var pt in request.Points)
//                this.logger.Info($"{pt.UserPoint.Latitude} {pt.UserPoint.Longitude}");

            if (!manager.TryFindRawRoute(request.PlannerPreferences, request.GetPointsSequence().ToList(), CancellationToken.None, out List<LegRun>? legs,
                    out string? problem))
            {
                plan = null;
                return false;
            }

            var daily_turns = new List<List<TurnInfo>>();
            
            {
                int leg_offset = 0;
                for (int day_idx = 0; day_idx < request.DailyPoints.Count; ++day_idx)
                {
                    int leg_count = ScheduleLikeExtension.GetLegCount(day_idx, request.DailyPoints[day_idx].Count,
                        // the anchor is already added at the end when creating request
                        addLoopedAnchor: false);

                    daily_turns.Add(this.turner.ComputeTurnPoints(legs.Skip(leg_offset).Take(leg_count).SelectMany(leg => leg.Steps.Select(it => it.Place)),
                        request.TurnerPreferences));

                    leg_offset += leg_count;
                }
            }

            plan = this.manager.CompactRawRoute(request.PlannerPreferences, legs);
            if (problem != null)
                plan.ProblemMessage = problem;
            plan.DailyTurns = daily_turns;

            return true;
        }


    }
}
