using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BlazorLeaflet;
using BlazorLeaflet.Models;
using BlazorLeaflet.Models.Events;
using Geo;
using MathUnit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TrackPlanner.Data;
using TrackPlanner.WebUI.Client.Data;
using TrackPlanner.WebUI.Client.Pages;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using VisualAnchor = TrackPlanner.WebUI.Client.Data.VisualAnchor<BlazorLeaflet.Models.Marker>;
using VisualDay = TrackPlanner.WebUI.Client.Data.VisualDay<BlazorLeaflet.Models.Marker,bool>;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class Markers : IDisposable,ISchedule<VisualDay,VisualAnchor>
    {
        private LatLng onDragMarkerLatLng = new LatLng { Lat = 47.5574007f, Lng = 16.3918687f };

        private (int day,int index)? anchorPlaceholder;

        public IEnumerable<Marker> MarkerElements => this.Days.SelectMany(it=> it.Anchors).Select(it => it.Visual);
        public IEnumerable<IReadOnlyAnchor> AnchorElements => this.Days.SelectMany(it=> it.Anchors);

        private readonly EditContext editContext;

        public VisualSchedule<BlazorLeaflet.Models.Marker,bool> VisualSchedule { get; }
        UserPlannerPreferences IReadOnlySchedule.PlannerPreferences => this.VisualSchedule.PlannerPreferences;
        UserTurnerPreferences IReadOnlySchedule.TurnerPreferences => this.VisualSchedule.TurnerPreferences;
        public List<VisualDay> Days => this.VisualSchedule.Days;
        IReadOnlyList<IReadOnlyDay> IReadOnlySchedule.Days => this.Days;
        TrackPlan IReadOnlySchedule.TrackPlan => this.plan;
         bool IReadOnlySchedule.StartsAtHome => this.VisualSchedule.StartsAtHome;
         bool IReadOnlySchedule.EndsAtHome => this.VisualSchedule.EndsAtHome;
        
        private bool isLooped;
        public bool IsLooped
        {
            get { return this.isLooped; }
            private set { setIsLoopedAsync(value); }

        }

        private async void setIsLoopedAsync(bool value)
        {
            if (this.isLooped == value)
                return;

            this.isLooped = value;
            if (this.Days[0].Anchors.Count>0)
                recreateMarkers(this.Days.Count - 1, this.Days[^1].Anchors.Count - 1);

            if (!this.plan.IsEmpty)
            {
                if (value)
                {
                    this.plan.Legs.Add(LegPlan.Missing);
                }
                else
                {
                    this.plan.Legs.RemoveLast();
                }
            }

            if (value && this.VisualSchedule.StartsAtHome)
                VisualSchedule.EndsAtHome = true;

            await rebuildNeededAsync();
        
        }

        private TrackPlan plan => this.MapManager.Plan;
        private  CacheGetter<SummaryJourney> summary;
        private CommonDialog commonDialog = default!;
        public SummaryJourney Summary => this.summary.Value;

        [Parameter] public IMapManager MapManager { get; set; } = default!;
        [Inject] public IJSRuntime JsRuntime { get; set; } = default!;
        
        public Markers()
        {
            this.editContext = new EditContext(this);
            this.editContext.OnFieldChanged += EditContextOnOnFieldChanged;
            this.VisualSchedule = new VisualSchedule<BlazorLeaflet.Models.Marker,bool>(Program.Configuration.PlannerPreferences,
                Program.Configuration.TurnerPreferences,Program.Configuration.VisualPreferences);
            this.summary = new CacheGetter<SummaryJourney>(this.GetSummary);
        }

        public void Dispose()
        {
            this.editContext.OnFieldChanged -= EditContextOnOnFieldChanged;
        }


        private void onDayToggled(AccordionToggle toggle) // this one was already toggled
        {
            if (!toggle.Collapsed) // if it is open now
            {
                // collapse all others
                int index = 0;
                foreach (var day in this.Days)
                {
                   // if (toggle != day.Accordion)
                   if (!index.Equals(toggle.Tag))
                    {
                        Console.WriteLine($"Collapsing day accordion {index}");
                        day.DEBUG_SetVisual(true);
                    }
                    else
                    {
                        Console.WriteLine($"At {index} there is active toggle, internal {toggle.Collapsed}, bound value {day.Visual}");
                    }

                    ++index;
                }
            }
            
            Console.WriteLine("Triggering toggle render for markers");
           // StateHasChanged();
        }
        
        private void EditContextOnOnFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            Console.WriteLine("editContext: Some of the fields changed");
            ResetSummary();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            this.commonDialog = new CommonDialog(JsRuntime);
            
            DataInitialization();
        }

        public void DataInitialization()
        {
            this.isLooped = Program.Configuration.Defaults.LoopRoute;
            this.VisualSchedule.StartsAtHome = Program.Configuration.Defaults.StartsAtHome;
            this.VisualSchedule. EndsAtHome = Program.Configuration.Defaults.EndsAtHome;

            this.Days.Clear();
            this.Days.Add(new VisualDay(){ Start = Program.Configuration.PlannerPreferences.JourneyStart});
            this.Days.First().DEBUG_SetVisual( false);
            this.anchorPlaceholder = (0,0);
            this.VisualSchedule.IsModified = false;
            this.summary.Reset();
        }
        
        public void Clear()
        {
            this.MapManager.BeforeMarkersRemoved();

            DataInitialization();

            StateHasChanged();
        }

        public List<ScheduleDay> GetSchedule(bool onlyPinned)
        {
            return this.Days.Select(d => new ScheduleDay()
            {
                Start = d.Start,
                Anchors = d.Anchors.Where(it => !onlyPinned || it.IsPinned).Select(a => a.ToScheduleAnchor()).ToList()
            }).ToList();
        }

        public void SetSchedule(IReadOnlySchedule source)
        {
            this.MapManager.BeforeMarkersRemoved();

            this.Days.Clear();
            this.anchorPlaceholder = null;

            this.VisualSchedule. StartsAtHome = source.StartsAtHome;
            this.VisualSchedule. EndsAtHome = source.EndsAtHome;
            this.isLooped = source.IsLooped;
            
            // we have to create a empty skeleton first so creating marker will know if the marker is important or not
            this.Days.AddRange(source.Days.Select(d => new VisualDay(d.Start, d.Anchors.Select(_ => default(VisualAnchor)!).ToList() )));
            this.Days.Last().DEBUG_SetVisual( false);
            
            int day_idx = -1;
            foreach (var day_src in source.Days)
            {
                ++day_idx;
                var day = this.Days[day_idx];
                int anchor_idx = -1;
                foreach (ScheduleAnchor anchor_src in day_src.Anchors)
                {
                    if (!string.IsNullOrEmpty(anchor_src.Label))
                    {
                        Console.WriteLine($"DEBUG Loaded non-empty label {anchor_src.Label}");
                    }
                    ++anchor_idx;
                    var marker = attachMarker(geoPointToLatLng( anchor_src.UserPoint), day_idx, anchor_idx,anchor_src.IsPinned);
                    day.Anchors[anchor_idx] = anchor_src.ToVisualAnchor(marker);
                }
            }

            this.VisualSchedule.IsModified = false;
            
            Console.WriteLine($"Recreated days {this.Days.Count} with {this.Days.SelectMany(it => it.Anchors).Count()} anchors");
            // daily statistics will be called from parent component
        }

        private static LatLng geoPointToLatLng(in GeoPoint pt)
        {
            return new LatLng((float) pt.Latitude.Degrees, (float) pt.Longitude.Degrees);
        }

      
        private void splitDay(int dayIndex, int anchorIndex)
        {
            this.VisualSchedule.IsModified = true;
            
            var (curr_day,new_day) = this.SplitDay(dayIndex,anchorIndex);

            curr_day.DEBUG_SetVisual( true);
            new_day.DEBUG_SetVisual( false);
            
            this.anchorPlaceholder = null;

            recreateMarkers(dayIndex , curr_day.Anchors.Count-1); // start from the end of the day

            ResetSummary();
        }

        private void mergeDayToPrevious(MouseEventArgs args, int dayIndex)
        {
            var prev_day = this.Days[dayIndex - 1];
            var prev_count = prev_day.Anchors.Count;
            var cur_day = this.Days[dayIndex];
            prev_day.Anchors.AddRange(cur_day.Anchors);
            this.Days.RemoveAt(dayIndex);

            this.anchorPlaceholder = null;

            recreateMarkers(dayIndex - 1, prev_count-1); // start from the last anchor that day

            ResetSummary();
        }

        private async ValueTask rebuildNeededAsync( [CallerMemberName] string memberName = "")
        {
            Console.WriteLine($"redraw trigger from {memberName}");
            this.summary.Reset();
            await this.MapManager.RebuildNeededAsync();
        }

        private void insertMarkerPlaceholder(MouseEventArgs args,int dayIndex, int anchorIndex)
        {
            this.anchorPlaceholder = (dayIndex, anchorIndex);
            Console.WriteLine($"DEBUG setting insert at {this.anchorPlaceholder}");

            StateHasChanged();
        }

        private void detachMarker(Marker marker)
        {
            marker.OnMove -= onMarkerDrag;
            marker.OnMoveEnd -= onMarkerDragEndAsync;
            marker.OnClick -= onMarkerClickAsync;

            this.MapManager.MarkerRemoved(marker);
        }

        private (int dayIndex,int anchorIndex) indexOfMarker(Marker marker)
        {
            for (int day_idx=0;day_idx<this.Days.Count;++day_idx)
            {
                for (int anchor_idx=0;anchor_idx<this.Days[day_idx].Anchors.Count;++anchor_idx)
                {
                    if (this.Days[day_idx].Anchors[anchor_idx].Visual == marker)
                        return (day_idx,anchor_idx);
                }
            }

            throw new ArgumentException("Marker not found.");
        }

        private void onMarkerDrag(Marker marker, DragEvent evt)
        {
            this.onDragMarkerLatLng = evt.LatLng;
            
            StateHasChanged();
        }



        private static string iconClassName(bool isImportant, bool isPinned)
        {
            if (isPinned)
                return isImportant ? "orange-number-icon" : "navy-number-icon";
            else
                return "auto-anchor-icon";
        }

        private Marker attachMarker(LatLng coords, int dayIndex,int anchorIndex,bool isPinned)
        {
            string title = getMarkerTitle(dayIndex, anchorIndex);
            bool is_important = isAnchorImportant(dayIndex, anchorIndex);
            // https://dotscrapbook.wordpress.com/2014/11/28/simple-numbered-markers-with-leaflet-js/
            DivIcon icon;
            if (isPinned)
            icon= new DivIcon()
            {
                Size = new Size(36, 46),
                Anchor = new Point(18, 43),
                PopupAnchor= new Point(3, -40),
            };
            else
              /*  icon= new DivIcon()
                {
                    Size = new Size(36, 46),
                    Anchor = new Point(18, 43),
                    PopupAnchor= new Point(3, -40),
                };*/
            icon= new DivIcon()
            {
                Size = new Size(48, 48),
                Anchor = new Point(15, 38),
                PopupAnchor= new Point(3, -40),
            };

            icon.ClassName = iconClassName(is_important, isPinned);
            icon.Html = $"<div class='{(isPinned?"pinned":"auto")}-anchor-icon-text'>{title}</div>";
            
            var marker = new Marker(coords)
            {
                Draggable = true,
                Title = title,
                //Popup = new Popup {Content = $"I am at {coords.Lat:0.00}° lat, {coords.Lng:0.00}° lng"},
                //Tooltip = new Tooltip {Content = "Click and drag to move me"}
                Icon = icon,
            };
            
            // Console.WriteLine($"Attaching marker with title {marker.Title} and icon {icon.Html}");

            marker.OnMove += onMarkerDrag;
            marker.OnMoveEnd += onMarkerDragEndAsync;
            marker.OnClick += onMarkerClickAsync;

            this.MapManager.MarkerAdded(marker);
            
            return marker;
        }

        private async void onMarkerClickAsync(InteractiveLayer sender, MouseEvent e)
        {
            var marker = (sender as Marker)!;
            var coords = await askForPositionAsync(marker.Position);
            if (coords != null)
            {
                await setMarkerPositionAsync(marker, coords);
                await marker.SetLatLngAsync(MapManager.Map, coords);
            }
        }

        private VisualAnchor<Marker> insertAnchor(LatLng coords,int dayIndex,int anchorIndex,bool isPinned)
        {
            var marker = attachMarker(coords, dayIndex, anchorIndex,isPinned);

            var anchor = new VisualAnchor<Marker>(marker){IsPinned = isPinned};

            anchor.Break = Program.Configuration.PlannerPreferences.DefaultAnchorBreak;

            var day = this.Days[dayIndex];
            if (anchorIndex < 0 || anchorIndex > day.Anchors.Count)
                throw new ArgumentOutOfRangeException($"{nameof(anchorIndex)}={anchorIndex} out of range {day.Anchors.Count}.");
            day.Anchors.Insert(anchorIndex, anchor);

            return anchor;
        }
        
        /*private bool isLast(int index)
        {
            for (int i=index+1;i<this.days.Count;++i)
                if (this.days[i] != null)
                    return false;

            return true;
        }
*/
    

       private static string getMarkerTitle(int dayIndex, int anchorIndex)
       {
           return $"{dayIndex + 1}-{anchorIndex + 1+(dayIndex==0?0:1)}";
       }

       private async void addMarkerByPositionAsync()
       {
           var coords = await askForPositionAsync(position: null);
           if (coords != null)
               await AddMarkerAsync(coords);
       }

       private async ValueTask<LatLng?> askForPositionAsync(LatLng? position)
       {
           string? initial = null;
           if (position != null)
               initial = $"{position.Lat}, {position.Lng}";
           string? input = await this.commonDialog.PromptAsync("Anchor position",initial);
           if (string.IsNullOrEmpty(input))
               return null;

           var parts = input.Split(",").Select(it => it.Trim()).ToList();
           if (parts.Count != 2)
           {
               Console.WriteLine("Expected 'lat,long' input.");
               return null;
           }

           if (!float.TryParse(parts[0], out var lat))
           {
               Console.WriteLine($"Unable to parse latitude: {parts[0]}");
               return null;
           }

           if (!float.TryParse(parts[1], out var lon))
           {
               Console.WriteLine($"Unable to parse longitude: {parts[1]}");
               return null;
           }

           return new LatLng(lat:lat,lng:lon);
       }
       
       private void deletePlaceholder()
       {
           this.anchorPlaceholder = null;
       }

       private void recreateMarkers(int dayIndex, int anchorIndex)
       {
           for (int d_idx = dayIndex; d_idx < Days.Count; ++d_idx)
           {
               var day = this.Days[d_idx];

               for (int a_idx = (d_idx == dayIndex ? anchorIndex : 0); a_idx < day.Anchors.Count; ++a_idx)
               {
                   var anchor = day.Anchors[a_idx];

                   string title = getMarkerTitle(d_idx, a_idx);

                   if (title == anchor.Visual.Title && iconClassName(isAnchorImportant(d_idx, a_idx),anchor.IsPinned) == anchor.Visual.Icon.ClassName)
                   {
                       continue;
                   }

                   detachMarker(anchor.Visual);
                   anchor.Visual = attachMarker(anchor.Visual.Position, d_idx, a_idx,anchor.IsPinned);
               }
           }
       }

       private bool isAnchorImportant(int dayIndex,int anchorIndex)
       {
           bool is_important = ((dayIndex == 0 && anchorIndex == 0) // starting point 
                                ||
                                // or the last of the day except for the last day (unless it is not looped route)
                                (anchorIndex == Days[dayIndex].Anchors.Count - 1 && (!this.IsLoopActivated() || dayIndex<this.Days.Count-1)));
           
           return is_important;
       }


       public void ResetSummary()
       {
           this.summary.Reset();
           
           StateHasChanged();
       }

      /* private int? getPreceedingLegIndex(int dayIndex, int anchorIndex)
       {
           int leg_idx = 0;
           for (int d = 0; d < dayIndex; ++d)
           {
               leg_idx += this.GetLegCount(d);
           }
           
           leg_idx += anchorIndex - 1;

           return leg_idx;
       }*/


      private (int dayIndex,int anchorIndex,bool reused) checkpointIndexToAnchor(SummaryCheckpoint checkpoint, int dayIndex, int checkpointIndex)
      {
          if (checkpoint.IsLooped)
              return (0,0,true);

          if (dayIndex == 0)
              return (dayIndex, checkpointIndex, false);

          if (checkpointIndex == 0) // first checkpoint of "next" day
              return (dayIndex-1,this.Days[dayIndex-1].Anchors.Count-1,true);

          return (dayIndex,checkpointIndex - 1,false);
      }
       
  
       public void RebuildAutoAnchors()
       {
           Console.WriteLine($"DEBUG before RebuildAutoAnchors {this.DEBUG_PinsToString()}");
           
           foreach (var day in this.Days)
           {
               for (int anchor_idx=day.Anchors.Count-1;anchor_idx>=0;--anchor_idx)
                   if (!day.Anchors[anchor_idx].IsPinned)
                       removeAnchor(day,anchor_idx);
           }

           int leg_idx = 0; 
           for (int day_idx = 0; day_idx < this.Days.Count; ++day_idx)
           {
               int anchor_starting_leg_idx = day_idx==0?0:-1; // only first day has its own starting anchor

               // we need to iterate over fixed+1 legs because we don't know in advance how many auto legs
               // were added after last (valid for current day) fixed leg. So we will iterate into the first
               // leg of the next day
               int fixed_leg_count = this.GetLegCount(day_idx);
               Console.WriteLine($"DEBUG RebuildAutoAnchors Day {day_idx} with {fixed_leg_count} fixed legs, starting at {leg_idx}/{plan.Legs.Count} leg");
               int DEBUG_iter =0;
               for (;fixed_leg_count>=0 && leg_idx<plan.Legs.Count ;++anchor_starting_leg_idx,++DEBUG_iter)
               {
                   if (this.plan.Legs[leg_idx].AutoAnchored)
                   {
                       Console.WriteLine($"DEBUG Inserting auto anchors at {day_idx}/{anchor_starting_leg_idx}");
                       insertAnchor(geoPointToLatLng(plan.Legs[leg_idx].Fragments.First().Places.First().Point.Convert()),
                           day_idx, anchor_starting_leg_idx,  isPinned: false);
                   }
                   else
                   {
                       --fixed_leg_count;
                   }

                   ++leg_idx;
               }
Console.WriteLine($"DEBUG loop ended after {DEBUG_iter} iterations with leg_idx {leg_idx} and fixed len {fixed_leg_count}");
               --leg_idx; // on every day we go one (fixed) leg too far, so we need to go back one leg
           }
           
           Console.WriteLine($"DEBUG after RebuildAutoAnchors {this.DEBUG_PinsToString()}");

           recreateMarkers(0,0); 
       }
       
       private async Task deleteMarkerAsync( int dayIndex, int anchorIndex)
       {
           this.VisualSchedule.IsModified = true;
           
           int DEBUG_leg_count = this.plan.Legs.Count;
           
           var day = this.Days[dayIndex];
           var anchor = day.Anchors[anchorIndex];

           if (resetAnchorSurroundings(dayIndex, ref anchorIndex, out int incoming_leg_idx))
           {
               if (incoming_leg_idx < this.plan.Legs.Count - 1)
                   MapManager.RemoveLeg(incoming_leg_idx + 1);
           }

           removeAnchor(dayIndex,anchorIndex);
           
           if (day.Anchors.Count == 0 && this.Days.Count > 1)
           {
               this.Days.RemoveAt(dayIndex);
               this.anchorPlaceholder = null;

               anchorIndex = 0; // for refresh
           }
           else
           {
               this.anchorPlaceholder = (dayIndex, anchorIndex);
               if (anchorIndex == day.Anchors.Count)
                   --anchorIndex;
           }

           recreateMarkers(dayIndex, anchorIndex);

   

          await rebuildNeededAsync();

           StateHasChanged();
           
           Console.WriteLine($"After deleting marker we have {this.plan.Legs.Count}, previously {DEBUG_leg_count}");
       }

       private async void onMarkerDragEndAsync(Marker marker, Event e)
       {
           await setMarkerPositionAsync(marker,this.onDragMarkerLatLng);
       }

       private async ValueTask setMarkerPositionAsync(Marker marker,LatLng position)
       {
           this.VisualSchedule.IsModified = true;

           marker.Position = position;
           (int day_idx, int anchor_idx) = indexOfMarker(marker);

           resetAnchorSurroundings(day_idx, ref anchor_idx, out _);

           await rebuildNeededAsync();
       }

       public bool HasEnoughPointsForBuild()
       {
           return this.Days.SelectMany(it => it.Anchors).Where(it => it.IsPinned).HasMany();
       }


       public async Task<bool> AddMarkerAsync(LatLng coords)
       {
           if (this.anchorPlaceholder == null)
               return false;

           this.VisualSchedule.IsModified = true;
           
           var (day_idx, anchor_idx) = this.anchorPlaceholder.Value;

          var anchor = insertAnchor(coords, day_idx, anchor_idx, isPinned: true);
          
           if (HasEnoughPointsForBuild())
           {
               if (this.plan.IsEmpty) // when starting with plan go easy and just remember to add loop leg
               {
                   this.plan.Legs.Insert(0, LegPlan.Missing);
                   if (this.IsLooped)
                       this.plan.Legs.Insert(0, LegPlan.Missing);
               }
               else
               {
                   int leg_idx = this.GetIncomingLegIndex(day_idx, anchor_idx) ?? 0;
                   this.plan.Legs.Insert(leg_idx, LegPlan.Missing);
               }

               Console.WriteLine($"DEBUG AddMarkerAsync legs count {this.plan.Legs.Count}");
           }
           resetAnchorSurroundings(day_idx, ref anchor_idx, out _);

           // start from previous one this day, because we could add at the end of the day, meaning we need to change the icon
           // of the previous marker from "final" to "regular"
           recreateMarkers(day_idx, Math.Max(0, anchor_idx - 1)); 

           // at this point marker reference is invalidated because of the refresh

           this.anchorPlaceholder = (day_idx, anchor_idx + 1);

          await rebuildNeededAsync();

           StateHasChanged();

           return true;
       }


       private bool resetAnchorSurroundings(int dayIndex, ref int anchorIndex, out int incomingLegIndex)
       {
           if (this.plan.IsEmpty)
           {
               incomingLegIndex = -1;
               return false;
           }

           const int incoming_pre_leg = -1;

           incomingLegIndex = this.GetIncomingLegIndex(dayIndex, anchorIndex) ?? incoming_pre_leg;
           Console.WriteLine($"DEBUG Reset adjacent legs leg:{incomingLegIndex}/{this.plan.Legs.Count} anchor:{anchorIndex}");
           if (incomingLegIndex >= 0) // incoming
               this.plan.Legs[incomingLegIndex] = LegPlan.Missing;
           if (incomingLegIndex < this.plan.Legs.Count - 1) // outgoing
               this.plan.Legs[incomingLegIndex + 1] = LegPlan.Missing;

           if (dayIndex == 0 && anchorIndex == 0 && this.IsLooped) // looped
               this.plan.Legs[^1] = LegPlan.Missing;

           {
               var anchor_idx_next = anchorIndex + 1;
               var day_idx_next = dayIndex;
               // if the current anchor is last of the day we have to remove adjacent auto-anchors
               // from the next day
               if (anchor_idx_next == this.Days[day_idx_next].Anchors.Count && day_idx_next < this.Days.Count - 1)
               {
                   ++day_idx_next;
                   anchor_idx_next = 0;
               }

               var anchors = this.Days[day_idx_next].Anchors;
               Console.WriteLine($"DEBUG Reset auto anchors {anchors.Count} {String.Join(",", anchors.Select(it => it.IsPinned))}");
               while (anchor_idx_next < anchors.Count && !anchors[anchor_idx_next].IsPinned) // removing next auto-anchors
               {
                   Console.WriteLine($"DEBUG Removing next auto anchor at {day_idx_next}:{anchor_idx_next} with leg {incomingLegIndex + 2}");
                   removeAnchor(day_idx_next, anchor_idx_next);
                   MapManager.RemoveLeg(incomingLegIndex + 2);

               }
           }
           {
               var is_loop_start = dayIndex == 0 && anchorIndex == 0 && this.IsLooped;

               var anchors = this.Days[dayIndex].Anchors;
               if (is_loop_start)
               {
                   dayIndex = this.Days.Count - 1;
                   anchors = this.Days[dayIndex].Anchors;

                   anchorIndex = anchors.Count; // set anchor as next to last
                   incomingLegIndex = this.plan.Legs.Count;
                   Console.WriteLine($"DEBUG switch to the end of anchors {dayIndex}:{anchorIndex}");
               }

               while (anchorIndex > 0 && !anchors[anchorIndex - 1].IsPinned) // removing previous auto-anchors
               {
                   Console.WriteLine($"DEBUG Removing prev auto anchor at {anchorIndex - 1} with leg {incomingLegIndex - 1}");
                   removeAnchor(dayIndex, anchorIndex - 1);
                   this.MapManager.RemoveLeg(incomingLegIndex - 1);
                   --incomingLegIndex;
                   --anchorIndex;
               }

               if (is_loop_start)
               {
                   anchorIndex = 0;
                   incomingLegIndex = incoming_pre_leg;
               }
           }

           Console.WriteLine($"DEBUG resetAnchorSurroundings finished with {this.DEBUG_PinsToString()}");

               return true;
       }

       private void removeAnchor(int dayIndex, int anchorIndex)
       {
           removeAnchor(this.Days[dayIndex],anchorIndex);
       }
       private void removeAnchor(VisualDay day, int anchorIndex)
       {
           var anchors = day.Anchors;
           detachMarker(anchors[anchorIndex].Visual);
           anchors.RemoveAt(anchorIndex);
       }

       private void pinMarker(int dayIndex, int anchorIndex)
       {
           var anchor = this.Days[dayIndex].Anchors[anchorIndex];
           anchor.IsPinned = true;
           
           recreateMarkers(dayIndex,anchorIndex);
            
           ResetSummary();
       }

    }
    
}