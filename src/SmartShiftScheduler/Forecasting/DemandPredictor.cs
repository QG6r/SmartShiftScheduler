namespace SmartShiftScheduler;

/// <summary>
/// Estimates how many people each department needs on each shift.
///
/// Idea: a department has a typical staffing profile over the day (three control points: 08:00, 14:00, 20:00).
/// A Lagrange polynomial through those points gives the need at any hour; the profile is scaled by the expected
/// hotel occupancy of the weekday. The result is rounded and clamped to 2..3 people for day shifts; nights always need 1.
/// </summary>
public static class DemandPredictor
{
    public const int MinDayStaff = 2;
    public const int MaxDayStaff = 3;
    public const int NightStaff = 1;

    private static readonly double[] ControlHours = { 8, 14, 20 };

    // The shift is evaluated at one representative hour inside it.
    private const double MorningHour = 9.5;
    private const double AfternoonHour = 15.0;

    /// <summary>Lagrange interpolating polynomial through the points (x[i], y[i]) evaluated at <paramref name="targetHour"/>.</summary>
    public static double InterpolateLagrange(double targetHour, double[] x, double[] y)
    {
        double result = 0;
        int n = x.Length;

        for (int i = 0; i < n; i++)
        {
            double term = y[i];
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    term *= (targetHour - x[j]) / (x[i] - x[j]);
                }
            }

            result += term;
        }

        return result;
    }

    public static int GetRequiredStaff(DayOfWeek day, ShiftType shift, Department dept)
    {
        if (shift == ShiftType.Night)
        {
            return NightStaff;
        }

        double occupancy = GetOccupancyForecast(day);

        double[] baseStaff = dept switch
        {
            Department.Reception => new[] { 2.2, 3.2, 2.2 },
            Department.Housekeeping => new[] { 3.2, 2.2, 2.2 },
            Department.FoodAndBeverage => new[] { 2.2, 2.2, 3.2 },
            _ => new[] { 2.0, 2.0, 2.0 }
        };

        double[] scaled = baseStaff.Select(n => n * occupancy).ToArray();
        double hour = shift == ShiftType.Morning ? MorningHour : AfternoonHour;
        double prediction = InterpolateLagrange(hour, ControlHours, scaled);

        return (int)Math.Clamp(Math.Round(prediction), MinDayStaff, MaxDayStaff);
    }

    /// <summary>Expected share of rooms occupied: Friday/Saturday 75%, Sunday 90%, other days 85%.</summary>
    public static double GetOccupancyForecast(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Friday or DayOfWeek.Saturday => 0.75,
            DayOfWeek.Sunday => 0.90,
            _ => 0.85
        };
    }

    /// <summary>The full weekly demand table (Monday first) for the given departments.</summary>
    public static List<ShiftDemand> BuildWeeklyDemand(IEnumerable<Department> departments)
    {
        var demands = new List<ShiftDemand>();

        foreach (var dept in departments)
        {
            foreach (var day in Week.Days)
            {
                foreach (var shift in Enum.GetValues<ShiftType>())
                {
                    demands.Add(new ShiftDemand(day, shift, dept, GetRequiredStaff(day, shift, dept)));
                }
            }
        }

        return demands;
    }
}
