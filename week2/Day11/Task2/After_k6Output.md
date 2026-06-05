# After — k6 Load Test Output (IN-clause endpoint)

**Command run:**
```
k6 run k6/after.js
```

**Endpoint tested:** `GET /api/collections/1/with-quotes-fast`

**What this endpoint does:**
Loads the collection, collects all quote IDs, then fetches all quotes in a single
`WHERE Id IN (...)` query. Always 2 SQL queries per request regardless of collection size.

**Index in use:** `IX_Quotes_Author` — covering index on `Quotes(Author)` with
`INCLUDE (Text, CreatedAt)`. SQL Server resolves author-filtered lookups entirely
from the index leaf page, no key lookup back to the clustered index.

---

## Results

| Metric | Value |
|---|---|
| p50 (median) | 4.73 ms |
| p99 | 25.93 ms |
| All thresholds passed | ✓ |

---

## Screenshot

![After k6 Output](After_k6Output.png)