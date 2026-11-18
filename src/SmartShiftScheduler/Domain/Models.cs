namespace SmartShiftScheduler;

/// <summary>How many people a department needs on one shift.</summary>
public sealed record ShiftDemand(DayOfWeek Day, ShiftType Shift, Department Dept, int Required);

/// <summary>One worker placed on one shift.</summary>
public sealed record ShiftAssignment(DayOfWeek Day, ShiftType Shift, Worker Worker)
{
    public Department Dept => Worker.Dept;
}

/// <summary>A shift that could not be fully staffed.</summary>
public sealed record Shortage(Department Dept, DayOfWeek Day, ShiftType Shift, int Required, int Assigned)
{
    public int Missing => Required - Assigned;
}

public sealed class ScheduleResult
{
    public List<ShiftAssignment> Assignments { get; } = new();

    public List<Shortage> Shortages { get; } = new();
}

public sealed record SchedulerSettings
{
    /// <summary>Every shift lasts 8 hours (see <see cref="ShiftType"/>).</summary>
    public int ShiftHours { get; init; } = 8;

    /// <summary>Weekly hour limit per worker.</summary>
    public int MaxWeeklyHours { get; init; } = 40;

    public int MaxWeeklyShifts => MaxWeeklyHours / ShiftHours;
}
