# Day 8 — Clustered vs Non-Clustered Indexes

## Table
Table: `Orders` with 100,000 rows generated using a CTE.

## Indexes Created
| Index | Type | Column | Include |
|---|---|---|---|
| CIX_Orders_OrderId | Clustered | OrderId | — |
| IX_Orders_CustomerId | Non-Clustered | CustomerId | OrderDate, TotalAmount |
| IX_Orders_OrderDate | Non-Clustered | OrderDate | CustomerId, TotalAmount |

## Results (SET STATISTICS IO ON)

| Query | Before (Heap) | After Indexes |
|---|---|---|
| OrderId = 50000 | 778 reads | 1 read |
| CustomerId = 1234 | 778 reads | 2 reads |
| OrderDate range | 778 reads | 3 reads |
| INSERT | 1 read | 1 read |

## What I Learned

- Without indexes, SQL Server reads all 778 pages every time — no shortcut.
- A clustered index physically sorts the data, so a PK lookup needs just 1 page.
- A covering NCI stores extra columns inside the index — no need to go back to the main table.
- Every index added speeds up reads but adds maintenance cost on writes.
