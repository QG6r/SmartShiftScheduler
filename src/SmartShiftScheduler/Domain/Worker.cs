namespace SmartShiftScheduler;

/// <summary>One survey respondent. Immutable input data: the scheduler keeps its own working state.</summary>
public sealed class Worker
{
    public required string Id { get; init; }

    public required Department Dept { get; init; }

    /// <summary>Days of the week the worker is available at all (survey question 3).</summary>
    public HashSet<DayOfWeek> AvailableDays { get; init; } = new();

    /// <summary>Preferred shifts (survey question 4). A preference, not a restriction.</summary>
    public HashSet<ShiftType> PreferredShifts { get; init; } = new();

    /// <summary>Shifts the worker strictly cannot work (survey question 6, parsed from free text).</summary>
    public HashSet<Slot> UnavailableSlots { get; init; } = new();

    /// <summary>Days off the worker wants this week (survey question 5).</summary>
    public int RequestedOffDays { get; init; }

    /// <summary>Longest run of working days the worker accepts (survey question 7).</summary>
    public int MaxConsecutiveDays { get; init; } = 5;

    /// <summary>Free text from question 6 that could not be turned into days or shifts. It is NOT applied.</summary>
    public string? IgnoredUnavailabilityNote { get; init; }

    public override string ToString() => $"{Id} ({Dept})";
}
