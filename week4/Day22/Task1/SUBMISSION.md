# Day 22 — Resilience with Polly

## Pipeline Overview

Five-layer `ResiliencePipeline` attached to `ExternalQuoteClient` (typed `HttpClient`).
Outermost → innermost order:

```
Request
  → Total Timeout    (15 s overall budget across all retries)
  → Bulkhead         (max 10 concurrent, queue 5)
  → Circuit Breaker  (trip after 3 failures / 10 s window, break 30 s)
  → Per-Attempt Timeout (3 s per attempt)
  → Retry            (3 retries, exponential backoff + jitter)
    → SlowQuoteService GET /external/quote
```

NuGet package used: `Microsoft.Extensions.Http.Resilience` (Polly v8).
`Polly.Extensions.Http` (v7) was explicitly rejected.

---

## Circuit Breaker State Diagram

```
            3 failures in 10 s window
  CLOSED ──────────────────────────────► OPEN
    ▲                                      │
    │                                      │ wait 30 s
    │                                      ▼
    │                 1 test call      HALF-OPEN
    └──── success ◄────────────────────────┘
         (OnCircuitClosed)     (OnCircuitHalfOpened)

Trigger conditions:
  CLOSED  → OPEN      : failure rate threshold reached (3 failures / 10 s)
  OPEN    → HALF-OPEN : break duration elapsed (30 s)
  HALF-OPEN → CLOSED  : test call returns 200
  HALF-OPEN → OPEN    : test call fails → another 30 s break
```

---

## Scenario A — Happy Path

### Command
```powershell
curl.exe "http://localhost:5255/resilience-test?mode=ok"
```

### curl Response
```json
{"mode":"ok","result":"{\"quote\":\"The only way to do great work is to love what you do. — Steve Jobs\"}"}
```

### QuotesApi Logs
```
info: Program[0]
      resilience-test: starting call with ok
info: QuotesApi.Resilience.ExternalQuoteClient[0]
      Calling SlowQuoteService with ok
info: System.Net.Http.HttpClient.ExternalQuoteClient.LogicalHandler[100]
      Start processing HTTP request GET http://localhost:5001/external/quote?*
info: System.Net.Http.HttpClient.ExternalQuoteClient.ClientHandler[100]
      Sending HTTP request GET http://localhost:5001/external/quote?*
info: System.Net.Http.HttpClient.ExternalQuoteClient.ClientHandler[101]
      Received HTTP response headers after 120.2656ms - 200
info: Polly[3]
      Execution attempt. Source: 'ExternalQuoteClient-external-quotes//Retry',
      Result: '200', Handled: 'False', Attempt: '0', Execution Time: 120.7592ms
info: System.Net.Http.HttpClient.ExternalQuoteClient.LogicalHandler[101]
      End processing HTTP request after 121.7885ms - 200
```

### What This Proves
- `Attempt: '0'` — first call succeeded, no retries fired
- `Handled: 'False'` — Polly saw no failure, circuit stays Closed
- Response time 120 ms, well within 3 s per-attempt timeout

---

## Scenario B — Retry with Backoff (Flaky)

### Command
```powershell
curl.exe "http://localhost:5255/resilience-test?mode=flaky"
```

### curl Response
```json
{"mode":"flaky","result":"{\"quote\":\"Flaky call #3 succeeded!\"}"}
```

### What This Proves
- SlowQuoteService `mode=flaky` fails the first N calls then succeeds
- The response `"Flaky call #3 succeeded!"` confirms Polly retried and eventually got through
- Retry with exponential backoff + jitter fired between attempts
- Retry behaviour is proven in detail in Scenario D logs (same RETRY log lines, same mechanism)

---

## Scenario C — Timeout Fires

### Command
```powershell
curl.exe "http://localhost:5255/resilience-test?mode=slow"
```

### curl Response
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "External service unavailable after retries: TimeoutRejectedException"
}
```

### QuotesApi Logs
```
warn: QuotesApi.Resilience[0]
      RETRY: attempt 3 after 1530ms — reason: The operation didn't complete
      within the allowed timeout of '00:00:03'.
