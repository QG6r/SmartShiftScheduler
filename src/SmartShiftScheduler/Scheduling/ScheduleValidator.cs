namespace SmartShiftScheduler;

public sealed record RuleViolation(string WorkerId, DayOfWeek Day, ShiftType Shift, string Rule);

/// <summary>
/// Checks a finished schedule against the hard rules. It is written separately from <see cref="ShiftScheduler"/>
/// on purpose: it looks only at the result, so it can catch mistakes in the scheduler (and in hand-made schedules).
/// </summary>
public static class ScheduleValidator
{
    public static List<RuleViolation> Validate(IEnumerable<ShiftAssignment> schedule, SchedulerSettings settings)
    {
        var violations = new List<RuleViolation>();

        foreach (var group in schedule.GroupBy(a => a.Worker))
        {
            var worker = group.Key;
            var assignments = group
                .OrderBy(a => Array.IndexOf(Week.Days, a.Day))
                .ThenBy(a => a.Shift)
                .ToList();

            var workedDays = assignments.Select(a => a.Day).ToHashSet();
            var nightDays = assignments.Where(a => a.Shift == ShiftType.Night).Select(a => a.Day).ToHashSet();
            int cap = ShiftScheduler.WeeklyCap(worker, settings);
            var seenDays = new HashSet<DayOfWeek>();

            for (int i = 0; i < assignments.Count; i++)
            {
                var a = assignments[i];

                void Report(string rule) => violations.Add(new RuleViolation(worker.Id, a.Day, a.Shift, rule));

                if (!worker.AvailableDays.Contains(a.Day))
                {
                    Report("Worker is not available on this day");
                }

                if (worker.UnavailableSlots.Contains(new Slot(a.Day, a.Shift)))
                {
                    Report("Worker said they strictly cannot work this shift");
                }

                if (!seenDays.Add(a.Day))
                {
                    Report("More than one shift on the same day");
                }

                if (i >= cap)
                {
                    Report("Weekly shift limit exceeded");
                }

                if (a.Shift != ShiftType.Night && nightDays.Contains(Week.Previous(a.Day)))
                {
                    Report("Morning/afternoon shift right after a night shift");
                }

                if (Week.RunLength(workedDays, a.Day) > worker.MaxConsecutiveDays)
                {
                    Report("More consecutive working days than the worker accepts");
                }
            }
        }

        return violations;
    }
}
