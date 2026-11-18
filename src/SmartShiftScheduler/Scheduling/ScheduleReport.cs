namespace SmartShiftScheduler;

/// <summary>Quality numbers for one schedule. Every value is computed from the schedule, none is hard-coded.</summary>
public sealed record ScheduleReport(
    int RequiredSlots,
    int FilledSlots,
    double Coverage,
    double AverageShiftsPerWorker,
    int MinShifts,
    int MaxShifts,
    double FairnessIndex,
    double PreferenceMatch,
    int RuleViolations,
    double RuleCompliance)
{
    public static ScheduleReport Create(
        ScheduleResult result,
        IReadOnlyList<Worker> workers,
        IReadOnlyList<ShiftDemand> demands,
        SchedulerSettings settings)
    {
        int required = demands.Sum(d => d.Required);
        int filled = result.Assignments.Count;

        var shiftsPerWorker = workers
            .Select(w => result.Assignments.Count(a => a.Worker == w))
            .ToList();

        int min = shiftsPerWorker.Count == 0 ? 0 : shiftsPerWorker.Min();
        int max = shiftsPerWorker.Count == 0 ? 0 : shiftsPerWorker.Max();
        double average = shiftsPerWorker.Count == 0 ? 0 : shiftsPerWorker.Average();

        int preferred = result.Assignments.Count(a => a.Worker.PreferredShifts.Contains(a.Shift));

        var violations = ScheduleValidator.Validate(result.Assignments, settings);
        int violatingAssignments = violations.Select(v => (v.WorkerId, v.Day, v.Shift)).Distinct().Count();

        return new ScheduleReport(
            RequiredSlots: required,
            FilledSlots: filled,
            Coverage: required == 0 ? 1.0 : (double)filled / required,
            AverageShiftsPerWorker: average,
            MinShifts: min,
            MaxShifts: max,
            FairnessIndex: 1.0 - (double)(max - min) / 7.0,
            PreferenceMatch: filled == 0 ? 1.0 : (double)preferred / filled,
            RuleViolations: violations.Count,
            RuleCompliance: filled == 0 ? 1.0 : 1.0 - (double)violatingAssignments / filled);
    }
}
