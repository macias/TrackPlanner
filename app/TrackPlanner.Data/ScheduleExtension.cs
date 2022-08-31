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

            var summary_day = new SummaryDay() {Start = start};
            // if we don't have anything (we just started from scratch) do not create any checkpoints as well
            // just an empty day, that's all,
            // NOTE: for last day, looped, not having any anchors is a valid scenario (it starts from the beginning
            // of the previous day and loop to the global start) 
            if (dayIndex == 0 && day.Anchors.Count == 0)
                return summary_day;
            
            var last_shopping = start;

                summary_day.Checkpoints.Add(new SummaryCheckpoint()
                {
                    Arrival = start,
                    Departure = start,
                });
                int anchor_idx = dayIndex == 0 ? 1 : 0;
                for (; anchor_idx < day.Anchors.Count; ++anchor_idx)
                    summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day, ref last_shopping,
                        ref rolling_time, dayIndex, anchor_idx));

                if (schedule.IsLoopedDay(dayIndex))
                    summary_day.Checkpoints.Add(schedule.createSummaryPoint(summary_day, ref last_shopping,
                        ref rolling_time, dayIndex, anchor_idx));

                Console.WriteLine("DEBUG fixing last checkpoint for the day");
                
                var last_checkpoint = summary_day.Checkpoints.Last();

                Console.WriteLine("DEBUG adding extra snack time");

                // if there is too big gap between last shopping and the end of the day add one more snack time
                if (last_checkpoint.Departure - last_shopping > schedule.PlannerPreferences.ShoppingInterval / 2)
                {
                    TimeSpan shop_at = last_checkpoint.Departure - schedule.PlannerPreferences.SnackTimeDuration;
                    addShopping(schedule, summary_day, last_checkpoint, ref shop_at, campResupply: false);
                }

                if (!dayEndsAtHome(schedule, dayIndex))
                {
                    // last resupply except final day (water for camping)

                    // we find and convert last snack time into camp resupply
                    var snack_point_idx = summary_day.Checkpoints.FindLastIndex(it => it.SnackTimesAt.Any());
                    Console.WriteLine($"DEBUG extending snack time {snack_point_idx}");
                    if (snack_point_idx != -1)
                    {
                        var snack_duration = removeLastSnackTime(schedule, summary_day, summary_day.Checkpoints[snack_point_idx], out TimeSpan snack_time);
                        var resupply_duration = addShopping(schedule, summary_day, summary_day.Checkpoints[snack_point_idx], ref snack_time, campResupply: true);
                        var diff_duration = resupply_duration - snack_duration;

                        foreach (var point in summary_day.Checkpoints.Skip(snack_point_idx + 1))
                        {
                            point.Arrival += diff_duration;
                            point.Departure += diff_duration;
                        }
                    }
                }
                
                Console.WriteLine("DEBUG clearing last checkpoint for the day");
                
                last_checkpoint.Break = TimeSpan.Zero;
                // since we don't count in break for the last checkpoint we have to shift
                // arrival time to departure
                last_checkpoint.Arrival = last_checkpoint.Departure;


                summary_day.Distance = summary_day.Checkpoints.Select(it => it.IncomingDistance).Sum();

                TimeSpan late_camping = last_checkpoint.Arrival - ((!schedule.EndsAtHome || dayIndex < schedule.Days.Count - 1)
                    ? schedule.PlannerPreferences.CampLandingTime
                    : schedule.PlannerPreferences.HomeLandingTime);
                if ( late_camping > TimeSpan.Zero)
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

        private static SummaryCheckpoint createSummaryPoint(this IReadOnlySchedule schedule, SummaryDay summaryDay, ref TimeSpan lastShopping,
            ref TimeSpan rollingTime,
            int dayIndex, int anchorIndex)
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

            var summary_point = new SummaryCheckpoint()
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

            var starts_at_home = (schedule.StartsAtHome && dayIndex == 0);
            var ends_at_home = dayEndsAtHome(schedule, dayIndex);
            var is_home_adjacent = ends_at_home || starts_at_home;

            while (summary_point.Departure - lastShopping > schedule.PlannerPreferences.ShoppingInterval)
            {
                lastShopping += schedule.PlannerPreferences.ShoppingInterval;

                addShopping(schedule, summaryDay, summary_point, ref lastShopping, 
                    // first resupply only on "middle" days, food for next day
                    campResupply:!is_home_adjacent && !summaryDay.Checkpoints.Any(it => it.CampRessuplyAt.HasValue));
            }

            TimeSpan last_event = lastShopping;

            if (!is_home_adjacent
                && summary_point.Departure > schedule.PlannerPreferences.LaundryOpportunity
                && summaryDay.Checkpoints.All(it => it.LaundryAt == null))
            {
                summary_point.LaundryAt = schedule.PlannerPreferences.LaundryOpportunity.Max(last_event);
                var duration = schedule.PlannerPreferences.LaundryDuration;

                summaryDay.LaundryDuration += duration;
                summary_point.Break += duration;
                summary_point.Departure += duration;

                last_event = summary_point.LaundryAt.Value + duration;
            }

            if (summary_point.Departure > schedule.PlannerPreferences.LunchOpportunity
                && summaryDay.Checkpoints.All(it => it.LunchAt == null))
            {
                summary_point.LunchAt = schedule.PlannerPreferences.LunchOpportunity.Max(last_event);
                var duration = schedule.PlannerPreferences.LunchDuration;

                summaryDay.LunchDuration += duration;
                summary_point.Break += duration;
                summary_point.Departure += duration;

                last_event = summary_point.LunchAt.Value + duration;
            }

            return summary_point;
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

        private static TimeSpan addShopping(IReadOnlySchedule readOnlySchedule, SummaryDay summaryDay, 
            SummaryCheckpoint summaryPoint, ref TimeSpan shoppingAt, bool campResupply)
        {
            TimeSpan duration;
            if (campResupply)
            {
                duration = readOnlySchedule.PlannerPreferences.CampResupplyDuration;
                summaryPoint.CampRessuplyAt = shoppingAt;

                summaryDay.CampResupplyDuration += duration;
            }
            else
            {
                duration = readOnlySchedule.PlannerPreferences.SnackTimeDuration;
                summaryPoint.SnackTimesAt.Add(shoppingAt);

                summaryDay.SnackTimesDuration += duration;
            }

            shoppingAt += duration;
            summaryPoint.Departure += duration;
            summaryPoint.Break += duration;

            return duration;
        }

        private static TimeSpan removeLastSnackTime(IReadOnlySchedule readOnlySchedule, SummaryDay summaryDay,
            SummaryCheckpoint summaryPoint,out TimeSpan snackTime)
        {
            TimeSpan duration;

            duration = readOnlySchedule.PlannerPreferences.SnackTimeDuration;
            snackTime = summaryPoint.SnackTimesAt[^1];
            summaryPoint.SnackTimesAt.RemoveLast();

            summaryDay.SnackTimesDuration -= duration;

            summaryPoint.Departure -= duration;
            summaryPoint.Break -= duration;

            return duration;
        }

    }
}