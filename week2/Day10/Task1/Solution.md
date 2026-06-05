# Day 10 — EF Core Change Tracker + AsNoTracking

## What the change tracker does on every tracked read

For each row EF Core materialises when tracking is on, it:
1. Allocates an `EntityEntry<T>` wrapper around the entity
2. Takes an **original-values snapshot** (a copy of every property value)
3. Inserts the entry into an **identity map** keyed by primary key

Steps 2 and 3 are what make `SaveChanges()` work without you telling EF which rows changed —
it compares current values against the snapshot to detect modifications.
`AsNoTracking()` skips all three steps entirely.

---

## Identity Resolution

The identity map means two separate queries for the same PK **inside the same `DbContext` scope**
return the **same object reference** when tracking is on.

```csharp
// Tracked — identity map returns the SAME object both times
var a = ctx.Products.First(p => p.Id == 1);
var b = ctx.Products.First(p => p.Id == 1);

Console.WriteLine(ReferenceEquals(a, b)); // True
a.Name = "CHANGED";
Console.WriteLine(b.Name);               // "CHANGED" — same object

// Untracked — each call materialises a NEW object
var c = ctx.Products.AsNoTracking().First(p => p.Id == 1);
var d = ctx.Products.AsNoTracking().First(p => p.Id == 1);

Console.WriteLine(ReferenceEquals(c, d)); // False
c.Name = "CHANGED";
Console.WriteLine(d.Name);               // original value — different objects
```

---

## Tracking State

The change tracker exposes each entity's lifecycle state.
A plain property mutation is detected automatically by snapshot comparison:

```csharp
var products = ctx.Products.Take(5).ToList();
// All 5 entries are Unchanged

products[0].Price = 9_999m;
ctx.ChangeTracker.DetectChanges();
// products[0] is now Modified; the other 4 remain Unchanged
// SaveChanges() would issue exactly 1 UPDATE statement
```

Untracked loads leave zero entries in the tracker:

```csharp
_ = ctx.Products.AsNoTracking().Take(5).ToList();
Console.WriteLine(ctx.ChangeTracker.Entries().Count()); // 0
```

---

## The Two Query Variants

```csharp
// Variant 1 — tracked (default)
// EF allocates EntityEntry + snapshot for each of the 10 000 rows.
int WithTracking()
{
    using var ctx = AppDbContext.Create();
    return ctx.Products.ToList().Count;
}

// Variant 2 — AsNoTracking
// EF materialises each row but skips snapshot + identity-map registration.
int WithoutTracking()
{
    using var ctx = AppDbContext.Create();
    return ctx.Products.AsNoTracking().ToList().Count;
}
```

---

## Benchmark Results (10 000 rows, Release build, .NET 10, Intel i5-1155G7)

```
| Method          | Mean      | Allocated | Ratio | Alloc Ratio |
|---------------- |----------:|----------:|------:|------------:|
| WithTracking    | 24.35 ms  |   8.47 MB |  1.00 |        1.00 |
| WithoutTracking |  8.97 ms  |   3.09 MB |  0.37 |        0.36 |
```

`AsNoTracking` is **2.7× faster** and allocates **2.7× less memory**.

The extra ~5.4 MB per read comes entirely from the snapshot copies and `EntityEntry`
objects the change tracker keeps alive — one per row, all held in Gen2 until the
`DbContext` is disposed.

---

## When you would NOT use AsNoTracking

Do **not** use `AsNoTracking` when the same entity PK may be loaded more than once
inside a single unit of work — without the identity map each load produces a separate
object, so mutations on one copy are invisible through the other and `SaveChanges`
will not see them.
