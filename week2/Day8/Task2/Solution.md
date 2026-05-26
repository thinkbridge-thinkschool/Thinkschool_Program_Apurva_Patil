# Day 8 Task 2 — Covering Indexes + Included Columns

## Setup

Table: `Orders` — 100,000 rows, **clustered index on `OrderId`**.  
Target query: fetch `OrderId`, `OrderDate`, `TotalAmount` for a given `CustomerId`.

---

## What Is a Key Lookup?

When SQL Server uses a non-clustered index (NCI) to satisfy a query, the NCI leaf pages store:
- The **index key column(s)**
- The **clustering key** (as a row locator)

If the query also needs columns that are **not** in the NCI, SQL Server must follow the row locator back into the clustered index to fetch them. This extra trip is called a **Key Lookup**, and it happens once per matching row.

---

## Step A — Produce a Key Lookup

```sql
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_NoInclude
ON Orders(CustomerId);
```

NCI leaf contains: `CustomerId`, `OrderId` (row locator).  
Query asks for `OrderDate` and `TotalAmount` — **not in the NCI**.

### Execution Plan (Step A)

```
Index Seek (IX_Orders_CustomerId_NoInclude)
    → Key Lookup (CIX_Orders_OrderId)    ← extra round-trip per row
        → Nested Loops
```

`SET STATISTICS IO` output (approx):

| Metric | Value |
|--------|-------|
| Logical reads | ~22 |
| Key lookups per row | 1 |

---

## Step B — Eliminate with a Covering Index

```sql
DROP INDEX IX_Orders_CustomerId_NoInclude ON Orders;

CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Covering
ON Orders(CustomerId)
INCLUDE (OrderDate, TotalAmount);
```

NCI leaf now contains: `CustomerId` (key) + `OrderId` (row locator) + `OrderDate` + `TotalAmount` (included).  
The query can be satisfied **entirely from the index** — no Key Lookup needed.

### Execution Plan (Step B)

```
Index Seek (IX_Orders_CustomerId_Covering)   ← single operator, query done
```

`SET STATISTICS IO` output (approx):

| Metric | Value |
|--------|-------|
| Logical reads | ~3 |
| Key lookups per row | 0 |

---

## Before vs After

| | Step A (no INCLUDE) | Step B (covering index) |
|---|---|---|
| Plan operators | Index Seek + Key Lookup + Nested Loops | Index Seek only |
| Logical reads | ~22 | ~3 |
| Key Lookup | Present | Eliminated |

---

## Key Takeaways

- A **key lookup** is the hidden cost of a non-clustered index that doesn't cover the query — one random I/O per result row.
- `INCLUDE` columns live only in the NCI **leaf pages** (not the inner B-tree nodes), so they add storage cost but not index depth.
- A **covering index** = the index key(s) + all columns the query needs, making the index self-sufficient.
- Only include columns that are genuinely needed by frequently-run queries — unnecessary INCLUDE columns waste index storage and slow down writes.
