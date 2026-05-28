using QueryTranslationDemo;

Console.WriteLine("""
─────────────────────────────────────────────────────────────────────────────
 Day 10 Task 2 — Query Translation + Projections
 Database: EFCoreDemoDay10 (seeded by Day10/Task1 — 10 000 Products)
─────────────────────────────────────────────────────────────────────────────
""");

Console.WriteLine("① SQL logging");
SqlLoggingDemo.Run();

Console.WriteLine("② Projection demos");
ProjectionDemo.Run();
ProjectionDemo.RunFiltered();

Console.WriteLine("③ Client-side evaluation");
ClientEvalDemo.Run();

Console.WriteLine("""
─────────────────────────────────────────────────────────────────────────────
 Key takeaways
  1. LogTo(…, [Database.Command], Information) shows exactly what SQL EF sends.
  2. .Select(p => new Dto{…}) removes unused columns from the SELECT list.
  3. .Where() before .ToList() keeps the filter server-side.
  4. .AsEnumerable() mid-query silently fetches all rows — no exception raised.
─────────────────────────────────────────────────────────────────────────────
""");
