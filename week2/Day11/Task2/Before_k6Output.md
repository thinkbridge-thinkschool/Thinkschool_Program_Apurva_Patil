# Before — k6 Load Test Output (N+1 endpoint)

**Command run:**
```
k6 run k6/before.js
```

**Endpoint tested:** `GET /api/collections/1/with-quotes-slow`

**What this endpoint does:**
Loads the collection, then fetches each quote one by one in a `foreach` loop.
For a collection with 10 items → 11 SQL queries per request.

---

## Results

| Metric | Value |
|---|---|
| p50 (median) | 21.54 ms |
| p99 | 67.58 ms |
| All thresholds passed | ✓ |

---

## Screenshot

![Before k6 Output](Before_k6_Output.png)