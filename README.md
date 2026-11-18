# SmartShiftScheduler

A console application (C# / .NET 8) that builds a weekly shift schedule for hotel staff from the answers of an employee survey.

It does three things:

1. **Reads the survey.** Employees answer a Google Form: department, available days, preferred shifts, wanted days off, shifts they strictly cannot work, and how many days in a row they accept.
2. **Forecasts demand.** For every department and shift it estimates how many people are needed, using a Lagrange interpolation polynomial scaled by the expected hotel occupancy of the weekday.
3. **Builds the schedule.** A deterministic greedy algorithm assigns people to shifts without breaking any hard rule. Shifts that cannot be staffed are **reported**, not hidden.

At the end it prints quality numbers (coverage, workload, fairness, preference match, rule violations), all computed from the finished schedule.

---

## Quick start

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/QG6r/SmartShiftScheduler.git
cd SmartShiftScheduler

dotnet run --project src/SmartShiftScheduler -- data/responses.csv   # run on the sample survey
dotnet test                                                          # 80 unit tests
```

The argument is the path to a Google Forms CSV export. Without an argument the program looks for `data/responses.csv`.
If the file is missing it prints a clear error and exits with code 1.

`data/responses.csv` is a real export: 16 employees in three departments. Worker ids are four-digit numbers, there are no names.

---

## Survey format

The parser reads the columns by position (the order Google Forms uses):

| # | Question | How it is used |
|---|----------|----------------|
| 0 | Timestamp | ignored |
| 1 | Worker ID | identifies the person (kept as text, so `0956` stays `0956`) |
| 2 | Department | `Reception`, `Housekeeping`, `Food & Beverage (F&B)` |
| 3 | Available days | **hard rule**: never scheduled on other days |
| 4 | Preferred shifts | **soft preference** (Morning / Afternoon / Night / Available Anytime) |
| 5 | Days off wanted this week | lowers the weekly shift limit: at most `7 - days off` shifts |
| 6 | Days/times they strictly cannot work (free text) | **hard rule** when it can be understood, see below |
| 7 | Max consecutive working days | **hard rule**; if several options are ticked (`4days;5days;6days`) the largest is used |

Shift hours come from the form: Morning 08:00-16:00, Afternoon 16:00-00:00, Night 00:00-08:00.

**Free-text answers (column 6).** The parser understands day names (`Monday`, `Mondays`, `Weds`), `Weekend`, and the words morning / afternoon / night:

| Answer | Meaning |
|--------|---------|
| `Thursday Night Shift` | only Thursday night is blocked |
| `Monday fully` | all three Monday shifts are blocked |
| `Weekend` | Saturday and Sunday are blocked |
| `Birthday`, `National holidays` | cannot be mapped to a weekly slot, so it is **not applied**; the program prints a warning for it |

---

## How it works

### 1. Demand forecast (`Forecasting/DemandPredictor.cs`)

Each department has a typical staffing profile through the day, given at three control points (08:00, 14:00, 20:00):

| Department | 08:00 | 14:00 | 20:00 |
|------------|:-----:|:-----:|:-----:|
| Reception | 2.2 | 3.2 | 2.2 |
| Housekeeping | 3.2 | 2.2 | 2.2 |
| Food & Beverage | 2.2 | 2.2 | 3.2 |

The profile is multiplied by the expected occupancy (Friday and Saturday 75%, Sunday 90%, other days 85%). Then the
Lagrange polynomial through the three scaled points is evaluated at one hour inside the shift (09:30 for Morning, 15:00 for Afternoon):

$$L(x) = \sum_{i=0}^{n} y_i \prod_{j \neq i} \frac{x - x_j}{x_i - x_j}$$

The result is rounded and limited to **2-3 people** for day shifts. Night shifts always need **1** person.

Honest note: because of the 2-3 limit the forecast has a small effect. With the current parameters it asks for 3 people only for
Reception on Afternoon shifts (Mon-Thu and Sun) and for Housekeeping on Sunday Morning; everything else is 2 (day) or 1 (night).
All the numbers are in one file, so changing the model means editing `DemandPredictor.cs`.

### 2. Scheduling (`Scheduling/ShiftScheduler.cs`)

**Hard rules** (never broken, even to fill a gap):

- the worker is available that day, and has not blocked that exact shift;
- at most one shift per day;
- at most 40 hours a week (5 shifts), and at most `7 - wanted days off` shifts;
- no more consecutive working days than the worker accepts (the week is cyclic: Sunday is followed by Monday);
- recovery after night: no Morning/Afternoon shift on the day after a Night shift.

**Soft rules:** preferred shifts. Pass 1 fills every shift only with workers who prefer it; pass 2 fills what is left with any worker who is allowed to take it.
Inside a pass the person with the fewest shifts so far goes first (workload balancing), then the lowest id. There is no randomness: the same input always gives the same schedule.

**Demand is soft.** If nobody can legally take a shift, it stays short and appears in the "understaffed" summary and as a red row in the table.
The algorithm never fails with "no solution".

### 3. Quality report (`Scheduling/ScheduleReport.cs`, `ScheduleValidator.cs`)

| KPI | Definition |
|-----|------------|
| Coverage | filled staff places / required staff places |
| Average workload | shifts per worker, with the minimum and maximum |
| Fairness index | `1 - (maxShifts - minShifts) / 7` over all workers (below 85% prints a warning) |
| Preference match | share of assigned shifts that the worker prefers |
| Rule violations | counted by `ScheduleValidator`, which checks the finished schedule independently of the scheduler |

---

## Sample run

`dotnet run --project src/SmartShiftScheduler -- data/responses.csv` (excerpt, real output of the bundled survey):

```
[INFO] Responses read from data/responses.csv: 16
[INFO]   Reception: 6 employees
[INFO]   Housekeeping: 3 employees
[INFO]   FoodAndBeverage: 7 employees
[WARN] Worker 7777: "National holidays" is not a weekday or shift, so it was not applied.

 DEPARTMENT: RECEPTION
┌───────────┬───────────┬──────────────────────────────┬──────┐
│ Day       │ Shift     │ Employees                    │ Fill │
├───────────┼───────────┼──────────────────────────────┼──────┤
│ Monday    │ Morning   │ 1526, 3478                   │ 2/2  │
│ Monday    │ Afternoon │ 4023                         │ 1/3  │
│ Monday    │ Night     │ 5662                         │ 1/1  │
│ Tuesday   │ Morning   │ 2896, 3478                   │ 2/2  │
...

UNDERSTAFFED SHIFTS (red rows above)
[!] Reception: 14 of 40 staff places are empty. Hardest days: Sunday (-4), Tuesday (-3), Monday (-2)
[!] Housekeeping: 22 of 36 staff places are empty. Hardest days: Sunday (-5), Saturday (-4), Monday (-3)
[!] FoodAndBeverage: 6 of 35 staff places are empty. Hardest days: Saturday (-3), Sunday (-3)

SCHEDULE QUALITY ANALYSIS (KPIs)
[KPI] Coverage: 62% (69 of 111 staff places filled)
[KPI] Average workload: 4.3 shifts/worker (min 3, max 5)
[KPI] Fairness index: 71% (higher is better)
[KPI] Preference match: 99% of shifts are ones the worker prefers
[KPI] Rule violations: 0 (rule compliance 100%)
```

**How to read it.** The survey has only 16 people, while the forecast needs about 5 people per department per day.
Housekeeping has 3 employees and on Sunday only one of them is available, so the program says so instead of inventing a schedule.
This is the point of the soft demand: the manager sees exactly which shifts are empty and why, and can hire, ask for extra availability or change the demand.

---

## Project structure

```
src/SmartShiftScheduler/
  Program.cs                      entry point: read CSV -> forecast -> schedule -> print
  Domain/                         Department, ShiftType, Worker, ShiftDemand, ShiftAssignment, ...
  Data/
    CsvReader.cs                  small RFC 4180 reader (quotes, commas inside quotes)
    ResponseCsvParser.cs          survey columns -> Worker objects
    UnavailabilityNoteParser.cs   free text "cannot work" -> blocked slots
  Forecasting/DemandPredictor.cs  Lagrange interpolation + occupancy
  Scheduling/
    ShiftScheduler.cs             the greedy scheduler
    ScheduleValidator.cs          independent rule checker
    ScheduleReport.cs             KPIs
  Output/ConsoleVisualizer.cs     tables and KPI output
tests/SmartShiftScheduler.Tests/  xUnit tests (include the real survey file)
data/responses.csv                sample survey export
```

## Tests and CI

`dotnet test` runs 80 tests: the interpolation maths, the CSV reader, the parser on the real survey file, each scheduling rule,
the validator, the KPI formulas, and a full run on the real data (no rule violations, no blocked slot used, same result on every run).

GitHub Actions (`.github/workflows/ci.yml`) restores, builds, runs the tests and then runs the program on `data/responses.csv`,
checking that the output contains the KPI block and `Rule violations: 0`.

---

## Known limitations

- **It is a heuristic, not an optimizer.** The greedy algorithm never breaks a rule, but it does not search for the best possible schedule.
  When people are scarce it fills shifts in order (Monday to Sunday, Morning, Afternoon, Night) and does not give Night priority over a second Morning worker.
- **One typical week.** The schedule repeats weekly; there are no dates, holidays or per-week requests. Answers such as "National holidays" or "Birthday" cannot be used.
- **Departments are separate.** Nobody works outside their own department.
- **The demand model is simple.** Three control points per department and fixed occupancy numbers, not real booking data.
- **Small survey.** With 16 answers the result shows how the method behaves, not a finished staffing plan for a real hotel.
