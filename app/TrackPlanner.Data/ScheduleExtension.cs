using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Data.Stored;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public static class ScheduleLikeExtension
    {
        public static IEnumerable<(string label, string classIcon, int count, TimeSpan duration)> GetEventStats(int[] eventCounters,UserPlannerPreferences preferences)
        {
            return
                Enumerable.Range(0, eventCounters.Length)
                    .GroupBy(it => preferences.TripEvents[it].Label)
                    .Select(group_it =>
                    {
                        var first_event = preferences.TripEvents[group_it.First()];
                        var total_count = group_it.Select(it => eventCounters[it]).Sum();
                        var total_duration = group_it.Select(it => preferences.TripEvents[it].Duration * eventCounters[it]).Sum();

                        return (first_event.Label, first_event.ClassIcon, total_count, total_duration);
                    })
                    .Where(it => it.total_count!=0);
        }

        public static (TDay current,TDay added) SplitDay<TDay,TAnchor>(this ISchedule<TDay,TAnchor> schedule,int dayIndex, int anchorIndex)
        where TDay : IDay<TAnchor>,new()
        where TAnchor : IAnchor
        {
            var curr_day = schedule.Days[dayIndex];
            var new_day = new TDay() { Start = dayIndex==0? schedule.PlannerPreferences.NextDayStart: curr_day.Start};
            var split_idx = anchorIndex + 1;
            new_day.Anchors.AddRange(curr_day.Anchors.Skip(split_idx));
            curr_day.Anchors.RemoveRange(split_idx, curr_day.Anchors.Count - split_idx);
            
            curr_day.Anchors.Last().IsPinned = true;
            
            schedule.Days.Insert(dayIndex + 1, new_day);

            return (curr_day, new_day);
        }
        
        public static bool IsLoopActivated(this IReadOnlySchedule _this)
        {
            return _this.IsLooped && _this.Days.SelectMany(it => it.Anchors).HasMany();
        }

        public static bool IsLoopedDay(this IReadOnlySchedule readOnlySchedule, int dayIndex)
        {
            return readOnlySchedule.IsLoopActivated() && dayIndex == readOnlySchedule.Days.Count - 1;
        }

        public static int GetLegCount(this IReadOnlySchedule readOnlySchedule, int dayIndex)
        {
            return GetLegCount(dayIndex, readOnlySchedule.Days[dayIndex].Anchors.Count, addLoopedAnchor: readOnlySchedule.IsLoopedDay(dayIndex));
        }

        public static int GetLegCount(int dayIndex,int anchorsCount,bool addLoopedAnchor)
        {
            int legs_count = anchorsCount;
            if (dayIndex == 0) // first day contains both start and end (for that day)
                --legs_count;

            // DO NOT add "else" here, we could have single day, looped, in such case count(legs)=count(anchors)
            if (addLoopedAnchor)
                ++legs_count;

            return legs_count;
        }
        
        public static IEnumerable<LegPlan> GetDayLegs(this IReadOnlySchedule readOnlySchedule, int dayIndex)
        {
            int leg_offset = GetIncomingLegIndex(readOnlySchedule, dayIndex, 0) ?? 0;

            var leg_count = readOnlySchedule.GetLegCount(dayIndex);

            var legs = readOnlySchedule.TrackPlan.Legs.Skip(leg_offset).Take(leg_count);

            return legs;
        }

        public static SummaryJourney GetSummary(this IReadOnlySchedule schedule)
        {
            var summary = new SummaryJourney() { PlannerPreferences = schedule.PlannerPreferences };
            for (int day_idx = 0; day_idx < schedule.Days.Count; ++day_idx)
            {
                var summary_day = createSummaryDay(schedule, day_idx);

                summary.Days.Add(summary_day);
            }

            return summary;
        }

        private static SummaryDay createSummaryDay(this IReadOnlySchedule schedule, int dayIndex)
        {
            var day = schedule.Days[dayIndex];

            var start = day.Start;
            TimeSpan rolling_time = TimeSpan.Zero;

            var summary_day = new SummaryDay() {Start = start};
            // if we don't have anything (we just started from scratch) do not create any checkpoints as well
            // just an empty day, that's all,
            // NOTE: for last day, looped, not having any anchors is a valid scenario (it starts from the beginning
            // of the previous day and loop to the global start) 
            if (dayIndex == 0 && day.Anchors.Count == 0)
                return summary_day;


            summary_day.Checkpoints.Add(new SummaryCheckpoint(eventsCount: schedule.PlannerPreferences.TripEvents.Length)
            {
                Arrival = start,
                Departure = start,
            });

            int anchor_idx = dayIndex == 0 ? 1 : 0;
            for (; anchor_idx < day.Anchors.Count; ++anchor_idx)
            {
                summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day,
                    ref rolling_time, dayIndex, anchor_idx));
            }

            if (schedule.IsLoopedDay(dayIndex))
            {
                summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day,
                    ref rolling_time, dayIndex, anchor_idx));
            }

            var last_events = schedule.PlannerPreferences.TripEvents
                .Select(it => it.Category)
                .Distinct()
                .ToDictionary(it => it, _ => start);

            for (int i = 0; i < summary_day.Checkpoints.Count; ++i)
                addDayTripEvents(schedule, summary_day, i, last_events, dayIndex);


            Console.WriteLine("DEBUG fixing last checkpoint for the day");

            var last_checkpoint = summary_day.Checkpoints.Last();

            Console.WriteLine("DEBUG adding extra snack time");

            // let's add/replace the events set for the last moment of the day
            {
                var is_home_start = dayStartsAtHome(schedule, dayIndex);
                var is_home_end = dayEndsAtHome(schedule, dayIndex);

                for (int event_idx = 0; event_idx < schedule.PlannerPreferences.TripEvents.Length;++event_idx)
                {
                    var user_event = schedule.PlannerPreferences.TripEvents[event_idx];

                    if (!isValidEventDay(user_event, dayIndex, is_home_start, is_home_end)
                        // it is already added
                        || summary_day.Checkpoints.Any(it => it.EventCounters[event_idx] > 0))
                        continue;

                    if (last_events[user_event.Category] == start) // we need to add extra event
                    {
                        addTripEvent(schedule, summary_day, summary_day.Checkpoints.Count-1, event_idx);
                    }
                    else // we need to replace event
                    {
                        bool replaced = false;
                        
                        // here we have to reverse the order to go from lower priority to highest
                        for (int sub_event_idx=  schedule.PlannerPreferences.TripEvents.Length-1;sub_event_idx>event_idx;++sub_event_idx)
                        {
                            var sub_event = schedule.PlannerPreferences.TripEvents[sub_event_idx];
                            if (sub_event.Category!=user_event.Category) 
                                continue;
                            var sub_point_idx = summary_day.Checkpoints.FindLastIndex(it => it.EventCounters[sub_event_idx] != 0);
                            if (sub_point_idx==-1)
                                continue;

                            removeTripEvent(schedule, summary_day, sub_point_idx, sub_event_idx);
                            addTripEvent(schedule, summary_day, sub_point_idx, event_idx);
                            replaced = true;
                            break;
                        }

                        if (!replaced)
                        {
                            addTripEvent(schedule, summary_day, summary_day.Checkpoints.Count-1, event_idx);
                            summary_day.Problem = $"Couldn't correctly find a replacement for event [{event_idx}]{user_event.Label}";
                        }
                    }
                    
                }
            }

            // moving all events from the last checkpoint to the previous one
            // rationale: when reading summary it is surprise effect that last checkpoint (most likely camping)
            // has snack time included, while it is not possible
            if (summary_day.Checkpoints.Count>1)
            {
                for (int event_idx = 0; event_idx < last_checkpoint.EventCounters.Length; ++event_idx)
                    while (last_checkpoint.EventCounters[event_idx] > 0)
                    {
                        removeTripEvent(schedule, summary_day, summary_day.Checkpoints.Count-1, event_idx);
                        addTripEvent(schedule, summary_day, summary_day.Checkpoints.Count-2, event_idx);
                    }
            }


            Console.WriteLine("DEBUG clearing last checkpoint for the day");

            last_checkpoint.Break = TimeSpan.Zero;
            // since we don't count in break for the last checkpoint we have to shift
            // arrival time to departure
            last_checkpoint.Arrival = last_checkpoint.Departure;

            // in similar fashion we "correct" the first day, it is not a break time, it is postponed start (but not for day)
            summary_day.Checkpoints[0].Break = TimeSpan.Zero;
            summary_day.Checkpoints[0].Arrival = summary_day.Checkpoints[0].Departure;

            summary_day.Distance = summary_day.Checkpoints.Select(it => it.IncomingDistance).Sum();

            TimeSpan late_camping = last_checkpoint.Arrival - ((!schedule.EndsAtHome || dayIndex < schedule.Days.Count - 1)
                ? schedule.PlannerPreferences.CampLandingTime
                : schedule.PlannerPreferences.HomeLandingTime);
            if (late_camping > TimeSpan.Zero)
            {
                summary_day.LateCampingBy = late_camping;
            }


            return summary_day;
        }

        public static int? GetIncomingLegIndex(this IReadOnlySchedule readOnlySchedule, int dayIndex, int anchorIndex)
        {
            if (dayIndex == 0 && anchorIndex == 0)
                return null;

            int count = 0;
            for (int d = 0; d < dayIndex; ++d)
                count += GetLegCount(readOnlySchedule, d);
            // first day includes starting anchor, for every next day the starting anchor is the last anchor from the previous day
            return count + anchorIndex - (dayIndex == 0 ? 1 : 0);
        }

        public static string DEBUG_PinsToString(this IReadOnlySchedule readOnlySchedule)
        {
            return $"DAYS {readOnlySchedule.Days.Count} " + String.Join(Environment.NewLine, readOnlySchedule.Days.ZipIndex().Select(it => $"d{it.index}: {(String.Join(", ", it.item.Anchors.Select(a => a.IsPinned ? "P" : "a")))}"))
                                                  + Environment.NewLine
                                                  + $"LEGS {readOnlySchedule.TrackPlan.Legs.Count} " + String.Join(" ",
                                                      readOnlySchedule.TrackPlan.Legs.Select(it => (it.AutoAnchored ? "A" : "P")+(it.IsDrafted?"d":"c")));
            /*+"LEGS "+String.Join(Environment.NewLine,
                schedule.TrackPlan.Legs.Select(it => it.AutoAnchored?"a":"P")
                    .Partition(Enumerable.Range(0,schedule.Days.Count).Select(schedule.GetLegCount))
                        .ZipIndex()
                        .Select(it => $"d{it.index}: {(String.Join(" ",  it.item))}"));*/
        }

        public static (int dayIndex, int anchorIndex, int legStartingDayIndex) LegIndexToStartingDayAnchor(this IReadOnlySchedule readOnlySchedule, int legIndex)
        {
            int local_leg_idx = legIndex;
            for (int day_idx = 0; day_idx < readOnlySchedule.Days.Count; ++day_idx)
            {
                int leg_count = readOnlySchedule.GetLegCount(day_idx);
                if (local_leg_idx < leg_count)
                {
                    var starting_day_idx = legIndex - local_leg_idx;

                    if (day_idx == 0)
                    {
                        return (day_idx, local_leg_idx, starting_day_idx);
                    }
                    else
                    {
                        // for opening legs on next days, the starting anchor is last one from previous day
                        // we indicate it by returning effectively -1 for such case
                        return (day_idx, local_leg_idx - 1, starting_day_idx);
                    }
                }

                local_leg_idx -= leg_count;
            }

            throw new InvalidOperationException($"Couldn't compute day for leg {local_leg_idx}.");
        }

        public static int GetDayByLeg(this IReadOnlySchedule readOnlySchedule, SummaryJourney summary, int legIndex, out SummaryDay day, out TimeSpan coveredDayBreaks)
        {
            if (legIndex >= readOnlySchedule.TrackPlan.Legs.Count)
                throw new ArgumentOutOfRangeException($"Leg index {legIndex} is greater than entire route {readOnlySchedule.TrackPlan.Legs.Count}.");

            // returns index of leg starting given day

            int leg_starting_day_idx = 0;
            for (int day_idx = 0; day_idx < readOnlySchedule.Days.Count; ++day_idx)
            {
                int legs_count = readOnlySchedule.GetLegCount(day_idx);
                Console.WriteLine($"GetDayByLeg day {day_idx} legs {legs_count}");

                if (legIndex < leg_starting_day_idx + legs_count)
                {
                    day = summary.Days[day_idx];

                    var leg_idx_within_day = legIndex - leg_starting_day_idx;
                    coveredDayBreaks = day.Checkpoints
                        .Take(leg_idx_within_day + 1)
                        .Select(it => it.Break)
                        .Sum();
                    return leg_starting_day_idx;
                }

                leg_starting_day_idx += legs_count;
            }

            throw new InvalidOperationException($"Couldn't compute day for leg {legIndex}.");
        }

        private static SummaryCheckpoint createSummaryPoint(this IReadOnlySchedule schedule, SummaryDay summaryDay,
            ref TimeSpan rollingTime, int dayIndex, int anchorIndex)
        {
            var day = schedule.Days[dayIndex];
            GetAnchorDetails(schedule, dayIndex, anchorIndex, out var is_looped_anchor, out var is_last_of_day);
            // we store label for the last checkpoint in the first anchor (of entire schedule)
            var anchor = is_looped_anchor ? schedule.Days[0].Anchors[0] : day.Anchors[anchorIndex];

            TimeSpan start = summaryDay.Checkpoints[^1].Departure;

            int leg_idx = GetIncomingLegIndex(schedule, dayIndex, anchorIndex)!.Value;
            LegPlan leg;
            {
                var DEBUG_context = $"{dayIndex}:{anchorIndex} {schedule.DEBUG_PinsToString()}";
                leg = schedule.TrackPlan.GetLeg(leg_idx, DEBUG_context);
            }
            var true_time = DataHelper.CalcTrueTime(rollingTime, leg.UnsimplifiedDistance, leg.RawTime, schedule.RouterPreferences.GetLowRidingSpeedLimit(),
                schedule.PlannerPreferences.HourlyStamina);
            rollingTime += true_time;
            start += true_time;


            var break_time = is_last_of_day ? TimeSpan.Zero : anchor.Break;

            return new SummaryCheckpoint(eventsCount: schedule.PlannerPreferences.TripEvents.Length)
            {
                Arrival = start,
                IncomingDistance = leg.UnsimplifiedDistance,
                RollingTime = rollingTime,
                IncomingLegIndex = leg_idx,
                IsLooped = is_looped_anchor,
                Break = break_time,
                Departure = start + break_time,
                Label = anchor.Label
            };
        }

        private static void addDayTripEvents(IReadOnlySchedule schedule, SummaryDay summaryDay, int summaryPointIndex, 
            Dictionary<string, TimeSpan> lastEvents, int dayIndex)
        {
            var is_last_point = summaryPointIndex == summaryDay.Checkpoints.Count - 1;
            var current_summary_point = summaryDay.Checkpoints[summaryPointIndex];

            var is_home_start = dayStartsAtHome(schedule, dayIndex);
            var is_home_end = dayEndsAtHome(schedule, dayIndex);
            
            for (int event_idx = 0; event_idx < schedule.PlannerPreferences.TripEvents.Length;)
            {
                var user_event = schedule.PlannerPreferences.TripEvents[event_idx];

                if (isValidEventDay(user_event,dayIndex,is_home_start,is_home_end))
                {
                    bool add_event;
                    if (user_event.Interval != null)
                    {
                        add_event = current_summary_point.Departure > lastEvents[user_event.Category] + user_event.Interval
                                    // it is better to play safe for the last point
                                    || (is_last_point
                                        && current_summary_point.Departure > lastEvents[user_event.Category] + user_event.Interval / 2);
                    }
                    else
                    {
                        // we treat time-clocks set to null as non-starters of the day
                        add_event = (summaryPointIndex!=0 || user_event.ClockTime!=null) 
                            && current_summary_point.Departure > (user_event.ClockTime ?? TimeSpan.Zero)
                                    && summaryDay.Checkpoints.All(it => it.EventCounters[event_idx] == 0);
                    }

                    if (add_event)
                    {
                        var duration = addTripEvent(schedule, summaryDay, summaryPointIndex, event_idx);
                        if (user_event.Interval != null)
                            lastEvents[user_event.Category] += user_event.Interval.Value + duration;
                        else
                            lastEvents[user_event.Category] = (user_event.ClockTime ?? TimeSpan.Zero).Max(current_summary_point.Arrival) + duration;

                        event_idx = 0; // we changed departure time, let's check all events again
                        continue;
                    }
                }

                ++event_idx;
            }

        }

        private static bool isValidEventDay(TripEvent tripEvent, int dayIndex, bool isHomeStart, bool isHomeEnd)
        {
            return (!isHomeStart || !tripEvent.SkipAfterHome)
                   && (!isHomeEnd || !tripEvent.SkipBeforeHome)
                   && dayIndex % tripEvent.EveryDay == 0;
        }

        private static bool dayStartsAtHome(IReadOnlySchedule schedule, int dayIndex)
        {
            return (schedule.StartsAtHome && dayIndex == 0);
        }

        public static void GetAnchorDetails(this IReadOnlySchedule schedule, int dayIndex, int anchorIndex,out bool isLoopedAnchor,
            out bool isLastOfDay)
        {
            var day = schedule.Days[dayIndex];
            isLoopedAnchor = dayIndex==schedule.Days.Count-1 && anchorIndex == day.Anchors.Count;
            // anchor[0] on every "next" day is not really first anchor, those starting anchor have their own render method
            isLastOfDay = isLoopedAnchor
                                  || (anchorIndex == day.Anchors.Count - 1 && (dayIndex < schedule.Days.Count - 1 || !schedule.IsLoopActivated()));
        }

        private static bool dayEndsAtHome(IReadOnlySchedule readOnlySchedule, int dayIndex)
        {
            return readOnlySchedule.EndsAtHome && dayIndex == readOnlySchedule.Days.Count - 1;
        }

        private static TimeSpan addTripEvent(IReadOnlySchedule schedule, SummaryDay summaryDay, 
            int summaryPointIndex, int userEventIndex)
        {
            var duration = schedule.PlannerPreferences.TripEvents[userEventIndex].Duration;
            
            SummaryCheckpoint summary_point = summaryDay.Checkpoints[summaryPointIndex];
            ++summary_point.EventCounters[userEventIndex];
          
            summary_point.Departure += duration;
            summary_point.Break += duration;

            for (int i = summaryPointIndex + 1; i < summaryDay.Checkpoints.Count; ++i)
            {
                summaryDay.Checkpoints[i].Arrival += duration;
                summaryDay.Checkpoints[i].Departure += duration;
            }

            return duration;
        }
        

        private static TimeSpan removeTripEvent(IReadOnlySchedule schedule, SummaryDay summaryDay,
            int summaryPointIndex,int userEventIndex)
        {
            var duration = schedule.PlannerPreferences.TripEvents[userEventIndex].Duration;

            SummaryCheckpoint summary_point = summaryDay.Checkpoints[summaryPointIndex];
            --summary_point.EventCounters[userEventIndex];

            summary_point.Departure -= duration;
            summary_point.Break -= duration;

            for (int i = summaryPointIndex + 1; i < summaryDay.Checkpoints.Count; ++i)
            {
                summaryDay.Checkpoints[i].Arrival -= duration;
                summaryDay.Checkpoints[i].Departure -= duration;
            }
            
            return duration;
        }
        
    }
}