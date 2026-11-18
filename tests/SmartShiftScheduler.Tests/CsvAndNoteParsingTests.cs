using SmartShiftScheduler;

namespace SmartShiftScheduler.Tests;

public class CsvReaderTests
{
    [Fact]
    public void Comma_inside_quotes_does_not_split_the_field()
    {
        var rows = CsvReader.Parse("a,\"Morning Shift (e.g., 08:00 - 16:00)\",c\n");

        Assert.Single(rows);
        Assert.Equal(new[] { "a", "Morning Shift (e.g., 08:00 - 16:00)", "c" }, rows[0]);
    }

    [Fact]
    public void Doubled_quote_is_an_escaped_quote()
    {
        var rows = CsvReader.Parse("\"say \"\"hi\"\"\",x");

        Assert.Equal(new[] { "say \"hi\"", "x" }, rows[0]);
    }

    [Fact]
    public void Windows_line_endings_and_blank_lines_are_handled()
    {
        var rows = CsvReader.Parse("a,b\r\n\r\nc,d\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "c", "d" }, rows[1]);
    }

    [Fact]
    public void Line_break_inside_quotes_stays_in_the_field()
    {
        var rows = CsvReader.Parse("\"line1\nline2\",z");

        Assert.Single(rows);
        Assert.Equal("line1\nline2", rows[0][0]);
    }

    [Fact]
    public void Empty_fields_are_kept()
    {
        var rows = CsvReader.Parse("a,,c");

        Assert.Equal(new[] { "a", "", "c" }, rows[0]);
    }
}

public class UnavailabilityNoteParserTests
{
    [Theory]
    [InlineData("Thursday Night Shift", 1, true)]
    [InlineData("Monday Afternoon Shifts", 1, true)]
    [InlineData("Monday fully", 3, true)]
    [InlineData("Mondays", 3, true)]
    [InlineData("Only Tuesday", 3, true)]
    [InlineData("Weekend", 6, true)]
    [InlineData("Monday fully and Tuesday", 6, true)]
    [InlineData("Mery Christmas and Weds", 3, true)]
    [InlineData("No any", 0, true)]
    [InlineData("no any ", 0, true)]
    [InlineData("", 0, true)]
    [InlineData(null, 0, true)]
    [InlineData("National holidays ", 0, false)]
    [InlineData("Birthday ", 0, false)]
    [InlineData("New Year’s days", 0, false)]
    public void Real_answers_from_the_form_are_mapped_to_slots(string? text, int expectedSlots, bool understood)
    {
        var result = UnavailabilityNoteParser.Parse(text);

        Assert.Equal(expectedSlots, result.Slots.Count);
        Assert.Equal(understood, result.Understood);
    }

    [Fact]
    public void Day_and_shift_together_block_only_that_shift()
    {
        var result = UnavailabilityNoteParser.Parse("Thursday Night Shift");

        Assert.Contains(new Slot(DayOfWeek.Thursday, ShiftType.Night), result.Slots);
    }

    [Fact]
    public void Shift_without_a_day_blocks_that_shift_every_day()
    {
        var result = UnavailabilityNoteParser.Parse("night shifts");

        Assert.Equal(7, result.Slots.Count);
        Assert.All(result.Slots, s => Assert.Equal(ShiftType.Night, s.Shift));
    }
}
