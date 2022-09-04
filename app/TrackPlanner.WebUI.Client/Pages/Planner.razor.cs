using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Blazor.DownloadFileFast.Interfaces;
using Blazored.Modal;
using Blazored.Modal.Services;
using BlazorLeaflet;
using BlazorLeaflet.Models;
using BlazorLeaflet.Models.Events;
using Force.DeepCloner;
using Geo;
using MathUnit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TrackPlanner.Data;
using TrackPlanner.RestSymbols;
using TrackPlanner.Settings;
using TrackPlanner.WebUI.Client.Data;
using TrackPlanner.WebUI.Client.Shared;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TimeSpan = System.TimeSpan;

namespace TrackPlanner.WebUI.Client.Pages
{
    public partial class Planner : IMapManager
    {
        public Map Map { get; private set; }= default!;

        private Markers markers = default!;
        public TrackPlan Plan { get; private set; }= new ();
        // layer -> segment number
        private Dictionary<InteractiveLayer,LayerReference> legLayers = new();
        private bool alreadyRendered;

        private bool autoBuild;
        public bool AutoBuild
        {
            get { return autoBuild; }
            set
            {
                if (autoBuild == value)
                    return;
                
                autoBuild = value;
                
                // fire&forget
                #pragma warning disable CS4014
                BuildPlanAsync();
                #pragma warning restore CS4014 
            }
        }
        public bool StableRoads
        {
            get { return Program.Configuration.PlannerPreferences.UseStableRoads; }
            set
            {
                if (Program.Configuration.PlannerPreferences.UseStableRoads == value)
                    return;
                
                Program.Configuration.PlannerPreferences.UseStableRoads = value;
                
                // fire&forget
#pragma warning disable CS4014
                CompleteRebuildPlanAsync();
#pragma warning restore CS4014 
            }
        }
        private readonly DraftHelper draftHelper;
        private CommonDialog commonDialog = default!;
        private PlanWorker planWorker = default!;
        private bool trueCalculations;
        public bool TrueCalculations => this.trueCalculations;

        public string? FileName { get; set; }
        
        [Inject] public IJSRuntime JsRuntime { get; set; } = default!;
        [Inject] public RestClient Rest { get; set; } = default!;
        [Inject] public IBlazorDownloadFileService DownloadService { get; set; } = default!;
        [Inject] public IModalService Modal  { get; set; } = default!;

        public Planner()
        {
            this.draftHelper = new DraftHelper(new ApproximateCalculator());
        }
        protected override void OnAfterRender(bool firstRender)
        {
            Console.WriteLine($"OnAfterRender");
            
            base.OnAfterRender(firstRender);

            if (!this.alreadyRendered)
            {
                this.alreadyRendered = true;
            }
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            
            Console.WriteLine($"Creating map with {Program.Configuration.TileServer}");
            foreach (var entry in Program.Configuration.VisualPreferences.SpeedStyles)
                Console.WriteLine($"{entry.Key} {entry.Value.AbgrColor}");

            this.trueCalculations = Program.Configuration.Defaults.CalcReal;
            this.commonDialog = new CommonDialog(JsRuntime);
            this.planWorker = new PlanWorker(Rest);
            
            this.Map = new Map(JsRuntime)
            { 
                Center = new LatLng {Lat = 53.16844f, Lng = 18.73222f},
                Zoom = 9.8f
            };

            Console.WriteLine("Creating event");
            this.Map.OnInitialized += () =>
            {
                Console.WriteLine("Map on init");

                var tile_layer = new BlazorLeaflet.Models.TileLayer
                {
                    UrlTemplate = Program.Configuration.TileServer + "{z}/{x}/{y}.png",
                    Attribution = "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OpenStreetMap</a> contributors",
                };
                this.Map.AddLayer(tile_layer);
                Console.WriteLine("Add on click");

                this.Map.OnClick += onMapClick;

                Console.WriteLine("Map on init done");

            };

            Console.WriteLine("Main init done");
        }

        /*private async void MarkersOnOnMarkerContentChangedAsync(object? sender, Marker marker)
        {
            await LeafletInterops.UpdatePopupContent(jsRuntime, map.Id, marker);
        }*/

        public void MarkerRemoved( Marker marker)
        {
            this.Map.RemoveLayer(marker);
        }

        public void BeforeMarkersRemoved()
        {
            removeLegLayers();
            foreach (var marker in this.markers.MarkerElements)
                this.Map.RemoveLayer(marker);
        }