info: System.Net.Http.HttpClient.ExternalQuoteClient.ClientHandler[100]
      Sending HTTP request GET http://localhost:5001/external/quote?*
info: Polly[3]
      Execution attempt. Source: 'ExternalQuoteClient-external-quotes//Retry',
      Result: 'The operation was canceled.', Handled: 'False',
      Attempt: '3', Execution Time: 2739.9398ms
fail: Polly[0]
      Resilience event occurred. EventName: 'OnTimeout',
      Source: 'ExternalQuoteClient-external-quotes//Timeout',
      Operation Key: '', Result: ''
warn: QuotesApi.Resilience[0]
      TOTAL TIMEOUT: overall budget (15s) exceeded, aborting all retries
fail: Program[0]
      resilience-test: all retry attempts exhausted for slow
      Polly.Timeout.TimeoutRejectedException: The operation didn't complete
      within the allowed timeout of '00:00:15'.
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5255/resilience-test?mode=slow
      - 503 - application/problem+json 15008.9232ms
```

### What This Proves
- Per-attempt timeout fires at 3 s each attempt
- After 3 retries (4 total attempts × ~3 s) the 15 s total timeout budget is exhausted
- `OnTimeout` Polly event fires
- `TOTAL TIMEOUT: overall budget (15s) exceeded` — outer total-timeout layer cuts off remaining retries
- Final response is 503 with `TimeoutRejectedException`

---

## Scenario D — Circuit Breaker Opens

### Commands
```powershell
# Three calls to exhaust failures and trip the breaker
curl.exe "http://localhost:5255/resilience-test?mode=fail"
curl.exe "http://localhost:5255/resilience-test?mode=fail"
curl.exe "http://localhost:5255/resilience-test?mode=fail"
# Fourth call — rejected instantly by open circuit
curl.exe "http://localhost:5255/resilience-test?mode=fail"
```

### curl Responses
Calls 1–3 (retried then exhausted):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "External service unavailable after retries: HttpRequestException"
}
```

Call 4 (circuit open — rejected instantly):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Circuit breaker is open — all calls blocked for 30 s."
}
```

### QuotesApi Logs — Call 3 (breaker trips on this call)
```
info: Program[0]
      resilience-test: starting call with fail
info: QuotesApi.Resilience.ExternalQuoteClient[0]
      Calling SlowQuoteService with fail
info: System.Net.Http.HttpClient.ExternalQuoteClient.ClientHandler[101]
      Received HTTP response headers after 1.0126ms - 500
warn: Polly[3]
      Execution attempt. Source: 'ExternalQuoteClient-external-quotes//Retry',
      Result: '500', Handled: 'True', Attempt: '0', Execution Time: 1.1782ms
warn: Polly[0]
      Resilience event occurred. EventName: 'OnRetry', Result: '500'
warn: QuotesApi.Resilience[0]
      RETRY: attempt 1 after 1007ms — reason: InternalServerError
warn: QuotesApi.Resilience[0]
      RETRY: attempt 2 after 497ms — reason: InternalServerError
warn: QuotesApi.Resilience[0]
      RETRY: attempt 3 after 2913ms — reason: InternalServerError
fail: Polly[3]
      Execution attempt. Source: 'ExternalQuoteClient-external-quotes//Retry',
      Result: '500', Handled: 'True', Attempt: '3', Execution Time: 0.9064ms
fail: Polly[0]
      Resilience event occurred. EventName: 'OnCircuitOpened',
      Source: 'ExternalQuoteClient-external-quotes//CircuitBreaker',
      Result: '500'
fail: QuotesApi.Resilience[0]
      CIRCUIT BREAKER: opened — blocking all calls for 30s
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished - 503 - application/problem+json 4439.4966ms
```

### QuotesApi Logs — Call 4 (rejected instantly, no retries)
```
info: Program[0]
      resilience-test: starting call with fail
