namespace SmartShiftScheduler;

public enum Department
{
    Reception,
    Housekeeping,
    FoodAndBeverage
}

/// <summary>
/// Shift names and hours are the ones used in the survey form:
/// Morning 08:00-16:00, Afternoon 16:00-00:00, Night 00:00-08:00.
/// </summary>
public enum ShiftType
{
    Morning,
    Afternoon,
    Night
}

/// <summary>One concrete shift on one day of the week.</summary>
public readonly record struct Slot(DayOfWeek Day, ShiftType Shift);

/// <summary>Helpers for a weekly schedule that repeats every week (Monday first, Sunday wraps to Monday).</summary>
public static class Week
{
    public static readonly DayOfWeek[] Days =
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    };

    public static DayOfWeek Previous(DayOfWeek day) => (DayOfWeek)(((int)day + 6) % 7);

    public static DayOfWeek Next(DayOfWeek day) => (DayOfWeek)(((int)day + 1) % 7);

    /// <summary>
    /// Length of the run of consecutive working days that contains <paramref name="day"/>,
    /// counting <paramref name="day"/> itself (whether or not it is in <paramref name="workedDays"/>).
    /// The week is cyclic, so Sunday is followed by Monday. A full week counts as 7.
    /// </summary>
    public static int RunLength(ISet<DayOfWeek> workedDays, DayOfWeek day)
    {
        int count = 1;

        for (int i = 1; i < 7; i++)
        {
            var previous = (DayOfWeek)(((int)day - i + 7) % 7);
            if (!workedDays.Contains(previous)) break;
            count++;
        }

        for (int i = 1; i < 7 && count < 7; i++)
        {
            var following = (DayOfWeek)(((int)day + i) % 7);
            if (!workedDays.Contains(following)) break;
            count++;
        }

        return count;
    }
}