        public void MarkerAdded(Marker marker)
        {
            this.Map.AddLayer(marker);
            Console.WriteLine($"Map layer added");
        }


        private void DownloadAsync()
        {
          /*  if (this.Plan == null)
            {
                Console.WriteLine("There is no plan");
                return;
            }

            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                TrackPlanner.DataExchange.TrackWriter.SaveAsKml(Program.Configuration, stream,"something meaningful", this.Plan);
                bytes = stream.ToArray();
            }

            await DownloadService.DownloadFileAsync("export.kml", bytes, "application/vnd.google-earth.kml+xml");
            Console.WriteLine("done downloading");*/
        }

        private static Color getColor(LineDecoration lineDecoration)
        {
            return Color.FromArgb(lineDecoration.GetArgbColor());
        }

        private bool tryIndexOf(LegPlan leg, LegFragment fragment, out int legIndex, out int fragmentIndex)
        {
            legIndex = this.Plan.Legs.IndexOf(leg);
            fragmentIndex = leg.Fragments.IndexOf(fragment);
            if (legIndex == -1 || fragmentIndex == -1)
            {
                Console.WriteLine($"Leg={legIndex}/fragment={fragmentIndex} not found, hitting {(leg == LegPlan.Missing ? "invalidated" : (leg.IsDrafted ? "drafted" : "regular"))} leg.");
                return false;
            }
            else
                return true;
        }

        private async void LineOnOnMouseOverAsync(InteractiveLayer sender, MouseEvent e)
        {
            if (!this.legLayers.TryGetValue(sender, out LayerReference layer_idx))
            {
                Console.WriteLine($"Unknown segment");
                return;
            }
            
          //  this.plan.DEBUG_Validate();

            LegPlan active_leg = layer_idx.LegRef;
            LegFragment active_fragment = layer_idx.FragmentRef;
            if (!tryIndexOf(active_leg, active_fragment, out int leg_idx, out int fragment_idx))
                throw new ArgumentException();

                GeoHelper.SnapToFragment(GeoPoint.FromDegrees(e.LatLng.Lat, e.LatLng.Lng), active_fragment, out Length along_fragment);

            int day_starting_leg = this.markers.GetDayByLeg(this.markers.Summary,leg_idx,out SummaryDay day,out TimeSpan covered_day_breaks);
            Length running_distance = Length.Zero;
            TimeSpan running_time = TimeSpan.Zero;

            // sum up legs from start of the day
            foreach (var leg in this.Plan.Legs.Skip(day_starting_leg).Take(leg_idx-day_starting_leg))
            {
                running_distance += leg.UnsimplifiedDistance;
                var true_time = DataHelper.CalcTrueTime(running_time,leg.UnsimplifiedDistance,leg.RawTime,Program.Configuration.PlannerPreferences.GetLowRidingSpeedLimit(), Program.Configuration.PlannerPreferences.HourlyStamina );
                running_time += true_time;
            }

            {
                Length partial_leg_distance = Length.Zero;
                TimeSpan partial_leg_time = TimeSpan.Zero;
                
// and then add segments from active leg up to the active segment
                foreach (var fragment in active_leg.Fragments.Take(fragment_idx))
                {
                    partial_leg_distance += fragment.UnsimplifiedDistance;
                    partial_leg_time += fragment.RawTime;
                }

                Console.WriteLine($"LineOnOnMouseOverAsync:leg index {layer_idx.LegRef}, fragment idx {layer_idx.FragmentRef}/{active_leg.Fragments.Count}, day_starting_leg {day_starting_leg}, partial leg {TrackPlanner.Data.DataFormat.Format(partial_leg_distance,true)}/{TrackPlanner.Data.DataFormat.Format(active_leg.UnsimplifiedDistance,false)}, along {TrackPlanner.Data.DataFormat.Format( along_fragment,false)}/{TrackPlanner.Data.DataFormat.Format(active_fragment.UnsimplifiedDistance,false)}, prev anchor at {TrackPlanner.Data.DataFormat.Format(day.Start+running_time+covered_day_breaks)}, point {e.LatLng.ToPointF()}");

                // and finally the last portion along the segment
                partial_leg_distance += along_fragment;
                partial_leg_time += along_fragment / Program.Configuration.PlannerPreferences.Speeds[active_fragment.Mode];
                
                var true_time = DataHelper.CalcTrueTime(running_time,partial_leg_distance,partial_leg_time,
                    Program.Configuration.PlannerPreferences.GetLowRidingSpeedLimit(), Program.Configuration.PlannerPreferences.HourlyStamina );
                running_time += true_time;
            }
            
            running_time += covered_day_breaks;
            
            var p = new Popup
            {
                Position = e.LatLng,
                Content = $"{TrackPlanner.Data.DataFormat.Format(running_distance, withUnit:true)} @({TrackPlanner.Data.DataFormat.Format(day.Start+running_time)})"
                          + "<br/>"
                          + $"{TrackPlanner.Data.DataFormat.Format(day.Distance - running_distance,withUnit:true)} ({TrackPlanner.Data.DataFormat.Format( day.TrueDuration - running_time)})"
                          + "<br/>"
                          + $"{active_fragment.Mode}"
            };

            await p.OpenOnAsync(this.Map);
            await Task.Delay(Program.Configuration.PopupTimeout);
            await p.CloseAsync(this.Map);
        }