info: QuotesApi.Resilience.ExternalQuoteClient[0]
      Calling SlowQuoteService with fail
info: System.Net.Http.HttpClient.ExternalQuoteClient.LogicalHandler[100]
      Start processing HTTP request GET http://localhost:5001/external/quote?*
fail: Program[0]
      resilience-test: circuit open — fail rejected immediately:
      The circuit is now open and is not allowing calls.
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished - 503 - application/problem+json 1.9249ms
```

### What This Proves
- Calls 1–3 each triggered retries (RETRY: attempt 1, 2, 3) before exhausting
- On call 3's final failure: `OnCircuitOpened` fires → `CIRCUIT BREAKER: opened — blocking all calls for 30s`
- Call 4 completed in **1.9 ms** — rejected immediately without attempting the network call
- Compare: calls 1–3 took ~4000 ms each (retrying). Call 4 took 1.9 ms. The breaker is working.

---

## Scenario E — Half-Open Recovery

### Command
```powershell
# Run immediately after Scenario D — waits 31 s for break duration to elapse
Start-Sleep 31; curl.exe "http://localhost:5255/resilience-test?mode=ok"
```

### curl Response
```json
{"mode":"ok","result":"{\"quote\":\"The only way to do great work is to love what you do. — Steve Jobs\"}"}
```

### QuotesApi Logs
```
info: Program[0]
      resilience-test: starting call with ok
info: QuotesApi.Resilience.ExternalQuoteClient[0]
      Calling SlowQuoteService with ok
info: System.Net.Http.HttpClient.ExternalQuoteClient.LogicalHandler[100]
      Start processing HTTP request GET http://localhost:5001/external/quote?*
warn: Polly[0]
      Resilience event occurred. EventName: 'OnCircuitHalfOpened',
      Source: 'ExternalQuoteClient-external-quotes//CircuitBreaker',
      Operation Key: '', Result: ''
warn: QuotesApi.Resilience[0]
      CIRCUIT BREAKER: half-open — testing recovery
info: System.Net.Http.HttpClient.ExternalQuoteClient.ClientHandler[101]
      Received HTTP response headers after 120.4262ms - 200
info: Polly[3]
      Execution attempt. Source: 'ExternalQuoteClient-external-quotes//Retry',
      Result: '200', Handled: 'False', Attempt: '0', Execution Time: 120.6092ms
info: Polly[0]
      Resilience event occurred. EventName: 'OnCircuitClosed',
      Source: 'ExternalQuoteClient-external-quotes//CircuitBreaker',
      Result: '200'
info: QuotesApi.Resilience[0]
      CIRCUIT BREAKER: closed — normal traffic resuming
info: System.Net.Http.HttpClient.ExternalQuoteClient.LogicalHandler[101]
      End processing HTTP request after 133.4343ms - 200
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished - 200 - application/json 134.7389ms
```

### What This Proves
- After 30 s break, breaker transitions to Half-Open: `OnCircuitHalfOpened` fires
- One test call is allowed through
- Test call returns 200 → `OnCircuitClosed` fires → `CIRCUIT BREAKER: closed — normal traffic resuming`
- Full self-healing cycle completed without any manual intervention
- Total response time 134 ms — back to normal

---

## Summary

| Scenario | Result | Key Evidence |
|---|---|---|
| A — Happy path | ✅ 200 | Attempt 0, no retries, 120 ms |
| B — Flaky retry | ✅ 200 after retries | "Flaky call #3 succeeded!" |
| C — Timeout | ✅ 503 after 15 s | TOTAL TIMEOUT, TimeoutRejectedException |
| D — Breaker opens | ✅ 503 in 1.9 ms | CIRCUIT BREAKER: opened, instant rejection |
| E — Recovery | ✅ 200 after 31 s | half-open → closed, self-healed |

POST endpoints (`POST /api/quotes`) are never wrapped by this pipeline.
Retry is safe here because `GET /external/quote` is idempotent — calling it
multiple times returns the same quote without side effects.
