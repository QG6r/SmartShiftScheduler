using SmartShiftScheduler;

namespace SmartShiftScheduler.Tests;

internal static class TestHelpers
{
    public static string ResponsesCsvPath => Path.Combine(AppContext.BaseDirectory, "data", "responses.csv");

    public static Worker MakeWorker(
        string id,
        Department dept = Department.Reception,
        IEnumerable<DayOfWeek>? days = null,
        IEnumerable<ShiftType>? preferred = null,
        int daysOff = 0,
        int maxConsecutive = 7,
        IEnumerable<Slot>? blocked = null)
    {
        return new Worker
        {
            Id = id,
            Dept = dept,
            AvailableDays = new HashSet<DayOfWeek>(days ?? Week.Days),
            PreferredShifts = new HashSet<ShiftType>(preferred ?? Enum.GetValues<ShiftType>()),
            RequestedOffDays = daysOff,
            MaxConsecutiveDays = maxConsecutive,
            UnavailableSlots = new HashSet<Slot>(blocked ?? Array.Empty<Slot>())
        };
    }

    public static ShiftDemand Demand(DayOfWeek day, ShiftType shift, int required, Department dept = Department.Reception)
    {
        return new ShiftDemand(day, shift, dept, required);
    }

    public static ScheduleResult Run(IReadOnlyList<Worker> workers, params ShiftDemand[] demands)
    {
        return new ShiftScheduler().Build(workers, demands);
    }
}