        public async ValueTask RebuildNeededAsync()
        {
            Console.WriteLine("Planner redrawing");

            await BuildPlanAsync(this.TrueCalculations && this.AutoBuild);

            Console.WriteLine("MarkersOnOnDraftNeededAsync done");
        }

        private void removeLegLayers()
        {
            int count = 0;
            foreach (var layer in this.legLayers.Select(it => it.Key).ToArray())
            {
                removeLegLayer(layer);
                
                ++count;
            }
            
            Console.WriteLine($"Removed {count} fragment layers.");
}

        public void RemoveLeg(int legIndex)
        {
            int count = 0;
            var leg = this.Plan.Legs[legIndex];
            foreach (var layer in this.legLayers.Where(it => it.Value.LegRef==leg).Select(it => it.Key).ToArray())
            {
                removeLegLayer(layer);
                
                ++count;
            }
            
            this.Plan.Legs.RemoveAt(legIndex);
        }
        private void removeLegLayer(InteractiveLayer layer)
        {
            layer.OnMouseOver -= LineOnOnMouseOverAsync;
            this.Map.RemoveLayer(layer);
            this.legLayers.Remove(layer);
        }

        private Task BuildPlanAsync() // for UI sake
        {
            return BuildPlanAsync(this.TrueCalculations);
        }

        private  Task CompleteRebuildPlanAsync() // for UI sake
        {
            return CompleteRebuildPlanAsync(this.TrueCalculations);
        }

       

        public async Task NewProjectAsync()
        {
            if (!await this.commonDialog.ConfirmAsync("Really start from scratch?"))
                return;
            
            this.FileName = null;
            this.Plan = new TrackPlan();
            this.markers.Clear();
        }

        
        private async Task loadScheduleAsync()
        {
            if (this.markers.VisualSchedule.IsModified)
            {
                if (!await this.commonDialog.ConfirmAsync("Current plan is modified, load anyway?"))
                    return;
            }

            var schedule_path = await Modal.ShowFileDialogAsync("Load schedule", FileDialog.DialogKind.Open);
            if (schedule_path==null)
            {
                Console.WriteLine("Modal was cancelled");
                return;
            } 
            
            Console.WriteLine("Sending load rquest");
            long start= Stopwatch.GetTimestamp();
            var (failure, schedule) = await Rest.GetAsync<ScheduleJourney>(Url.Combine(Program.Configuration.PlannerServer, Routes.Planner, Methods.Get_LoadSchedule),
                new RestQuery().Add(Parameters.Path,schedule_path),
                CancellationToken.None);
            if (failure != null)
            {
                Console.WriteLine(failure);
            }
            else
            {
                Console.WriteLine($"Data loaded successfuly in {(Stopwatch.GetTimestamp()-start)/Stopwatch.Frequency}s");
                this.FileName = schedule_path;
                Program.Configuration.PlannerPreferences.UseStableRoads = schedule!.PlannerPreferences.UseStableRoads;
                this.Plan = schedule.TrackPlan;
                this.markers.SetSchedule(schedule);
                recreateLegLayers();
                this.markers.ResetSummary();
                
                Console.WriteLine("Calling state has changed");
            }
        }

        private ScheduleJourney createJourneySchedule(bool onlyPinned)
        {
            var schedule = new ScheduleJourney
            {
                Days = this.markers.GetSchedule(onlyPinned),
                StartsAtHome = this.markers.VisualSchedule.StartsAtHome,
                EndsAtHome = this.markers.VisualSchedule.EndsAtHome,
                IsLooped = this.markers.IsLooped,
                TrackPlan = this.Plan,
                PlannerPreferences = this.markers.VisualSchedule.PlannerPreferences.DeepClone(),
                TurnerPreferences = this.markers.VisualSchedule.TurnerPreferences.DeepClone(),
                VisualPreferences = this.markers.VisualSchedule.VisualPreferences.DeepClone(),
            };

            return schedule;
        }

