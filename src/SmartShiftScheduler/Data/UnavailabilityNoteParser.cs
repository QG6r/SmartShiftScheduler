using System.Text.RegularExpressions;

namespace SmartShiftScheduler;

/// <summary>
/// Turns the free-text answer to "When can you STRICTLY NOT work?" into concrete (day, shift) slots.
/// Understood: day names (Monday, Mondays, Mon, Weds ...), "weekend", and the shift words morning / afternoon / night.
/// "Thursday Night Shift" blocks one slot; "Monday fully" blocks the whole day; "Night" alone blocks that shift every day.
/// Anything else ("Birthday", "National holidays") cannot be mapped to a weekly slot and is reported as not understood.
/// </summary>
public static class UnavailabilityNoteParser
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly (DayOfWeek Day, Regex Pattern)[] DayPatterns =
    {
        (DayOfWeek.Monday, new Regex(@"\bmon(day)?s?\b", Options)),
        (DayOfWeek.Tuesday, new Regex(@"\btue(s|sday)?s?\b", Options)),
        (DayOfWeek.Wednesday, new Regex(@"\bwed(s|nesday)?s?\b", Options)),
        (DayOfWeek.Thursday, new Regex(@"\bthu(r|rs|rsday)?s?\b", Options)),
        (DayOfWeek.Friday, new Regex(@"\bfri(day)?s?\b", Options)),
        (DayOfWeek.Saturday, new Regex(@"\bsat(urday)?s?\b", Options)),
        (DayOfWeek.Sunday, new Regex(@"\bsun(day)?s?\b", Options))
    };

    private static readonly Regex Weekend = new(@"\bweekends?\b", Options);

    private static readonly (ShiftType Shift, Regex Pattern)[] ShiftPatterns =
    {
        (ShiftType.Morning, new Regex(@"\bmornings?\b", Options)),
        (ShiftType.Afternoon, new Regex(@"\bafternoons?\b", Options)),
        (ShiftType.Night, new Regex(@"\bnights?\b", Options))
    };

    // "No any", "none", "nothing", "n/a", "-" or an empty answer: nothing to block.
    private static readonly Regex NothingToBlock = new(@"^\s*(no\b.*|none|nothing|n/?a|-+)?\s*$", Options);

    public sealed record Result(HashSet<Slot> Slots, bool Understood);

    public static Result Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || NothingToBlock.IsMatch(text))
        {
            return new Result(new HashSet<Slot>(), true);
        }

        var days = new HashSet<DayOfWeek>();
        foreach (var (day, pattern) in DayPatterns)
        {
            if (pattern.IsMatch(text)) days.Add(day);
        }

        if (Weekend.IsMatch(text))
        {
            days.Add(DayOfWeek.Saturday);
            days.Add(DayOfWeek.Sunday);
        }

        var shifts = new HashSet<ShiftType>();
        foreach (var (shift, pattern) in ShiftPatterns)
        {
            if (pattern.IsMatch(text)) shifts.Add(shift);
        }

        if (days.Count == 0 && shifts.Count == 0)
        {
            return new Result(new HashSet<Slot>(), false);
        }

        if (days.Count == 0) days.UnionWith(Week.Days);
        if (shifts.Count == 0) shifts.UnionWith(Enum.GetValues<ShiftType>());

        var slots = new HashSet<Slot>();
        foreach (var day in days)
        {
            foreach (var shift in shifts)
            {
                slots.Add(new Slot(day, shift));
            }
        }

        return new Result(slots, true);
    }
}
