# Day 10 · Task 2 — Query Translation + Projections

## What this project demonstrates

| Demo | File | Concept |
|---|---|---|
| SQL logging setup | `AppDbContext.cs` | `LogTo` + `EnableSensitiveDataLogging` |
| Full entity vs projection | `SqlLoggingDemo.cs` | `.Select(p => new Dto{})` narrows the SQL SELECT list |
| WHERE + projection combined | `SqlLoggingDemo.cs` | Both pushed to a single SQL statement |
| Client-side eval trap | `ClientSideEvalDemo.cs` | `.AsEnumerable()` silently fetches all rows |

Database: `EFCoreDemoDay10` (LocalDB) — 10 000 `Product` rows seeded by Day 10 Task 1.

---

## 1. SQL Logging

```csharp
builder
    .LogTo(
        Console.WriteLine,
        new[] { DbLoggerCategory.Database.Command.Name },
        LogLevel.Information)
    .EnableSensitiveDataLogging();
```

- `DbLoggerCategory.Database.Command` filters to SQL command events only — no EF internals noise.
- `EnableSensitiveDataLogging` shows the actual parameter values (e.g. `@p = 3`) instead of `?`.
- **Development only** — never enable `SensitiveDataLogging` in production (leaks PII/secrets).

---

## 2. Projection — narrow the SQL SELECT list

### Before — full entity (all 4 columns fetched)

```csharp
ctx.Products.Take(3).ToList();
```

**Actual EF log output:**
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (28ms) [Parameters=[@p='3'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(@p) [p].[Id], [p].[Name], [p].[Price], [p].[Stock]
      FROM [Products] AS [p]
```

All 4 columns — `Id`, `Name`, `Price`, `Stock` — cross the wire even if the caller only needs the name.

### After — projection to DTO (only 2 columns fetched)

```csharp
ctx.Products
   .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
   .Take(3)
   .ToList();
```

**Actual EF log output:**
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (2ms) [Parameters=[@p='3'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(@p) [p].[Id], [p].[Name]
      FROM [Products] AS [p]
```

`Price` and `Stock` are absent from the SELECT list — never fetched, never allocated, never sent over the wire.
On wide tables (many columns, large strings, binary blobs) this bandwidth and allocation difference is measurable.

### With WHERE + projection — one SQL, no extra round-trip

```csharp
ctx.Products
   .Where(p => p.Price > 900m)
   .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
   .ToList();
```

**Actual EF log output:**
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (2ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT [p].[Id], [p].[Name]
      FROM [Products] AS [p]
      WHERE [p].[Price] > 900.0
```

Both the `WHERE` and the narrow `SELECT` are compiled into a single SQL statement — no extra round-trip.

---

## 3. Client-side evaluation — the `.AsEnumerable()` silent trap

This is the most dangerous pattern because **no exception is thrown**.

### The bug

```csharp
ctx.Products
   .AsNoTracking()
   .AsEnumerable()              // ← shifts evaluation boundary to C#
   .Where(p => p.Price < 5m)   // ← runs in C#, NOT in SQL
   .Take(10)                    // ← runs in C#, NOT in SQL
   .ToList();
```

**Actual EF log output (the caught bug):**
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT [p].[Id], [p].[Name], [p].[Price], [p].[Stock]
      FROM [Products] AS [p]
```

No `WHERE`. No `TOP`. All 10 000 rows crossed the network; C# then filtered down to 10.
No exception was raised — this is what makes it dangerous in production.

### How to detect it

Read the logged SQL. A bare `SELECT … FROM [table]` with no `WHERE` and no `TOP` when
your code has `.Where()` and `.Take()` is the signature of accidental client-side evaluation.

### The fix

```csharp
ctx.Products
   .AsNoTracking()
   .Where(p => p.Price < 5m)   // ← stays IQueryable → SQL WHERE
   .Take(10)                    // ← stays IQueryable → SQL TOP(10)
   .ToList();
```

**Actual EF log output (after fix):**
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (2ms) [Parameters=[@p='10'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(@p) [p].[Id], [p].[Name], [p].[Price], [p].[Stock]
      FROM [Products] AS [p]
      WHERE [p].[Price] < 5.0
```

`WHERE` and `TOP` are both present. Only the 10 matching rows were transferred.

---

## Screenshots

See the `Screenshots/` folder for the captured terminal output showing:

1. Full entity SQL vs projection SQL (column difference visible)
2. BAD `.AsEnumerable()` SQL — bare SELECT, no WHERE, no TOP
3. GOOD fixed SQL — WHERE + TOP present

---

## How to run

```bash
dotnet run -c Release
```

> The project connects to `EFCoreDemoDay10` on `(localdb)\mssqllocaldb`.
> Run Day 10 Task 1 first to seed the 10 000 product rows.
