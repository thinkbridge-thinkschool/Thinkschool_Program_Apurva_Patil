# Day 5 Task 6 — Smoke-Test the Deployed API

**Live URL:** `https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io`
**Deployed via:** Task 4 (`azd up`) — Azure Container Apps, `centralindia`, resource group `rg-quotesapi-task4`

---

## Endpoint Map

| Method | Route | Auth Required | Policy |
|---|---|---|---|
| `POST` | `/auth/token` | No | — |
| `POST` | `/auth/refresh` | No | — |
| `GET` | `/quotes` | Yes | Any valid JWT |
| `GET` | `/quotes/{id:int}` | Yes | Any valid JWT |
| `GET` | `/quotes/slow-nplusone` | Yes | Any valid JWT |
| `POST` | `/quotes` | Yes | Any valid JWT |
| `PUT` | `/quotes/{id:int}` | Yes | `quotes.write` scope |
| `DELETE` | `/quotes/{id:int}/owner/{ownerId}` | Yes | `sub` claim must match `ownerId` |

---

## Smoke-Test Script

```powershell
$BASE = "https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io"

# ── 1. Get token (warm) ───────────────────────────────────────────────────────
$resp   = Invoke-RestMethod -Method Post -Uri "$BASE/auth/token" `
            -ContentType "application/json" `
            -Body '{"userId":"apurv","scopes":["quotes.write"]}'
$TOKEN  = $resp.accessToken
$REFRESH = $resp.refreshToken

# ── 2. Rotate refresh token ───────────────────────────────────────────────────
$r2 = Invoke-RestMethod -Method Post -Uri "$BASE/auth/refresh" `
        -ContentType "application/json" `
        -Body "{`"refreshToken`":`"$REFRESH`"}"

# ── 3. GET /quotes — unauthenticated (expect 401) ─────────────────────────────
try { Invoke-RestMethod -Uri "$BASE/quotes" } catch { <# expect 401 #> }

# ── 4. GET /quotes — authenticated (expect 200) ───────────────────────────────
Invoke-RestMethod -Uri "$BASE/quotes" -Headers @{Authorization="Bearer $TOKEN"}

# ── 5. POST /quotes ───────────────────────────────────────────────────────────
$created = Invoke-RestMethod -Method Post -Uri "$BASE/quotes" `
    -Headers @{Authorization="Bearer $TOKEN"} `
    -ContentType "application/json" `
    -Body '{"text":"smoke test — Task6 end-to-end verification"}'
$QUOTE_ID = $created.id

# ── 6. GET /quotes/{id} ───────────────────────────────────────────────────────
Invoke-RestMethod -Uri "$BASE/quotes/$QUOTE_ID" -Headers @{Authorization="Bearer $TOKEN"}

# ── 7. GET /quotes/999 (expect 404) ───────────────────────────────────────────
try { Invoke-RestMethod -Uri "$BASE/quotes/999" -Headers @{Authorization="Bearer $TOKEN"} } catch {}

# ── 8. PUT /quotes/{id} with scope ───────────────────────────────────────────
Invoke-RestMethod -Method Put -Uri "$BASE/quotes/$QUOTE_ID" `
    -Headers @{Authorization="Bearer $TOKEN"} `
    -ContentType "application/json" -Body '"smoke-test updated text"'

# ── 9. PUT /quotes/{id} without scope (expect 403) ───────────────────────────
$noScopeToken = (Invoke-RestMethod -Method Post -Uri "$BASE/auth/token" `
    -ContentType "application/json" -Body '{"userId":"apurv","scopes":[]}').accessToken