        private async Task saveScheduleAsync()
        {
            Console.WriteLine($"Filename is null {FileName==null}");
            if (this.FileName == null)
            {
                var schedule_path = await Modal.ShowFileDialogAsync("Save schedule", FileDialog.DialogKind.Save);
                if (schedule_path == null)
                {
                    Console.WriteLine("Modal was cancelled");
                    return;
                }

                if (System.IO.Path.GetExtension(schedule_path).ToLowerInvariant() != SystemCommons.ProjectFileExtension)
                    schedule_path += SystemCommons.ProjectFileExtension;

                this.FileName = schedule_path;
            }

            var schedule = createJourneySchedule(onlyPinned:false);

            var (failure, _) = await Rest.PostAsync<ValueTuple>(Url.Combine(Program.Configuration.PlannerServer, Routes.Planner, Methods.Post_SaveSchedule),
               new SaveRequest() { Schedule = schedule, Path = this.FileName}, CancellationToken.None);
            if (failure != null)
            {
                Console.WriteLine(failure);
            }
            else
            {
                Console.WriteLine("Save successful.");
                this.markers.VisualSchedule.IsModified = false;
            }
        }
        
        private void recreateLegLayers()
        {
            Console.WriteLine("Planner drawing plan");

            removeLegLayers();

            //Console.WriteLine(plan!.Track.Length);
                addMapLegs(Plan.Legs);

            Console.WriteLine("Layers added");
        }

        private void addMapLegs(IReadOnlyList< LegPlan> legs)
        {
            
            foreach (var leg in legs)
            {
                foreach (var fragment in leg.Fragments)
                {
                    var deco = fragment.IsForbidden ? Program.Configuration.VisualPreferences.ForbiddenStyle : Program.Configuration.VisualPreferences.SpeedStyles[fragment.Mode];

                    var line = new Polyline();
                    line.StrokeColor = getColor(deco);
                    line.StrokeWidth = deco.Width;
                    line.Fill = false;

                    var shape = new PointF[1][];
                    int size = fragment.Places.Count;
                    shape[0] = new PointF[size];
                    for (int i = 0; i < size; ++i)
                    {
                        double lat = fragment.Places[i].Point.Latitude.Degrees;
                        double lon = fragment.Places[i].Point.Longitude.Degrees;
                        shape[0][i] = new PointF((float) lat, (float) lon);
                    }

                    line.Shape = shape;
                    line.OnMouseOver += LineOnOnMouseOverAsync;

                    this.Map.AddLayer(line);
                    this.legLayers.Add(line, new LayerReference(leg, fragment));
                }
            }

            Console.WriteLine($"We have {legs.Count} legs, added as {this.legLayers.Count} fragments to the map.");
        }

