using SmartShiftScheduler;
using static SmartShiftScheduler.Tests.TestHelpers;

namespace SmartShiftScheduler.Tests;

public class ScheduleReportTests
{
    [Fact]
    public void Numbers_are_computed_from_the_schedule()
    {
        var a = MakeWorker("A", preferred: new[] { ShiftType.Morning });
        var b = MakeWorker("B", preferred: new[] { ShiftType.Night });
        var demands = new[]
        {
            Demand(DayOfWeek.Monday, ShiftType.Morning, 2),
            Demand(DayOfWeek.Monday, ShiftType.Night, 1)
        };
        var result = new ShiftScheduler().Build(new[] { a, b }, demands);

        var report = ScheduleReport.Create(result, new[] { a, b }, demands, new SchedulerSettings());

        Assert.Equal(3, report.RequiredSlots);
        Assert.Equal(2, report.FilledSlots);
        Assert.Equal(2.0 / 3.0, report.Coverage, 9);
        Assert.Equal(1.0, report.AverageShiftsPerWorker, 9);
        Assert.Equal(1.0, report.FairnessIndex, 9);
        Assert.Equal(1.0, report.PreferenceMatch, 9);
        Assert.Equal(0, report.RuleViolations);
        Assert.Equal(1.0, report.RuleCompliance, 9);
    }

    [Fact]
    public void Rule_compliance_drops_when_the_schedule_breaks_a_rule()
    {
        var worker = MakeWorker("A", days: new[] { DayOfWeek.Monday });
        var result = new ScheduleResult();
        result.Assignments.Add(new ShiftAssignment(DayOfWeek.Monday, ShiftType.Morning, worker));
        result.Assignments.Add(new ShiftAssignment(DayOfWeek.Tuesday, ShiftType.Morning, worker)); // not available on Tuesday

        var report = ScheduleReport.Create(result, new[] { worker }, Array.Empty<ShiftDemand>(), new SchedulerSettings());

        Assert.True(report.RuleViolations >= 1);
        Assert.Equal(0.5, report.RuleCompliance, 9);
    }

    [Fact]
    public void Fairness_index_shrinks_with_the_gap_between_busiest_and_least_busy_worker()
    {
        var busy = MakeWorker("A");
        var idle = MakeWorker("B", days: new[] { DayOfWeek.Sunday });
        var demands = new[]
        {
            Demand(DayOfWeek.Monday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Tuesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Wednesday, ShiftType.Morning, 1)
        };
        var result = new ShiftScheduler().Build(new[] { busy, idle }, demands);

        var report = ScheduleReport.Create(result, new[] { busy, idle }, demands, new SchedulerSettings());

        Assert.Equal(3, report.MaxShifts);
        Assert.Equal(0, report.MinShifts);
        Assert.Equal(1.0 - 3.0 / 7.0, report.FairnessIndex, 9);
    }
}

/// <summary>The scenario from the real survey: 16 people, three departments, too few people for every shift.</summary>
public class RealSurveyScheduleTests
{
    private static (ParseResult Parsed, List<ShiftDemand> Demands, ScheduleResult Result) Build()
    {
        var parsed = ResponseCsvParser.ParseFile(ResponsesCsvPath);
        var demands = DemandPredictor.BuildWeeklyDemand(Enum.GetValues<Department>());
        var result = new ShiftScheduler().Build(parsed.Workers, demands);
        return (parsed, demands, result);
    }

    [Fact]
    public void A_schedule_is_produced_for_every_department()
    {
        var (_, _, result) = Build();

        foreach (var dept in Enum.GetValues<Department>())
        {
            Assert.Contains(result.Assignments, a => a.Dept == dept);
        }
    }

    [Fact]
    public void The_schedule_breaks_no_rule()
    {
        var (_, _, result) = Build();

        Assert.Empty(ScheduleValidator.Validate(result.Assignments, new SchedulerSettings()));
    }

    [Fact]
    public void Staffed_places_plus_missing_places_equal_the_forecast_demand()
    {
        var (_, demands, result) = Build();

        int required = demands.Sum(d => d.Required);
        int missing = result.Shortages.Sum(s => s.Missing);

        Assert.Equal(required, result.Assignments.Count + missing);
        Assert.All(result.Shortages, s => Assert.True(s.Missing > 0));
    }

    [Fact]
    public void Housekeeping_cannot_cover_sunday_because_only_one_person_is_available()
    {
        var (_, _, result) = Build();

        Assert.Contains(result.Shortages, s => s.Dept == Department.Housekeeping && s.Day == DayOfWeek.Sunday);
        Assert.True(result.Assignments.Count(a => a.Dept == Department.Housekeeping && a.Day == DayOfWeek.Sunday) <= 1);
    }

    [Fact]
    public void Nobody_works_a_slot_they_blocked_in_the_survey()
    {
        var (_, _, result) = Build();

        Assert.DoesNotContain(
            result.Assignments,
            a => a.Worker.UnavailableSlots.Contains(new Slot(a.Day, a.Shift)));
    }
}