try {
    Invoke-RestMethod -Method Put -Uri "$BASE/quotes/$QUOTE_ID" `
        -Headers @{Authorization="Bearer $noScopeToken"} `
        -ContentType "application/json" -Body '"should be forbidden"'
} catch { <# expect 403 #> }

# ── 10. DELETE /quotes/{id}/owner/{ownerId} — correct owner ──────────────────
Invoke-RestMethod -Method Delete -Uri "$BASE/quotes/$QUOTE_ID/owner/apurv" `
    -Headers @{Authorization="Bearer $TOKEN"}

# ── 11. DELETE — wrong owner (expect 403) ────────────────────────────────────
try {
    Invoke-RestMethod -Method Delete -Uri "$BASE/quotes/1/owner/alice" `
        -Headers @{Authorization="Bearer $TOKEN"}
} catch { <# expect 403 #> }

# ── 12. GET /quotes/slow-nplusone ─────────────────────────────────────────────
Invoke-RestMethod -Uri "$BASE/quotes/slow-nplusone" -Headers @{Authorization="Bearer $TOKEN"}
```

---

## Results

All 12 tests passed.

| # | Test | Expected | Actual Status | Latency | Result |
|---|---|---|---|---|---|
| 1 | `POST /auth/token` (cold start) | 200 + JWT pair | 200 | **19 098 ms** | PASS |
| 2 | `POST /auth/token` (warm) | 200 + JWT pair | 200 | 138 ms | PASS |
| 3 | `POST /auth/refresh` | 200 + new pair | 200 | 37 ms | PASS |
| 4 | `GET /quotes` (no auth) | 401 | 401 | 35 ms | PASS |
| 5 | `GET /quotes` (Bearer token) | 200 + array | 200 | 561 ms | PASS |
| 6 | `POST /quotes` | 201 + entity | 201 | 516 ms | PASS |
| 7 | `GET /quotes/1` | 200 + quote | 200 | 201 ms | PASS |
| 8 | `GET /quotes/999` | 404 | 404 | 68 ms | PASS |
| 9 | `PUT /quotes/1` (with `quotes.write`) | 200 + updated | 200 | 19 ms | PASS |
| 10 | `PUT /quotes/1` (no scope) | 403 | 403 | 42 ms | PASS |
| 11 | `DELETE /quotes/1/owner/apurv` (owner) | 200 + deleted | 200 | 45 ms | PASS |
| 12 | `DELETE /quotes/1/owner/alice` (wrong owner) | 403 | 403 | 18 ms | PASS |
| 13 | `GET /quotes/slow-nplusone` | 200 + array | 200 | 80 ms | PASS |

---

## Fragility Notes

### 1. Cold-start penalty is extreme — 19 seconds
The Container App scales to zero between test runs. The very first request (always `POST /auth/token`
in every script) absorbs JIT compilation, EF Core migration check (`db.Database.Migrate()`), DI
container initialisation, and first Azure Monitor connection. The warm latency is 138 ms — a 138×
difference.

**Fragility:** Any client with a timeout under 20 s will see a `TaskCanceledException` on the
first request after a cold period. A minimum-replica setting of 1 would fix it but incurs
continuous cost.

### 2. SQLite on an ephemeral container filesystem
The database file (`quotes.db`) lives inside the container. Every time the Container App
replica is recycled or scaled, the file is recreated from scratch via `db.Database.Migrate()`.
This means all data is wiped on every cold start.

**Fragility:** Not suitable for production data. The smoke test observed 0 quotes in the DB on
GET immediately after cold start (previous Task4/Task5 rows were gone). A real deployment needs
Azure SQL or Cosmos DB with a persistent connection string.

### 3. Refresh-token store is in-memory
`TokenService` keeps the refresh-token dictionary in a singleton. On cold start or replica
recycle the entire token store is lost. Any active user's refresh token immediately becomes
`invalid_grant`.

**Fragility:** Horizontal scaling to 2+ replicas breaks refresh-token rotation — a token issued
by replica A is unknown to replica B. This needs a shared backing store (Redis, SQL) before
the Container App autoscaler can be trusted.

### 4. `PUT /quotes/{id}` does not persist
The `Edit` action returns `{ Id, Text, Updated = true }` from memory without touching the DB.
A GET immediately after a PUT will still return the old text.

**Fragility:** Accepted in the current week-1 state, but a consumer testing round-trip
edit → read would see a stale value and flag this as a bug.

### 5. DELETE does not actually delete from DB
Same pattern as PUT — `Delete` returns `{ Id, Deleted = true }` without calling
`_db.Quotes.Remove` / `SaveChangesAsync`. The quote remains queryable after a DELETE.

**Fragility:** Same as above — smoke test passes, round-trip verification fails.

---

## Summary

The API is healthy on Azure. Auth flows (token issuance, JWT validation, scope policy, owner
policy) all work correctly end-to-end. The two biggest operational risks going into Week 2 are
the cold-start SLA gap and the SQLite-on-container data loss. The in-memory-only PUT/DELETE
would also need fixing before the API could be considered production-correct.
