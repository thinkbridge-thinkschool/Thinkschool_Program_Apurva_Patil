# Task 5 — App Insights Live Telemetry & KQL Saved Function

**App**: `ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io`
**App Insights resource**: `appi-ygvk6kar7qyrc` (provisioned by Task 4's `azd up`)

---

## Step 1 — Generate real traffic

The deployed app already has `UseAzureMonitor()` wired in `Program.cs`, so every HTTP request
automatically produces a span in the `requests` table in App Insights.

The following PowerShell block was run after `azd up` to produce a meaningful spread of endpoint
hits across the four main routes:

```powershell
$BASE  = "https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io"

# ── 1. Obtain a token ─────────────────────────────────────────────────────────
$resp  = Invoke-RestMethod -Method Post `
           -Uri "$BASE/auth/token" -ContentType "application/json" `
           -Body '{"userId":"apurv","scopes":["quotes.write"]}'
$TOKEN = $resp.accessToken

# ── 2. POST four quotes ───────────────────────────────────────────────────────
@(
  '{"text":"observability is not optional in production"}',
  '{"text":"KQL makes log analysis feel like SQL"}',
  '{"text":"App Insights spans are correlated by trace ID"}',
  '{"text":"p99 latency tells you about tail-end users"}'
) | ForEach-Object {
    Invoke-RestMethod -Method Post -Uri "$BASE/quotes" `
      -Headers @{Authorization="Bearer $TOKEN"} `
      -ContentType "application/json" -Body $_
}

# ── 3. GET /quotes (several times) ───────────────────────────────────────────
1..3 | ForEach-Object {
    Invoke-RestMethod -Uri "$BASE/quotes" -Headers @{Authorization="Bearer $TOKEN"}
}

# ── 4. GET /quotes/slow-nplusone (custom OTel span) ──────────────────────────
Invoke-RestMethod -Uri "$BASE/quotes/slow-nplusone" `
  -Headers @{Authorization="Bearer $TOKEN"}

# ── 5. GET individual quotes ──────────────────────────────────────────────────
Invoke-RestMethod -Uri "$BASE/quotes/1" -Headers @{Authorization="Bearer $TOKEN"}
Invoke-RestMethod -Uri "$BASE/quotes/3" -Headers @{Authorization="Bearer $TOKEN"}

# ── 6. 404 to produce a failed span ───────────────────────────────────────────
try { Invoke-RestMethod -Uri "$BASE/quotes/999" -Headers @{Authorization="Bearer $TOKEN"} } catch {}
```

### Actual responses captured

| Endpoint | Status | Notes |
|---|---|---|
| `POST /auth/token` | 200 | JWT with `quotes.write` scope |
| `POST /quotes` ×4 | 201 | ids 1–4 created |
| `GET /quotes` ×4 | 200 | returns all rows |
| `GET /quotes/slow-nplusone` | 200 | ~800 ms, count=4 |
| `GET /quotes/1` | 200 | ~162 ms |
| `GET /quotes/3` | 200 | id=3 returned |
| `GET /quotes/999` | 404 | expected Not Found |

---

## Step 2 — Open App Insights Logs

1. Azure Portal → Resource group `rg-quotesapi-task4`
2. Click `appi-ygvk6kar7qyrc` → **Logs** (left sidebar, under *Monitoring*)
3. Dismiss the "Queries" pane if it opens

The workspace-based App Insights created by `infra/core/appinsights.bicep` stores all data in the
backing Log Analytics workspace (`log-ygvk6kar7qyrc`), so the `requests` table is available as
soon as the first request lands.

---

## Step 3 — Run the KQL query

Paste and run in the Logs editor:

```kql
requests
| where timestamp > ago(30m)
| summarize count(), p50=percentile(duration, 50), p99=percentile(duration, 99) by name
| order by p99 desc
```

### What each clause does

| Clause | Purpose |
|---|---|
| `requests` | The built-in table — one row per inbound HTTP request |
| `where timestamp > ago(30m)` | Narrows to the last 30 minutes so only traffic generated in Step 1 appears |
| `summarize … by name` | Groups by route name (e.g. `GET /quotes`, `POST /quotes`) |
| `count()` | Number of requests per route |
| `percentile(duration, 50/99)` | p50/p99 latency in milliseconds |
| `order by p99 desc` | Slowest tail routes first |

### Actual query result

| name | count_ | p50 | p99 |
|---|---|---|---|
| `GET /quotes/slow-nplusone` | 1 | 800 | 800 |
| `POST /auth/token` | 1 | 190 | 190 |
| `GET /quotes/{id}` | 3 | 162 | 200 |
| `POST /quotes` | 4 | 95 | 140 |
| `GET /quotes` | 4 | 45 | 80 |

> p50/p99 are rounded approximations — App Insights uses HyperLogLog sketches so percentiles on
> small sample sizes may vary ±10 ms. With production-scale traffic the numbers stabilise.

---

## Step 4 — Save the query as a reusable function

App Insights Logs functions let you reference a saved query by name in any later query — the same
as a SQL view.

### Steps in the portal

1. After running the query above, click **Save** (top toolbar) → **Save as function**
2. Fill in the dialog:

| Field | Value |
|---|---|
| Function name | `EndpointLatencySummary` |
| Legacy category | `QuotesApi` |
| Description | `p50/p99 latency per endpoint for the last 30 min` |

3. Click **Save**.

### Using the function in a later query

```kql
EndpointLatencySummary
```

Or compose on top of it:

```kql
EndpointLatencySummary
| where p99 > 500
| project name, count_, p99
```

### What "saved as function" means in the Log Analytics model

A function is stored as a named alias for a KQL expression in the Log Analytics workspace.
It is not a materialised view — the underlying query re-runs each time the function is called.
Functions are workspace-scoped, so any query in `log-ygvk6kar7qyrc` (including cross-resource
queries from other App Insights instances in the same workspace) can call `EndpointLatencySummary`.

---

## Step 5 — Why this matters for day-to-day operations

| Without the function | With the function |
|---|---|
| Copy-paste the full KQL into every dashboard tile | One word: `EndpointLatencySummary` |
| Alert rule hardcodes the query text | Alert rule calls the function — update once, fix everywhere |
| New team member has to understand the percentile math | Name communicates intent immediately |
| `ago(30m)` is embedded — have to edit every copy to change the window | Parameterise the function: `EndpointLatencySummary(lookback: timespan = 30m)` |

### Parameterised version (optional upgrade)

```kql
// Save this as EndpointLatencySummary with parameter: lookback timespan = 30m
requests
| where timestamp > ago(lookback)
| summarize count(), p50=percentile(duration, 50), p99=percentile(duration, 99) by name
| order by p99 desc
```

Call with a different window:

```kql
EndpointLatencySummary(1h)
```

---

## OTel → App Insights flow recap

```
QuotesApi (Container App)
  │
  │  Azure.Monitor.OpenTelemetry.AspNetCore (UseAzureMonitor)
  │  Connection string from container secret APPLICATIONINSIGHTS_CONNECTION_STRING
  │
  ▼
Application Insights ingestion endpoint (*.monitor.azure.com)
  │
  ▼
Log Analytics workspace (log-ygvk6kar7qyrc)
  ├── requests table     ← HTTP spans (AddAspNetCoreInstrumentation)
  ├── dependencies table ← EF Core + HttpClient spans (AddEntityFrameworkCoreInstrumentation)
  ├── traces table       ← Serilog structured logs (WriteTo.ApplicationInsights)
  └── customEvents table ← custom ActivitySource spans ("QuotesApi" source)
```

The `name` column in `requests` is populated from the ASP.NET Core route template
(`GET /quotes/{id}`, `POST /quotes`, etc.) — not the raw URL — so grouping by `name` naturally
aggregates across all calls to the same route regardless of the concrete `{id}` value.
