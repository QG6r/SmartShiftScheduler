namespace SmartShiftScheduler;

public static class ConsoleVisualizer
{
    private const int DayWidth = 10;
    private const int ShiftWidth = 9;
    private const int EmployeesWidth = 30;
    private const int FillWidth = 5;

    public static void PrintWelcomeMessage()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("  SMART SHIFT SCHEDULER");
        Console.WriteLine("  Weekly staff schedule built from employee survey answers");
        Console.WriteLine(new string('=', 60));
        Console.ResetColor();
    }

    public static void PrintSchedule(ScheduleResult result, IReadOnlyList<ShiftDemand> demands)
    {
        foreach (var dept in demands.Select(d => d.Dept).Distinct())
        {
            PrintDepartmentHeader(dept.ToString());

            Console.WriteLine($"┌{Bar(DayWidth)}┬{Bar(ShiftWidth)}┬{Bar(EmployeesWidth)}┬{Bar(FillWidth)}┐");
            Console.WriteLine($"│ {"Day".PadRight(DayWidth - 2)} │ {"Shift".PadRight(ShiftWidth - 2)} │ {"Employees".PadRight(EmployeesWidth - 2)} │ {"Fill".PadRight(FillWidth - 2)} │");
            Console.WriteLine($"├{Bar(DayWidth)}┼{Bar(ShiftWidth)}┼{Bar(EmployeesWidth)}┼{Bar(FillWidth)}┤");

            foreach (var demand in demands.Where(d => d.Dept == dept))
            {
                var people = result.Assignments
                    .Where(a => a.Dept == dept && a.Day == demand.Day && a.Shift == demand.Shift)
                    .Select(a => a.Worker.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();

                string employees = people.Count == 0 ? "-" : string.Join(", ", people);
                string fill = $"{people.Count}/{demand.Required}";
                bool short_ = people.Count < demand.Required;

                if (short_) Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine(
                    $"│ {demand.Day.ToString().PadRight(DayWidth - 2)} │ {demand.Shift.ToString().PadRight(ShiftWidth - 2)} │ " +
                    $"{employees.PadRight(EmployeesWidth - 2)} │ {fill.PadRight(FillWidth - 2)} │");

                if (short_) Console.ResetColor();
            }

            Console.WriteLine($"└{Bar(DayWidth)}┴{Bar(ShiftWidth)}┴{Bar(EmployeesWidth)}┴{Bar(FillWidth)}┘\n");
        }
    }

    public static void PrintCoverageGaps(ScheduleResult result, IReadOnlyList<ShiftDemand> demands)
    {
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("UNDERSTAFFED SHIFTS (red rows above)");
        Console.WriteLine(new string('=', 60));

        if (result.Shortages.Count == 0)
        {
            Console.WriteLine("[OK] Every shift is fully staffed.");
            return;
        }

        foreach (var dept in demands.Select(d => d.Dept).Distinct())
        {
            int required = demands.Where(d => d.Dept == dept).Sum(d => d.Required);
            int missing = result.Shortages.Where(s => s.Dept == dept).Sum(s => s.Missing);

            var worstDays = result.Shortages
                .Where(s => s.Dept == dept)
                .GroupBy(s => s.Day)
                .Select(g => new { Day = g.Key, Missing = g.Sum(s => s.Missing) })
                .OrderByDescending(x => x.Missing)
                .ThenBy(x => Array.IndexOf(Week.Days, x.Day))
                .Take(3)
                .ToList();

            string worst = worstDays.Count == 0
                ? "none"
                : string.Join(", ", worstDays.Select(x => $"{x.Day} (-{x.Missing})"));

            Console.WriteLine($"[!] {dept}: {missing} of {required} staff places are empty. Hardest days: {worst}");
        }
    }

    public static void PrintReport(ScheduleReport report)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("SCHEDULE QUALITY ANALYSIS (KPIs)");
        Console.WriteLine(new string('=', 60));

        Console.WriteLine($"[KPI] Coverage: {Percent(report.Coverage)} ({report.FilledSlots} of {report.RequiredSlots} staff places filled)");
        Console.WriteLine($"[KPI] Average workload: {report.AverageShiftsPerWorker:F1} shifts/worker (min {report.MinShifts}, max {report.MaxShifts})");
        Console.WriteLine($"[KPI] Fairness index: {Percent(report.FairnessIndex)} (higher is better)");
        Console.WriteLine($"[KPI] Preference match: {Percent(report.PreferenceMatch)} of shifts are ones the worker prefers");
        Console.WriteLine($"[KPI] Rule violations: {report.RuleViolations} (rule compliance {Percent(report.RuleCompliance)})");

        if (report.FairnessIndex < 0.85)
        {
            Console.WriteLine("[!] Warning: workload distribution is uneven.");
        }
        else
        {
            Console.WriteLine("[OK] Workload is balanced across the team.");
        }
    }

    private static string Percent(double value) => $"{value * 100:F0}%";

    private static string Bar(int width) => new string('─', width);

    private static void PrintDepartmentHeader(string deptName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($" DEPARTMENT: {deptName.ToUpperInvariant()}");
        Console.WriteLine(new string('=', 50));
        Console.ResetColor();
    }
}
