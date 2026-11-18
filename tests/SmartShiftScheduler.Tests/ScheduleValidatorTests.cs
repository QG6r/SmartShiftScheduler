using SmartShiftScheduler;
using static SmartShiftScheduler.Tests.TestHelpers;

namespace SmartShiftScheduler.Tests;

public class ScheduleValidatorTests
{
    private static List<RuleViolation> Check(params ShiftAssignment[] schedule)
    {
        return ScheduleValidator.Validate(schedule, new SchedulerSettings());
    }

    [Fact]
    public void A_clean_schedule_has_no_violations()
    {
        var worker = MakeWorker("A");

        var violations = Check(
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Morning, worker),
            new ShiftAssignment(DayOfWeek.Wednesday, ShiftType.Night, worker));

        Assert.Empty(violations);
    }

    [Fact]
    public void Detects_work_on_an_unavailable_day()
    {
        var worker = MakeWorker("A", days: new[] { DayOfWeek.Monday });

        var violations = Check(new ShiftAssignment(DayOfWeek.Tuesday, ShiftType.Morning, worker));

        Assert.Contains(violations, v => v.Rule.Contains("not available"));
    }

    [Fact]
    public void Detects_a_shift_the_worker_said_they_cannot_work()
    {
        var worker = MakeWorker("A", blocked: new[] { new Slot(DayOfWeek.Monday, ShiftType.Night) });

        var violations = Check(new ShiftAssignment(DayOfWeek.Monday, ShiftType.Night, worker));

        Assert.Contains(violations, v => v.Rule.Contains("strictly cannot"));
    }

    [Fact]
    public void Detects_two_shifts_on_one_day()
    {
        var worker = MakeWorker("A");

        var violations = Check(
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Morning, worker),
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Afternoon, worker));

        Assert.Contains(violations, v => v.Rule.Contains("same day"));
    }

    [Fact]
    public void Detects_a_morning_shift_after_a_night_shift()
    {
        var worker = MakeWorker("A");

        var violations = Check(
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Night, worker),
            new ShiftAssignment(DayOfWeek.Tuesday, ShiftType.Morning, worker));

        Assert.Contains(violations, v => v.Rule.Contains("after a night"));
    }

    [Fact]
    public void Detects_more_shifts_than_the_weekly_limit()
    {
        var worker = MakeWorker("A");
        var assignments = Week.Days.Take(6).Select(d => new ShiftAssignment(d, ShiftType.Morning, worker)).ToArray();

        var violations = Check(assignments);

        Assert.Contains(violations, v => v.Rule.Contains("Weekly shift limit"));
    }

    [Fact]
    public void Detects_too_many_consecutive_days()
    {
        var worker = MakeWorker("A", maxConsecutive: 2);

        var violations = Check(
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Morning, worker),
            new ShiftAssignment(DayOfWeek.Tuesday, ShiftType.Morning, worker),
            new ShiftAssignment(DayOfWeek.Wednesday, ShiftType.Morning, worker));

        Assert.Contains(violations, v => v.Rule.Contains("consecutive"));
    }

    [Fact]
    public void Sunday_followed_by_monday_counts_as_consecutive()
    {
        var worker = MakeWorker("A", maxConsecutive: 1);

        var violations = Check(
            new ShiftAssignment(DayOfWeek.Sunday, ShiftType.Morning, worker),
            new ShiftAssignment(DayOfWeek.Monday, ShiftType.Morning, worker));

        Assert.Contains(violations, v => v.Rule.Contains("consecutive"));
    }
}
