using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public static class ScheduleLikeExtension
    {
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

        public static bool HasMultiplePoints(this IReadOnlySchedule schedule)
        {
            return schedule.Days.SelectMany(it => it.Anchors).HasMany();
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

            var summary_day = new SummaryDay(eventsCount: schedule.PlannerPreferences.UserEvents.Length) {Start = start};
            // if we don't have anything (we just started from scratch) do not create any checkpoints as well
            // just an empty day, that's all,
            // NOTE: for last day, looped, not having any anchors is a valid scenario (it starts from the beginning
            // of the previous day and loop to the global start) 
            if (dayIndex == 0 && day.Anchors.Count == 0)
                return summary_day;


            var last_events = schedule.PlannerPreferences.UserEvents.Select(_ => start).ToArray();

            summary_day.Checkpoints.Add(new SummaryCheckpoint(eventsCount: schedule.PlannerPreferences.UserEvents.Length)
            {
                Arrival = start,
                Departure = start,
            });

            var last_shopping = start;

            addTripEvents(schedule, summary_day, ref last_shopping, last_events, dayIndex);

            int anchor_idx = dayIndex == 0 ? 1 : 0;
            for (; anchor_idx < day.Anchors.Count; ++anchor_idx)
            {
                summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day,
                    ref rolling_time, dayIndex, anchor_idx));
                addTripEvents(schedule, summary_day, ref last_shopping, last_events, dayIndex);
            }


            if (schedule.IsLoopedDay(dayIndex))
            {
                summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day,
                    ref rolling_time, dayIndex, anchor_idx));
                addTripEvents(schedule, summary_day, ref last_shopping, last_events, dayIndex);
            }

            Console.WriteLine("DEBUG fixing last checkpoint for the day");

            var last_checkpoint = summary_day.Checkpoints.Last();

            Console.WriteLine("DEBUG adding extra snack time");

            // if there is too big gap between last shopping and the end of the day add one more snack time
            if (last_checkpoint.Departure - last_shopping > schedule.PlannerPreferences.ShoppingInterval / 2)
            {
                TimeSpan shop_at = last_checkpoint.Departure - schedule.PlannerPreferences.EventDuration[TripEvent.SnackTime];
                shop_at += addTripEventLocally(schedule, summary_day, last_checkpoint, TripEvent.SnackTime);
            }

            if (!dayEndsAtHome(schedule, dayIndex))
            {
                // last resupply except final day (water for camping)

                // we find and convert last snack time into camp resupply
                var snack_point_idx = summary_day.Checkpoints.FindLastIndex(it => it.EventCount[TripEvent.SnackTime] != 0);
                Console.WriteLine($"DEBUG extending snack time {snack_point_idx}");
                if (snack_point_idx != -1)
                {
                    var snack_duration = removeTripEventLocally(schedule, summary_day, summary_day.Checkpoints[snack_point_idx], TripEvent.SnackTime);
                    var resupply_duration = addTripEventLocally(schedule, summary_day, summary_day.Checkpoints[snack_point_idx], TripEvent.Resupply);
                    var diff_duration = resupply_duration - snack_duration;

                    foreach (var point in summary_day.Checkpoints.Skip(snack_point_idx + 1))
                    {
                        point.Arrival += diff_duration;
                        point.Departure += diff_duration;
                    }
                }
            }

            // moving all events from the last checkpoint to the previous one
            // rationale: when reading summary it is surprise effect that last checkpoint (most likely camping)
            // has snack time included, while it is not possible
            {
                foreach (var trip_event in Enum.GetValues<TripEvent>())
                    while (last_checkpoint.EventCount[trip_event] > 0)
                    {
                        removeTripEventLocally(schedule, summary_day, last_checkpoint, trip_event);
                        var event_duration = addTripEventLocally(schedule, summary_day, summary_day.Checkpoints[^2], trip_event);
                        last_checkpoint.Arrival += event_duration;
                        last_checkpoint.Departure += event_duration;
                    }

                for (int event_idx = 0; event_idx < last_checkpoint.UserEventsCounter.Length; ++event_idx)
                    while (last_checkpoint.UserEventsCounter[event_idx] > 0)
                    {
                        removeTripEventLocally(schedule, summary_day, last_checkpoint, event_idx);
                        var event_duration = addTripEventLocally(schedule, summary_day, summary_day.Checkpoints[^2], event_idx);
                        last_checkpoint.Arrival += event_duration;
                        last_checkpoint.Departure += event_duration;
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
                        // for opening legs on next days, the starting anchor is last one from previous day
                        // we indicate it by returning effectively -1 for such case
                        return (day_idx, local_leg_idx - 1, starting_day_idx);
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
            var true_time = DataHelper.CalcTrueTime(rollingTime, leg.UnsimplifiedDistance, leg.RawTime, schedule.PlannerPreferences.GetLowRidingSpeedLimit(),
                schedule.PlannerPreferences.HourlyStamina);
            rollingTime += true_time;
            start += true_time;


            var break_time = is_last_of_day ? TimeSpan.Zero : anchor.Break;

            return new SummaryCheckpoint(eventsCount: schedule.PlannerPreferences.UserEvents.Length)
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

        private static void addTripEvents(IReadOnlySchedule schedule, SummaryDay summaryDay, ref TimeSpan lastShopping,TimeSpan[] lastEvents, int dayIndex)
        {
            SummaryCheckpoint current_point = summaryDay.Checkpoints.Last();
            
            var is_home_adjacent = isHomeAdjacent(schedule, dayIndex);

            while (current_point.Departure - lastShopping > schedule.PlannerPreferences.ShoppingInterval)
            {
                lastShopping = lastShopping
                    .Add(schedule.PlannerPreferences.ShoppingInterval)
                    .Add(addTripEventLocally(schedule, summaryDay, current_point,
                    // first resupply only on "middle" days, food for next day
                    !is_home_adjacent && summaryDay.Checkpoints.All(it => it.EventCount[TripEvent.Resupply] == 0) ? TripEvent.Resupply : TripEvent.SnackTime));
            }

            for (int event_idx = 0; event_idx < schedule.PlannerPreferences.UserEvents.Length;)
            {
                var user_event = schedule.PlannerPreferences.UserEvents[event_idx];

                if ((!is_home_adjacent || user_event.NearHomeEnabled)
                    && dayIndex % user_event.EveryDay == 0
                    && (user_event.Opportunity == null || current_point.Departure > user_event.Opportunity)
                    && (user_event.Interval == null || current_point.Departure - lastEvents[event_idx] > user_event.Interval)
                    && (user_event.Interval != null || summaryDay.Checkpoints.All(it => it.UserEventsCounter[event_idx] == 0)))
                {
                    addTripEventLocally(schedule, summaryDay, current_point, event_idx);

                    event_idx = 0; // we changed departure time, let's check all events again
                    continue;
                }

                ++event_idx;
            }

        }

        private static bool isHomeAdjacent(IReadOnlySchedule schedule, int dayIndex)
        {
            return (schedule.StartsAtHome && dayIndex == 0) || dayEndsAtHome(schedule, dayIndex);
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

        private static TimeSpan addTripEventLocally(IReadOnlySchedule schedule, SummaryDay summaryDay, 
            SummaryCheckpoint summaryPoint, TripEvent tripEvent)
        {
            TimeSpan duration = schedule.PlannerPreferences.EventDuration[tripEvent];
            
            ++summaryPoint.EventCount[tripEvent];
            
            summaryDay.EventDuration[tripEvent] += duration;
            
            summaryPoint.Departure += duration;
            summaryPoint.Break += duration;

            return duration;
        }

        private static TimeSpan addTripEventLocally(IReadOnlySchedule schedule, SummaryDay summaryDay, 
            SummaryCheckpoint summaryPoint, int userEventIndex)
        {
            var duration = schedule.PlannerPreferences.UserEvents[userEventIndex].Duration;
            
            ++summaryPoint.UserEventsCounter[userEventIndex];
            
            summaryDay.UserEventDuration[userEventIndex] += duration;
            
            summaryPoint.Departure += duration;
            summaryPoint.Break += duration;

            return duration;
        }
        
        private static TimeSpan removeTripEventLocally(IReadOnlySchedule schedule, SummaryDay summaryDay,
            SummaryCheckpoint summaryPoint,TripEvent tripEvent)
        {
            TimeSpan duration= schedule.PlannerPreferences.EventDuration[tripEvent];
            
            --summaryPoint.EventCount[tripEvent];

            summaryDay.EventDuration[tripEvent] -= duration;

            summaryPoint.Departure -= duration;
            summaryPoint.Break -= duration;

            return duration;
        }

        private static TimeSpan removeTripEventLocally(IReadOnlySchedule schedule, SummaryDay summaryDay,
            SummaryCheckpoint summaryPoint,int userEventIndex)
        {
            var duration = schedule.PlannerPreferences.UserEvents[userEventIndex].Duration;
            
            --summaryPoint.UserEventsCounter[userEventIndex];

            summaryDay.UserEventDuration[userEventIndex] -= duration;

            summaryPoint.Departure -= duration;
            summaryPoint.Break -= duration;

            return duration;
        }
        
    }
}