        private async ValueTask<TrackPlan?> getPlanAsync(PlanRequest request,bool calcReal)
        {
            try
            {
                var (failure, new_plan) = await this.planWorker.GetPlanAsync(request, calcReal, CancellationToken.None);

                if (failure!=null)
                {
                    Console.WriteLine(failure);
                    await this.commonDialog.AlertAsync("Failure");
                }
                else if (new_plan?.Legs == null)
                    await this.commonDialog.AlertAsync("Computing route failed");
                else
                {
                    Console.WriteLine("Plan received.");
                    failure = new_plan.DEBUG_Validate();
                    if (failure!=null)
                        await this.commonDialog.AlertAsync($"Received plan is invalid: {failure}");
                    return new_plan;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await this.commonDialog.AlertAsync("Error");
            }

            return null;
        }

        
        /*private PlanRequest buildPlanRequest()
        {
            var user_points = markers.Elements.Select(m => GeoPoint.FromDegrees(latitude: m!.Position.Lat, longitude: m.Position.Lng)).ToList();
            if (this.markers.IsLoopedRoute)
                user_points.Add(user_points.First());

            var plan_request = new PlanRequest()
            {
                UserPoints = user_points.ToArray(),
                Preferences = Program.Configuration.UserPrefs,
            };
            
            return plan_request;
        }*/

        private async void onMapClick(object sender, MouseEvent e)
        {
            Console.WriteLine($"Clicked on {e.LatLng.ToPointF()}");
            if (!await this.markers.AddMarkerAsync( e.LatLng))
                return;

            StateHasChanged();
        }

        private async Task CompleteRebuildPlanAsync(bool calcReal) 
        {
            if (!this.markers.HasEnoughPointsForBuild())
            {
                Console.WriteLine("Not enough points to build a plan.");
                return;
            }

            TrackPlan? new_plan = await getPlanAsync(createJourneySchedule(onlyPinned:true).BuildPlanRequest(),calcReal);
            if (new_plan != null)
            {
                this.Plan = new_plan;
                recreateLegLayers();

                this.markers.RebuildAutoAnchors();
                this.markers.ResetSummary();
            }

            Console.WriteLine($"Complete rebuild: done.");
        }

        private async Task BuildPlanAsync(bool calcReal) 
        {
            if (this.Plan.IsEmpty)
            {
                await CompleteRebuildPlanAsync(calcReal);
                return;
            }

            if (!this.markers.HasEnoughPointsForBuild())
            {
                Console.WriteLine("Not enough points for refresh build.");
                return;
            }

            var anchors_count = markers.AnchorElements.Count();

            Console.WriteLine($"DEBUG building only needed legs, in total {this.Plan.Legs.Count}, needed {this.Plan.Legs.Count(it => it.IsDrafted)}");
            var planner_prefs = Program.Configuration.PlannerPreferences.DeepClone();
            var turner_prefs = Program.Configuration.TurnerPreferences.DeepClone();

            var replacements = new List<(int index, List<LegPlan> legs)>();

            {
                var DEBUG_anchors = this.markers.AnchorElements.ToList();
                if (this.markers.IsLooped)
                    DEBUG_anchors.Add(DEBUG_anchors[0]);
                
                var leg_idx = -1;
                foreach ((GeoPoint prev, GeoPoint next) in createJourneySchedule(onlyPinned:false).BuildPlanRequest().GetPointsSequence()
                             .Select(it => it.UserPoint).Slide())
                {
                    ++leg_idx;
                    if (!this.Plan.Legs[leg_idx].NeedsRebuild(calcReal))
                        continue;

                    Console.WriteLine($"DEBUG, BuildPlanAsync {leg_idx}/{this.Plan.Legs.Count}, count {anchors_count}, markers loop {this.markers.IsLooped}");
                    if (!DEBUG_anchors[leg_idx].IsPinned || !DEBUG_anchors[leg_idx + 1].IsPinned)
                    {
                        var (DEBUG_day_idx, DEBUG_anchor_idx,_) = this.markers.LegIndexToStartingDayAnchor(leg_idx);
                        throw new InvalidOperationException($"Impossible scenario, partial rebuilding on auto-anchors leg:{leg_idx} = {DEBUG_day_idx}:{DEBUG_anchor_idx} with {DEBUG_anchors[leg_idx].IsPinned} and {DEBUG_anchors[leg_idx + 1].IsPinned} of {this.markers.DEBUG_PinsToString()}.");
                    }

                    var request = new PlanRequest()
                    {
                        PlannerPreferences = planner_prefs,
                        TurnerPreferences = turner_prefs,
                        DailyPoints = new List<List<RequestPoint>>()
                        {
                            new List<RequestPoint>() {new RequestPoint( prev, allowSmoothing:false), 
                                new RequestPoint( next, allowSmoothing:false)}
                        }
                    };

                    TrackPlan? partial_plan = await getPlanAsync(request, calcReal);
                    if (partial_plan == null)
                        continue;

                    replacements.Add((leg_idx, partial_plan.Legs));
                }
            }

            foreach (var (idx, legs) in replacements.AsEnumerable().Reverse())
            {
                this.Plan.Legs.RemoveAt(idx);
                this.Plan.Legs.InsertRange(idx, legs);
                addMapLegs(legs);
            }


            var current_legs = this.Plan.Legs.ToHashSet();
            // remove all invalid now (pointing out to removed legs) layers
            int remove_count = 0;
            foreach (var layer in this.legLayers.Where(it => !current_legs.Contains(it.Value.LegRef)).Select(it => it.Key).ToArray())
            {
                ++remove_count;
                removeLegLayer(layer);
            }
            
            Console.WriteLine($"DEBUG build-needed removed {remove_count} leg fragment layers");

            this.markers.RebuildAutoAnchors();
            this.markers.ResetSummary();
        }


    }
}