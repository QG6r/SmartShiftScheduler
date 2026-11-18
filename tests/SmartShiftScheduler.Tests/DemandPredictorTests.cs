using SmartShiftScheduler;

namespace SmartShiftScheduler.Tests;

public class DemandPredictorTests
{
    [Fact]
    public void Lagrange_polynomial_passes_through_its_control_points()
    {
        double[] x = { 8, 14, 20 };
        double[] y = { 2.0, 3.5, 1.0 };

        for (int i = 0; i < x.Length; i++)
        {
            Assert.Equal(y[i], DemandPredictor.InterpolateLagrange(x[i], x, y), 9);
        }
    }

    [Fact]
    public void Lagrange_polynomial_reproduces_a_parabola_exactly()
    {
        double[] x = { 8, 14, 20 };
        double[] y = { 64, 196, 400 }; // y = x^2

        Assert.Equal(100.0, DemandPredictor.InterpolateLagrange(10, x, y), 9);
        Assert.Equal(225.0, DemandPredictor.InterpolateLagrange(15, x, y), 9);
    }

    [Fact]
    public void Night_shift_always_needs_exactly_one_person()
    {
        foreach (var dept in Enum.GetValues<Department>())
        {
            foreach (var day in Week.Days)
            {
                Assert.Equal(1, DemandPredictor.GetRequiredStaff(day, ShiftType.Night, dept));
            }
        }
    }

    [Fact]
    public void Day_shifts_need_between_two_and_three_people()
    {
        foreach (var dept in Enum.GetValues<Department>())
        {
            foreach (var day in Week.Days)
            {
                foreach (var shift in new[] { ShiftType.Morning, ShiftType.Afternoon })
                {
                    int staff = DemandPredictor.GetRequiredStaff(day, shift, dept);
                    Assert.InRange(staff, 2, 3);
                }
            }
        }
    }

    // Expected values were worked out by hand: nodes = base profile x occupancy, Lagrange at 9.5 (morning) / 15.0 (afternoon).
    // Reception, Monday: 2.696 at 15:00 -> 3.   Reception, Friday: 2.379 -> 2.   Housekeeping, Sunday: 2.571 at 9:30 -> 3.
    [Theory]
    [InlineData(Department.Reception, DayOfWeek.Monday, ShiftType.Afternoon, 3)]
    [InlineData(Department.Reception, DayOfWeek.Sunday, ShiftType.Afternoon, 3)]
    [InlineData(Department.Reception, DayOfWeek.Friday, ShiftType.Afternoon, 2)]
    [InlineData(Department.Reception, DayOfWeek.Monday, ShiftType.Morning, 2)]
    [InlineData(Department.Housekeeping, DayOfWeek.Sunday, ShiftType.Morning, 3)]
    [InlineData(Department.Housekeeping, DayOfWeek.Monday, ShiftType.Morning, 2)]
    [InlineData(Department.FoodAndBeverage, DayOfWeek.Sunday, ShiftType.Afternoon, 2)]
    public void Required_staff_matches_hand_checked_values(Department dept, DayOfWeek day, ShiftType shift, int expected)
    {
        Assert.Equal(expected, DemandPredictor.GetRequiredStaff(day, shift, dept));
    }

    [Fact]
    public void Weekly_demand_has_21_rows_per_department_and_starts_on_monday()
    {
        var demands = DemandPredictor.BuildWeeklyDemand(new[] { Department.Reception, Department.Housekeeping });

        Assert.Equal(42, demands.Count);
        Assert.Equal(21, demands.Count(d => d.Dept == Department.Reception));
        Assert.Equal(DayOfWeek.Monday, demands[0].Day);
        Assert.Equal(ShiftType.Morning, demands[0].Shift);
        Assert.Equal(DayOfWeek.Sunday, demands[20].Day);
    }
}
