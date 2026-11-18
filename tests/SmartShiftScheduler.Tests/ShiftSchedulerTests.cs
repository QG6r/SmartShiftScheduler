using SmartShiftScheduler;
using static SmartShiftScheduler.Tests.TestHelpers;

namespace SmartShiftScheduler.Tests;

public class ShiftSchedulerTests
{
    [Fact]
    public void Staffs_a_shift_when_enough_workers_exist()
    {
        var workers = new[] { MakeWorker("A"), MakeWorker("B"), MakeWorker("C") };

        var result = Run(workers, Demand(DayOfWeek.Monday, ShiftType.Morning, 2));

        Assert.Equal(2, result.Assignments.Count);
        Assert.Empty(result.Shortages);
    }

    [Fact]
    public void Reports_a_shortage_instead_of_failing()
    {
        var result = Run(new[] { MakeWorker("A") }, Demand(DayOfWeek.Monday, ShiftType.Morning, 3));

        Assert.Single(result.Assignments);
        var shortage = Assert.Single(result.Shortages);
        Assert.Equal(3, shortage.Required);
        Assert.Equal(1, shortage.Assigned);
        Assert.Equal(2, shortage.Missing);
    }

    [Fact]
    public void Does_not_use_a_worker_on_a_day_they_are_not_available()
    {
        var worker = MakeWorker("A", days: new[] { DayOfWeek.Tuesday });

        var result = Run(new[] { worker }, Demand(DayOfWeek.Monday, ShiftType.Morning, 1));

        Assert.Empty(result.Assignments);
        Assert.Single(result.Shortages);
    }

    [Fact]
    public void Does_not_use_a_worker_on_a_shift_they_strictly_cannot_work()
    {
        var blocked = MakeWorker("A", blocked: new[] { new Slot(DayOfWeek.Monday, ShiftType.Morning) });
        var free = MakeWorker("B");

        var result = Run(new[] { blocked, free }, Demand(DayOfWeek.Monday, ShiftType.Morning, 1));

        Assert.Equal("B", Assert.Single(result.Assignments).Worker.Id);
    }

    [Fact]
    public void A_blocked_shift_does_not_block_the_other_shifts_of_that_day()
    {
        var worker = MakeWorker("A", blocked: new[] { new Slot(DayOfWeek.Monday, ShiftType.Morning) });

        var result = Run(new[] { worker }, Demand(DayOfWeek.Monday, ShiftType.Afternoon, 1));

        Assert.Single(result.Assignments);
    }

    [Fact]
    public void Gives_a_worker_at_most_one_shift_per_day()
    {
        var result = Run(
            new[] { MakeWorker("A") },
            Demand(DayOfWeek.Monday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Monday, ShiftType.Afternoon, 1));

        Assert.Single(result.Assignments);
        Assert.Single(result.Shortages);
    }

    [Fact]
    public void Does_not_put_a_morning_shift_right_after_a_night_shift()
    {
        var result = Run(
            new[] { MakeWorker("A") },
            Demand(DayOfWeek.Monday, ShiftType.Night, 1),
            Demand(DayOfWeek.Tuesday, ShiftType.Morning, 1));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(ShiftType.Night, assignment.Shift);
        Assert.Equal(DayOfWeek.Tuesday, Assert.Single(result.Shortages).Day);
    }

    [Fact]
    public void Does_not_put_a_night_shift_right_before_an_already_planned_morning_shift()
    {
        // Tuesday morning is planned first, so Monday night would break the recovery rule.
        var result = Run(
            new[] { MakeWorker("A") },
            Demand(DayOfWeek.Tuesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Monday, ShiftType.Night, 1));

        Assert.Equal(DayOfWeek.Tuesday, Assert.Single(result.Assignments).Day);
        Assert.Equal(DayOfWeek.Monday, Assert.Single(result.Shortages).Day);
    }

    [Fact]
    public void Back_to_back_night_shifts_are_allowed()
    {
        var result = Run(
            new[] { MakeWorker("A") },
            Demand(DayOfWeek.Monday, ShiftType.Night, 1),
            Demand(DayOfWeek.Tuesday, ShiftType.Night, 1));

        Assert.Equal(2, result.Assignments.Count);
    }

