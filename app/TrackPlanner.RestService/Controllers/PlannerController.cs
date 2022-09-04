using TrackPlanner.Data.Stored;
using Microsoft.AspNetCore.Mvc;
using TrackPlanner.RestSymbols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Data.Serialization;
using TrackPlanner.RestService.Workers;
using TrackPlanner.Settings;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using DataFormat = TrackPlanner.Data.DataFormat;

namespace TrackPlanner.RestService.Controllers
{
    [ApiController]
    [Route(Routes.Planner)]
    public sealed class PlannerController : ControllerBase
    {
        private readonly RestServiceConfig serviceConfig;
        private const string schedulesSubdirectory = "schedules";
        private readonly string baseDirectory;
        private readonly ILogger logger;
        private readonly IWorker worker;
        private readonly ProxySerializer serializer;

        internal PlannerController(ILogger? logger, IWorker? worker, RestServiceConfig serviceConfig, string baseDirectory)
        {
            this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
            this.serviceConfig = serviceConfig;
            this.baseDirectory = System.IO.Path.GetFullPath(baseDirectory);
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.serializer = new ProxySerializer();
            
            System.IO.Directory.CreateDirectory( System.IO.Path.Combine(this.baseDirectory, schedulesSubdirectory));
        }


        [HttpGet(Methods.Get_About)]
        public string About()
        {
            this.logger.Info("Received about request");
            return "Hello world";
        }

        [HttpPost(Methods.Post_SaveSchedule)]
        public ActionResult SaveFullSchedule([FromBody] SaveRequest request)
        {
            string full_path = saveSchedule(request.Schedule, request.Path);

            ConvertToKml(full_path, request.Schedule,turnsMode:false);
            if (request.Schedule.TrackPlan.DailyTurns.Any()) // checking whether turns were computed
                ConvertToKml(full_path, request.Schedule,turnsMode:true);

            SaveSummary(full_path, request.Schedule);

            return Ok();
        }

