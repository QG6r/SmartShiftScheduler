namespace SmartShiftScheduler;

/// <summary>
/// Builds a weekly schedule with a deterministic greedy algorithm.
///
/// Demand is SOFT: if nobody can legally take a shift, the shift is left short and reported in
/// <see cref="ScheduleResult.Shortages"/>; the scheduler never fails and never breaks a rule to fill a gap.
///
/// Hard rules (never broken): worker is available that day, is not blocked for that exact shift, works at most one shift
/// per day, stays within the weekly hour limit and the wanted days off, does not exceed the max consecutive days,
/// and does not start a morning/afternoon shift right after a night shift.
///
/// Preferences are soft: pass 1 fills every shift with workers who prefer it, pass 2 fills what is left with anyone allowed.
/// Within a pass the worker with the fewest shifts so far goes first, then the lowest id, so the result is always the same.
/// </summary>
public sealed class ShiftScheduler
{
    private readonly SchedulerSettings _settings;

    public ShiftScheduler(SchedulerSettings? settings = null)
    {
        _settings = settings ?? new SchedulerSettings();
    }

    /// <summary>Most shifts one worker can take: the hour limit, reduced by the days off the worker asked for.</summary>
    public static int WeeklyCap(Worker worker, SchedulerSettings settings)
    {
        return Math.Min(settings.MaxWeeklyShifts, Math.Max(0, 7 - worker.RequestedOffDays));
    }

    public ScheduleResult Build(IReadOnlyList<Worker> workers, IReadOnlyList<ShiftDemand> demands)
    {
        var result = new ScheduleResult();
        var states = workers.ToDictionary(w => w, _ => new WorkerState());
        var assigned = new int[demands.Count];

        foreach (bool onlyPreferred in new[] { true, false })
        {
            for (int i = 0; i < demands.Count; i++)
            {
                var demand = demands[i];

                while (assigned[i] < demand.Required)
                {
                    var candidate = workers
                        .Where(w => w.Dept == demand.Dept)
                        .Where(w => !onlyPreferred || w.PreferredShifts.Contains(demand.Shift))
                        .Where(w => CanAssign(w, states[w], demand.Day, demand.Shift))
                        .OrderBy(w => states[w].ShiftCount)
                        .ThenBy(w => w.Id, StringComparer.Ordinal)
                        .FirstOrDefault();

                    if (candidate is null)
                    {
                        break;
                    }

                    states[candidate].ShiftByDay[demand.Day] = demand.Shift;
                    result.Assignments.Add(new ShiftAssignment(demand.Day, demand.Shift, candidate));
                    assigned[i]++;
                }
            }
        }

        for (int i = 0; i < demands.Count; i++)
        {
            if (assigned[i] < demands[i].Required)
            {
                var d = demands[i];
                result.Shortages.Add(new Shortage(d.Dept, d.Day, d.Shift, d.Required, assigned[i]));
            }
        }

        return result;
    }

    private bool CanAssign(Worker worker, WorkerState state, DayOfWeek day, ShiftType shift)
    {
        if (!worker.AvailableDays.Contains(day)) return false;
        if (worker.UnavailableSlots.Contains(new Slot(day, shift))) return false;
        if (state.ShiftByDay.ContainsKey(day)) return false;
        if (state.ShiftCount >= WeeklyCap(worker, _settings)) return false;

        // Recovery rule: no morning/afternoon shift on the day after a night shift...
        if (shift != ShiftType.Night)
        {
            if (state.ShiftByDay.TryGetValue(Week.Previous(day), out var before) && before == ShiftType.Night)
            {
                return false;
            }
        }
        // ...and, going the other way, no night shift on the day before a morning/afternoon shift.
        else if (state.ShiftByDay.TryGetValue(Week.Next(day), out var after) && after != ShiftType.Night)
        {
            return false;
        }

        return Week.RunLength(state.ShiftByDay.Keys.ToHashSet(), day) <= worker.MaxConsecutiveDays;
    }

    private sealed class WorkerState
    {
        public Dictionary<DayOfWeek, ShiftType> ShiftByDay { get; } = new();

        public int ShiftCount => ShiftByDay.Count;
    }
}
