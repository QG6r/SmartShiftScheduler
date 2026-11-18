using System.Text;
using SmartShiftScheduler;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Some hosts do not allow changing the encoding; the schedule is still readable.
}

string path = args.Length > 0 ? args[0] : Path.Combine("data", "responses.csv");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"[ERROR] Responses file not found: {path}");
    Console.Error.WriteLine("Usage: dotnet run --project src/SmartShiftScheduler -- <path-to-responses.csv>");
    Console.Error.WriteLine("The sample file is data/responses.csv (a Google Forms export).");
    return 1;
}

ConsoleVisualizer.PrintWelcomeMessage();

var parsed = ResponseCsvParser.ParseFile(path);
Console.WriteLine($"[INFO] Responses read from {path}: {parsed.Workers.Count}");

foreach (var dept in Enum.GetValues<Department>())
{
    Console.WriteLine($"[INFO]   {dept}: {parsed.Workers.Count(w => w.Dept == dept)} employees");
}

foreach (var warning in parsed.Warnings)
{
    Console.WriteLine($"[WARN] {warning}");
}

foreach (var worker in parsed.Workers.Where(w => w.IgnoredUnavailabilityNote is not null))
{
    Console.WriteLine($"[WARN] Worker {worker.Id}: \"{worker.IgnoredUnavailabilityNote}\" is not a weekday or shift, so it was not applied.");
}

if (parsed.Workers.Count == 0)
{
    Console.Error.WriteLine("[ERROR] No valid responses found, nothing to schedule.");
    return 1;
}

var settings = new SchedulerSettings();
var departments = parsed.Workers.Select(w => w.Dept).Distinct().OrderBy(d => d).ToList();
var demands = DemandPredictor.BuildWeeklyDemand(departments);

var result = new ShiftScheduler(settings).Build(parsed.Workers, demands);

Console.WriteLine();
ConsoleVisualizer.PrintSchedule(result, demands);
ConsoleVisualizer.PrintCoverageGaps(result, demands);
ConsoleVisualizer.PrintReport(ScheduleReport.Create(result, parsed.Workers, demands, settings));

if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

return 0;