    [Fact]
    public void Weekly_hour_limit_caps_shifts_at_five()
    {
        var demands = Week.Days.Select(d => Demand(d, ShiftType.Morning, 1)).ToArray();

        var result = Run(new[] { MakeWorker("A") }, demands);

        Assert.Equal(5, result.Assignments.Count);
        Assert.Equal(2, result.Shortages.Count);
    }

    [Fact]
    public void Wanted_days_off_lower_the_weekly_limit()
    {
        var demands = Week.Days.Select(d => Demand(d, ShiftType.Morning, 1)).ToArray();

        var result = Run(new[] { MakeWorker("A", daysOff: 3) }, demands);

        Assert.Equal(4, result.Assignments.Count);
    }

    [Fact]
    public void Respects_the_maximum_number_of_consecutive_days()
    {
        var demands = new[]
        {
            Demand(DayOfWeek.Monday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Tuesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Wednesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Thursday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Friday, ShiftType.Morning, 1)
        };

        var result = Run(new[] { MakeWorker("A", maxConsecutive: 2) }, demands);

        var days = result.Assignments.Select(a => a.Day).ToHashSet();
        Assert.Equal(new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday }, days);
        Assert.Equal(DayOfWeek.Wednesday, Assert.Single(result.Shortages).Day);
    }

    [Fact]
    public void Prefers_the_worker_who_prefers_the_shift()
    {
        // Without the preference rule "A" would win because of the lower id.
        var nightPerson = MakeWorker("A", preferred: new[] { ShiftType.Night });
        var morningPerson = MakeWorker("B", preferred: new[] { ShiftType.Morning });

        var result = Run(new[] { nightPerson, morningPerson }, Demand(DayOfWeek.Monday, ShiftType.Morning, 1));

        Assert.Equal("B", Assert.Single(result.Assignments).Worker.Id);
    }

    [Fact]
    public void Falls_back_to_a_worker_who_does_not_prefer_the_shift_when_nobody_else_can_work()
    {
        var nightPerson = MakeWorker("A", preferred: new[] { ShiftType.Night });

        var result = Run(new[] { nightPerson }, Demand(DayOfWeek.Monday, ShiftType.Morning, 1));

        Assert.Single(result.Assignments);
        Assert.Empty(result.Shortages);
    }

    [Fact]
    public void Spreads_shifts_evenly_between_equal_workers()
    {
        var demands = new[]
        {
            Demand(DayOfWeek.Monday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Tuesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Wednesday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Thursday, ShiftType.Morning, 1),
            Demand(DayOfWeek.Friday, ShiftType.Morning, 1)
        };
        var workers = new[] { MakeWorker("A"), MakeWorker("B") };

        var result = Run(workers, demands);

        int a = result.Assignments.Count(x => x.Worker.Id == "A");
        int b = result.Assignments.Count(x => x.Worker.Id == "B");
        Assert.Equal(5, a + b);
        Assert.InRange(Math.Abs(a - b), 0, 1);
    }

    [Fact]
    public void Never_staffs_a_department_with_workers_from_another_one()
    {
        var reception = MakeWorker("A", Department.Reception);

        var result = Run(new[] { reception }, Demand(DayOfWeek.Monday, ShiftType.Morning, 1, Department.Housekeeping));

        Assert.Empty(result.Assignments);
        Assert.Single(result.Shortages);
    }

    [Fact]
    public void Same_input_always_gives_the_same_schedule()
    {
        var parsed = ResponseCsvParser.ParseFile(ResponsesCsvPath);
        var demands = DemandPredictor.BuildWeeklyDemand(Enum.GetValues<Department>());

        var first = new ShiftScheduler().Build(parsed.Workers, demands);
        var second = new ShiftScheduler().Build(parsed.Workers, demands);

        Assert.Equal(
            first.Assignments.Select(a => (a.Day, a.Shift, a.Worker.Id)).ToList(),
            second.Assignments.Select(a => (a.Day, a.Shift, a.Worker.Id)).ToList());
    }
}