        public string saveSchedule(ScheduleJourney schedule, string relativePath)
        {
            var full_path = System.IO.Path.Combine(this.baseDirectory, schedulesSubdirectory, relativePath);

            using (var stream = new FileStream(full_path, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(stream))
            {
                sw.Write(serializer.Serialize(schedule));
            }

            return full_path;
        }

        internal void ConvertToKml(string path, ScheduleJourney? schedule,bool turnsMode)
        {
            if (schedule == null)
                schedule = this.serializer.Deserialize<ScheduleJourney>(System.IO.File.ReadAllText(path))!;
            var core_path = System.IO.Path.ChangeExtension(path, null);

            for (int day_idx = 0; day_idx < schedule.Days.Count; ++day_idx)
            {
                var legs = schedule.GetDayLegs(day_idx).ToList();

                // we cannot use ".day.kml" (i.e. with dot) pattern because Google Maps fails with "not supported format" (2022-05-31)
                using (var stream = new FileStream($"{core_path}-day-{TrackPlanner.Data.DataFormat.Adjust(day_idx + 1, schedule.Days.Count)}{(turnsMode?"-turns":"")}.kml", FileMode.Create)) // allowing overwrite
                {
                    var title = $"Day-{day_idx + 1} {TrackPlanner.Data.DataFormat.Format(legs.Select(it => it.RawTime).Sum())}, {TrackPlanner.Data.DataFormat.Format(legs.Select(it => it.UnsimplifiedDistance).Sum(), withUnit: true)}";
                    List<TurnInfo>? turns = null;
                    if (turnsMode) 
                        turns = schedule.TrackPlan.DailyTurns[day_idx];
                    TrackPlanner.DataExchange.TrackWriter.SaveAsKml(schedule.VisualPreferences, stream, title, legs, turns);
                }
            }
        }


        internal void SaveSummary(string path, ScheduleJourney schedule)
        {
            var core_path = System.IO.Path.ChangeExtension(path, null);

            SummaryJourney summary = schedule.GetSummary();

            int day_idx = -1;
            foreach (var day in summary.Days)
            {
                ++day_idx;

                using (var stream = new FileStream($"{core_path}-{TrackPlanner.Data.DataFormat.Adjust(day_idx + 1, summary.Days.Count)}-summary.html", FileMode.Create)) // allowing overwrite
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(@$"<!DOCTYPE html>
<html>
                            <head>
                        <meta charset='utf-8'>
                        <title>Day {day_idx + 1} summary</title>
                        </head>
<body style='background-color: {this.serviceConfig.GetSummaryActiveTheme().BackgroundColor}; color:{this.serviceConfig.GetSummaryActiveTheme().TextColor}'>");

                        writer.WriteLine($"<h3>Day {day_idx + 1} summary</h3>");

                        int pt_idx = -1;
                        foreach (var checkpoint in day.Checkpoints)
                        {
                            ++pt_idx;
                            // writing surfaces
                            if (checkpoint.IncomingLegIndex is { } leg_idx)
                            {
                                var leg = schedule.TrackPlan.Legs[leg_idx];
                                writer.Write("<div><small><i>");
                                // surface info
                                writer.Write(String.Join(", ", leg.Fragments.Partition(it => it.Mode)
                                    .Select(it => $"{it.First().Mode.ToString().ToLowerInvariant()} {TrackPlanner.Data.DataFormat.Format(it.Select(x => x.UnsimplifiedDistance).Sum(), withUnit: true)}")));
                                writer.WriteLine("</i></small></div>");
                            }

                            writer.Write($"<div><b>{pt_idx + 1}. {checkpoint.Label}</b> ");
                            if (pt_idx == 0)
                                writer.Write($"{TrackPlanner.Data.DataFormat.Format(checkpoint.Arrival)}");
                            else
                                writer.Write($"{TrackPlanner.Data.DataFormat.Format(checkpoint.IncomingDistance, true)} at {TrackPlanner.Data.DataFormat.Format(checkpoint.Arrival)}");
                            if (checkpoint.Break != TimeSpan.Zero)
                                writer.Write($" &gt;&gt; {Data.DataFormat.Format(checkpoint.Departure)}");
                            writer.WriteLine("</div>");
                            {
                                var events = String.Join(", ", Enum.GetValues<TripEvent>().Select(it =>
                                    {
                                        var count = checkpoint.EventCount[it];
                                        if (count == 0)
                                            return "";

                                        return $"{it.GetLabel()}: {(count == 1 ? "" : $"{count} ")}({DataFormat.Format(summary.PlannerPreferences.EventDuration[it] * count)}";
                                    })
                                    .Where(it => it != ""));
                                
                                if (events!="")
                                {
                                    writer.WriteLine("<div>");
                                    writer.Write($"<i>{events}</i>");
                                    writer.WriteLine("</div>");
                                }
                            }
                        }

                        writer.Write($"<div><b>In total:</b> {TrackPlanner.Data.DataFormat.Format(day.Distance, true)}");
                        if (day.LateCampingBy.HasValue)
                            writer.Write($", <span style='color:{this.serviceConfig.GetSummaryActiveTheme().WarningTextColor}'><b>running late by {TrackPlanner.Data.DataFormat.Format(day.LateCampingBy.Value)}</b></span>");
                        writer.Write($"</div>");
                        writer.WriteLine(@" </body>
</html>");
                    }
                }
            }
        }

        [HttpPut(Methods.Put_ComputeTrack)]
        public TrackPlan ComputeTrack([FromBody] PlanRequest request)
        {
            this.logger.Info($"Received turner config : {request.TurnerPreferences}");

            if (!this.worker.TryComputeTrack(request, out TrackPlan? route))
            {
                this.logger.Info("Didn't find any route");
                return new TrackPlan();
            }
            else
            {
                this.logger.Info("Route was found");

                Console.WriteLine($"First legs are : {(String.Join(", ", route.Legs.Take(5).Select(it => it.UnsimplifiedDistance).Select(it => TrackPlanner.Data.DataFormat.Format(it, false))))}");
                return route;
            }
        }

        [HttpGet(Methods.Get_GetDirectory)]
        public ActionResult<DirectoryData> GetDirectoryEntries([FromQuery(Name = Parameters.Directory)] string? directory = null)
        {
            var main_dir = System.IO.Path.Combine(this.baseDirectory, schedulesSubdirectory,
                // for frontend it is root directory, but for us it is relative path
                (directory ?? "").TrimStart('/', '\\'));
            // get only relative portion
            var directories = System.IO.Directory.GetDirectories(main_dir).Select(it => System.IO.Path.GetFileName(it)!).OrderBy(it => it.ToLowerInvariant()).ToArray();
            var files = System.IO.Directory.GetFiles(main_dir, "*" + SystemCommons.ProjectFileExtension).Select(it => System.IO.Path.GetFileName(it)!)
                .OrderBy(it => it!.ToLowerInvariant()!).ToArray();
            return new DirectoryData() {Directories = directories, Files = files};
        }

        internal bool TryLoadSchedule(string path, [MaybeNullWhen(false)] out ScheduleJourney schedule)
        {
            long start = Stopwatch.GetTimestamp();

            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(this.baseDirectory, schedulesSubdirectory, path));
            long loaded = Stopwatch.GetTimestamp();
            schedule = this.serializer.Deserialize<ScheduleJourney>(json);
            this.logger.Verbose($"Loaded in {(loaded - start) / Stopwatch.Frequency}s, deserialized in {(Stopwatch.GetTimestamp() - loaded) / Stopwatch.Frequency}s");

            if (schedule == null)
                return false;
            if (schedule.PlannerPreferences == null)
            {
                this.logger.Warning($"No preferences saved, using defaults.");
                schedule.PlannerPreferences = UserPlannerPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();
            }

            return true;
        }

        [HttpGet(Methods.Get_LoadSchedule)]
        public ActionResult<ScheduleJourney> LoadSchedule([FromQuery(Name = Parameters.Path)] string path)
        {
            if (TryLoadSchedule(path, out var schedule))
                return schedule;
            else
                return NotFound();
        }
    }
}