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

        internal RealWorker(ILogger? logger, RouteManager? manager)
        {
            this.logger = logger ?? throw  new ArgumentNullException(nameof(logger));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            logger.Info($"Starting {this}");
        }

        public bool TryComputeTrack(PlanRequest request, [MaybeNullWhen(false)] out TrackPlan plan)
        {
            //foreach (var pt in request.Points)
//                this.logger.Info($"{pt.UserPoint.Latitude} {pt.UserPoint.Longitude}");

            if (!manager.TryFindFlattenRoute(request.RouterPreferences, request.GetPointsSequence().ToList(), CancellationToken.None, out List<LegRun>? legs,
                    out string? problem))
            {
                plan = null;
                return false;
            }

            var turner = new NodeTurnWorker(logger, manager.Map, 
                new SystemTurnerConfig(){  DebugDirectory = manager.DebugDirectory!},
                request.TurnerPreferences);

            var daily_turns = new List<List<TurnInfo>>();
            
            {
                int leg_offset = 0;
                for (int day_idx = 0; day_idx < request.DailyPoints.Count; ++day_idx)
                {
                    int leg_count = ScheduleLikeExtension.GetLegCount(day_idx, request.DailyPoints[day_idx].Count,
                        // the anchor is already added at the end when creating request
                        addLoopedAnchor: false);

                    daily_turns.Add(turner.ComputeTurnPoints(legs.Skip(leg_offset).Take(leg_count)
                        .SelectMany(leg => leg.Steps.Select(it => it.Place)), ref problem));

                    leg_offset += leg_count;
                }
            }

            plan = this.manager.CompactFlattenRoute(request.RouterPreferences, legs);
            if (problem != null)
                plan.ProblemMessage = problem;
            plan.DailyTurns = daily_turns;

            return true;
        }


    }
}
