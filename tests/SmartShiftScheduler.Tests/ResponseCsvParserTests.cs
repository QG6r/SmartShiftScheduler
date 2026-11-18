using SmartShiftScheduler;

namespace SmartShiftScheduler.Tests;

public class ResponseCsvParserTests
{
    private const string Header =
        "\"Timestamp\",\"id\",\"dept\",\"days\",\"shifts\",\"off\",\"cannot\",\"consecutive\"\n";

    private static ParseResult ParseRealFile() => ResponseCsvParser.ParseFile(TestHelpers.ResponsesCsvPath);

    private static Worker Find(ParseResult parsed, string id) => parsed.Workers.Single(w => w.Id == id);

    [Fact]
    public void Real_export_gives_16_workers_in_three_departments_without_warnings()
    {
        var parsed = ParseRealFile();

        Assert.Equal(16, parsed.Workers.Count);
        Assert.Equal(6, parsed.Workers.Count(w => w.Dept == Department.Reception));
        Assert.Equal(3, parsed.Workers.Count(w => w.Dept == Department.Housekeeping));
        Assert.Equal(7, parsed.Workers.Count(w => w.Dept == Department.FoodAndBeverage));
        Assert.Empty(parsed.Warnings);
    }

    [Fact]
    public void Leading_zeros_in_worker_ids_are_kept()
    {
        Assert.Contains(ParseRealFile().Workers, w => w.Id == "0956");
    }

    [Fact]
    public void Afternoon_preference_is_recognised()
    {
        var worker = Find(ParseRealFile(), "4023");

        Assert.Equal(new[] { ShiftType.Afternoon }, worker.PreferredShifts.ToArray());
        Assert.Equal(6, worker.AvailableDays.Count);
        Assert.DoesNotContain(DayOfWeek.Tuesday, worker.AvailableDays);
    }

    [Fact]
    public void Available_anytime_means_all_three_shifts()
    {
        var worker = Find(ParseRealFile(), "2896");

        Assert.Equal(3, worker.PreferredShifts.Count);
    }

    [Fact]
    public void Largest_ticked_consecutive_option_is_used()
    {
        var worker = Find(ParseRealFile(), "3478"); // "4days;5days;6days"

        Assert.Equal(6, worker.MaxConsecutiveDays);
        Assert.Equal(2, worker.RequestedOffDays);
        Assert.Contains(new Slot(DayOfWeek.Thursday, ShiftType.Night), worker.UnavailableSlots);
    }

    [Fact]
    public void Worker_who_works_every_day_is_parsed_fully()
    {
        var worker = Find(ParseRealFile(), "7230");

        Assert.Equal(7, worker.AvailableDays.Count);
        Assert.Equal(7, worker.MaxConsecutiveDays);
        Assert.Equal(0, worker.RequestedOffDays);
        Assert.Empty(worker.UnavailableSlots);
        Assert.Null(worker.IgnoredUnavailabilityNote);
    }

    [Fact]
    public void Weekend_answer_blocks_saturday_and_sunday()
    {
        var worker = Find(ParseRealFile(), "5662");

        Assert.Equal(6, worker.UnavailableSlots.Count);
        Assert.All(worker.UnavailableSlots, s => Assert.True(s.Day == DayOfWeek.Saturday || s.Day == DayOfWeek.Sunday));
    }

    [Fact]
    public void Answers_that_are_not_weekly_slots_are_kept_as_ignored_notes()
    {
        var ignored = ParseRealFile().Workers
            .Where(w => w.IgnoredUnavailabilityNote is not null)
            .Select(w => w.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "1526", "3450", "5044", "7777" }, ignored);
    }

    [Fact]
    public void Unknown_department_is_skipped_with_a_warning()
    {
        var parsed = ResponseCsvParser.Parse(Header + "\"t\",\"1\",\"Security\",\"Monday\",\"Night Shift\",\"1\",\"No any\",\"5days\"\n");

        Assert.Empty(parsed.Workers);
        Assert.Single(parsed.Warnings);
        Assert.Contains("Security", parsed.Warnings[0]);
    }

    [Fact]
    public void Duplicate_id_keeps_the_first_answer_and_warns()
    {
        string row1 = "\"t\",\"1\",\"Reception\",\"Monday\",\"Night Shift\",\"1\",\"No any\",\"5days\"\n";
        string row2 = "\"t\",\"1\",\"Housekeeping\",\"Tuesday\",\"Night Shift\",\"1\",\"No any\",\"5days\"\n";

        var parsed = ResponseCsvParser.Parse(Header + row1 + row2);

        Assert.Single(parsed.Workers);
        Assert.Equal(Department.Reception, parsed.Workers[0].Dept);
        Assert.Single(parsed.Warnings);
    }

    [Fact]
    public void Row_with_too_few_columns_is_skipped_with_a_warning()
    {
        var parsed = ResponseCsvParser.Parse(Header + "\"t\",\"1\",\"Reception\"\n");

        Assert.Empty(parsed.Workers);
        Assert.Single(parsed.Warnings);
    }

    [Fact]
    public void Missing_consecutive_answer_falls_back_to_the_default()
    {
        var parsed = ResponseCsvParser.Parse(Header + "\"t\",\"1\",\"Reception\",\"Monday\",\"Night Shift\",\"1\",\"No any\",\"\"\n", 4);

        Assert.Equal(4, parsed.Workers[0].MaxConsecutiveDays);
    }

    [Fact]
    public void Food_and_beverage_label_from_the_form_is_recognised()
    {
        var parsed = ResponseCsvParser.Parse(Header + "\"t\",\"1\",\"Food & Beverage (F&B)\",\"Monday\",\"Night Shift\",\"1\",\"No any\",\"5days\"\n");

        Assert.Equal(Department.FoodAndBeverage, parsed.Workers[0].Dept);
    }
}
