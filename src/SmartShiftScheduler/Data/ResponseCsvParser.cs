using System.Text.RegularExpressions;

namespace SmartShiftScheduler;

public sealed record ParseResult(List<Worker> Workers, List<string> Warnings);

/// <summary>
/// Reads the Google Forms export. Column order (0-based):
/// 0 timestamp, 1 worker id, 2 department, 3 available days (";"-separated), 4 preferred shifts,
/// 5 wanted off days, 6 "strictly cannot work" (free text), 7 max consecutive days (multi-select, e.g. "4days;5days").
/// </summary>
public static class ResponseCsvParser
{
    private const int ColumnCount = 8;

    public static ParseResult ParseFile(string path, int defaultMaxConsecutiveDays = 5)
    {
        return Parse(File.ReadAllText(path), defaultMaxConsecutiveDays);
    }

    public static ParseResult Parse(string csvText, int defaultMaxConsecutiveDays = 5)
    {
        var workers = new List<Worker>();
        var warnings = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        var records = CsvReader.Parse(csvText);

        // records[0] is the header row.
        for (int i = 1; i < records.Count; i++)
        {
            var columns = records[i];
            int rowNumber = i + 1;

            if (columns.Length < ColumnCount)
            {
                warnings.Add($"Row {rowNumber}: expected {ColumnCount} columns but found {columns.Length}, row skipped.");
                continue;
            }

            string id = columns[1].Trim();
            if (id.Length == 0)
            {
                warnings.Add($"Row {rowNumber}: empty worker id, row skipped.");
                continue;
            }

            if (!seenIds.Add(id))
            {
                warnings.Add($"Row {rowNumber}: duplicate worker id {id}, row skipped (the first answer is used).");
                continue;
            }

            if (!TryParseDepartment(columns[2], out var dept))
            {
                warnings.Add($"Row {rowNumber}: unknown department \"{columns[2].Trim()}\" for worker {id}, row skipped.");
                continue;
            }

            var note = UnavailabilityNoteParser.Parse(columns[6]);

            workers.Add(new Worker
            {
                Id = id,
                Dept = dept,
                AvailableDays = ParseDays(columns[3]),
                PreferredShifts = ParseShifts(columns[4]),
                RequestedOffDays = ParseOffDays(columns[5]),
                UnavailableSlots = note.Slots,
                MaxConsecutiveDays = ParseMaxConsecutiveDays(columns[7], defaultMaxConsecutiveDays),
                IgnoredUnavailabilityNote = note.Understood ? null : columns[6].Trim()
            });
        }

        return new ParseResult(workers, warnings);
    }

    private static bool TryParseDepartment(string value, out Department dept)
    {
        string v = value.Trim().ToLowerInvariant();

        if (v.StartsWith("reception"))
        {
            dept = Department.Reception;
            return true;
        }

        if (v.StartsWith("housekeeping"))
        {
            dept = Department.Housekeeping;
            return true;
        }

        if (v.StartsWith("food") || v.Contains("f&b") || v.Contains("beverage"))
        {
            dept = Department.FoodAndBeverage;
            return true;
        }

        dept = default;
        return false;
    }

    private static HashSet<DayOfWeek> ParseDays(string raw)
    {
        var days = new HashSet<DayOfWeek>();
        var parts = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.All(char.IsLetter) && Enum.TryParse<DayOfWeek>(part, true, out var day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    // The form offers "Morning Shift", "Afternoon Shift", "Night Shift" and "Available Anytime".
    private static HashSet<ShiftType> ParseShifts(string raw)
    {
        string text = raw.ToLowerInvariant();
        var shifts = new HashSet<ShiftType>();

        if (text.Contains("morning")) shifts.Add(ShiftType.Morning);
        if (text.Contains("afternoon")) shifts.Add(ShiftType.Afternoon);
        if (text.Contains("night")) shifts.Add(ShiftType.Night);

        if (text.Contains("anytime"))
        {
            shifts.UnionWith(Enum.GetValues<ShiftType>());
        }

        return shifts;
    }

    private static int ParseOffDays(string raw)
    {
        return int.TryParse(raw.Trim(), out int value) ? Math.Clamp(value, 0, 7) : 0;
    }

    // The form lets people tick several options ("4days;5days;6days"). The largest one is the most they accept.
    private static int ParseMaxConsecutiveDays(string raw, int fallback)
    {
        var numbers = Regex.Matches(raw, @"\d+")
            .Select(m => int.Parse(m.Value))
            .ToList();

        return numbers.Count == 0 ? fallback : Math.Clamp(numbers.Max(), 1, 7);
    }
}
