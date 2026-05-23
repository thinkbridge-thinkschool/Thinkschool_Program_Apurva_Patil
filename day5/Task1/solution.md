# Day 3 Task 3 — Quotes API: End-to-End Auth Lock-Down

## PR URL

**Repository:** https://github.com/thinkbridge-thinkschool/apurva-day2-di-lifetimes  
**Commit hash:** `051f902`  
**Branch / PR:** `main`

---

## PR Review Status

**"this is solid"**

All 5 integration tests pass locally and the CI workflow is committed to the repo.  
Once the GitHub Actions run completes you can find it at:

> `https://github.com/thinkbridge-thinkschool/apurva-day2-di-lifetimes/actions`

---

## What Was Built

### Dual Authentication Scheme

`Program.cs` registers a **"smart" policy scheme** that inspects the token issuer before
delegating to the real validator:

| Token issuer                            | Routed to       |
|-----------------------------------------|-----------------|
| `login.microsoftonline.com` / `sts.windows.net` | `Entra` (Azure AD) |
| anything else (e.g. `"your-app"`)       | `InternalJwt` (symmetric HMAC-SHA256) |

```csharp
// Program.cs — policy scheme selector
options.ForwardDefaultSelector = context =>
{
    var jwt = new JwtSecurityToken(token);
    return jwt.Issuer.Contains("login.microsoftonline.com") ||
           jwt.Issuer.Contains("sts.windows.net")
        ? "Entra"
        : "InternalJwt";
};
```

The signing key is read from `Jwt:SigningKey` in configuration — never hardcoded.

---

### Refresh-Token Rotation with Reuse Detection

**`Services/TokenService.cs`** tracks every refresh token in an in-memory dictionary
keyed by token value. Every token carries a `FamilyId` that groups a rotation chain.

Rotation flow (`POST /auth/refresh`):

```
Client sends RT-1
  → RT-1 exists, not yet used
  → mark RT-1.IsUsed = true
  → issue (AT-2, RT-2) in the same family
  → return to client

If client (or attacker) sends RT-1 again:
  → RT-1.IsUsed == true  ← REUSE DETECTED
  → revoke every token with FamilyId == RT-1.FamilyId
  → return 401 — even RT-2 is now dead
```

Key properties:
- **Rotation**: each refresh produces a fresh pair; the old refresh is marked used.
- **Reuse detection**: a second use of a consumed token kills the entire chain.
- **Revocation scope**: `IsRevoked` flags every sibling; subsequent rotations fail even with the "new" token.

---

### Policies on Every Mutating Endpoint

| Endpoint                            | Policy                  | Checked via                  |
|-------------------------------------|-------------------------|------------------------------|
| `GET /quotes`                       | Any authenticated user  | Class-level `[Authorize]`    |
| `PUT /quotes/{id}`                  | `can-edit-quotes`       | `scope == "quotes.write"`    |
| `DELETE /quotes/{id}/owner/{ownerId}` | `can-delete-own-quote`| `sub == ownerId` (custom handler) |

---

### Integration Tests — All 5 Pass

Located in `IntegrationTests/QuotesApiTests.cs`, using `WebApplicationFactory<Program>`
to run the real pipeline in-process with a test JWT signing key.

| Test | Scenario | Expected |
|------|----------|----------|
| `Anonymous_GetQuotes_Returns401` | No Authorization header | **401** |
| `Authenticated_NoWriteScope_PutQuote_Returns403` | Valid token, no `scope: quotes.write` | **403** |
| `Authenticated_WithWriteScope_PutQuote_Returns200` | Valid token + correct scope | **200** |
| `ExpiredToken_GetQuotes_Returns401` | Token expired 1 hour ago (`ClockSkew=0`) | **401** |
| `RevokedRefreshChain_Returns401` | Replay attack detected, chain revoked | **401** |

Local run output:

```
Passed QuotesApiTests.Anonymous_GetQuotes_Returns401
Passed QuotesApiTests.Authenticated_NoWriteScope_PutQuote_Returns403
Passed QuotesApiTests.Authenticated_WithWriteScope_PutQuote_Returns200
Passed QuotesApiTests.ExpiredToken_GetQuotes_Returns401
Passed QuotesApiTests.RevokedRefreshChain_Returns401

Total tests: 5  |  Passed: 5  |  Total time: 4.45 s
```

---

### CI Workflow

`.github/workflows/ci.yml` triggers on every push / PR to `main`:

```yaml
- name: Run integration tests
  run: dotnet test Day3/Task3/IntegrationTests/Day3.IntegrationTests.csproj
       --configuration Release --verbosity normal
```

---

## Files Changed / Created

```
.github/workflows/ci.yml                          ← new: GitHub Actions CI
Day3/Task3/Program.cs                             ← updated: config-driven key, TokenService, public partial Program
Day3/Task3/appsettings.json                       ← updated: added Jwt:SigningKey
Day3/Task3/Models/RefreshToken.cs                 ← new: refresh token entity
Day3/Task3/Services/TokenService.cs               ← new: JWT issuance + refresh rotation
Day3/Task3/controllers/AuthController.cs          ← new: POST /auth/token, POST /auth/refresh
Day3/Task3/IntegrationTests/
  Day3.IntegrationTests.csproj                    ← new: test project
  TestWebAppFactory.cs                            ← new: in-process test host
  QuotesApiTests.cs                               ← new: 5 integration tests
Day3/Task3/Day3.csproj                            ← updated: exclude test folders from web compilation
```

---

## What I Learned This Session

**The thing that clicked:** Refresh-token reuse detection requires tracking state server-side — the token itself cannot self-report whether it has been used. The family-revocation pattern (all siblings die when one is replayed) is elegant because it turns a stolen token into a detectable anomaly rather than a silent privilege escalation.

**The idea I'll keep:** `WebApplicationFactory<Program>` with `PostConfigure<JwtBearerOptions>` lets you swap out the signing key and kill network calls (Entra) without touching production code — the entire real pipeline runs in test, including middleware order, policy evaluation, and controller routing.

---

## What Would Break This

1. **Shared static `_store` in `TokenService`**: The singleton store is in-process memory. Any horizontal scale-out (second pod/process) would give each instance its own store, breaking rotation — pod A issues RT-1, pod B can't find it. Fix: move the store to Redis or a database with row-level locking.

2. **No clock sync between issuer and validator**: If the server clock drifts, `ClockSkew` hides the problem in production but your test with `ClockSkew=0` would start failing mysteriously. Fix: NTP discipline + explicit `ClockSkew` configuration in every environment.

3. **TOCTOU on rotation**: Between checking `rt.IsUsed` and flipping it, two concurrent requests with the same token could both pass the guard (the lock helps here, but not across processes — see point 1).

4. **`/auth/token` has no credential check**: It accepts any `UserId` string. In production this endpoint must validate a password / client secret / PKCE code before issuing tokens. As-is, any caller can impersonate any userId.

5. **Refresh token entropy**: `Guid.NewGuid().ToString("N")` is 122 bits of randomness — acceptable, but a 256-bit CSPRNG output (e.g. `RandomNumberGenerator.GetBytes(32)`) is the gold standard for opaque bearer tokens.

---

## Day 5 Task 1 - Diagnose a Slow Endpoint with Traces

### What I changed

- Added an intentionally inefficient EF Core endpoint: `GET /quotes/slow-nplusone`.
- Implemented N+1 behavior by fetching 50 IDs, then querying each quote in a loop.
- Generated traffic and inspected Jaeger traces.
- Replaced the N+1 logic with a single set-based query (same endpoint route).
- Re-ran traffic and verified the improved trace profile.

### Trace evidence (before fix)

- Service: `QuotesApi`
- Endpoint: `GET /quotes/slow-nplusone`
- Typical duration: ~`634-736 ms`
- Example trace ID: `c8af04ebe10fef12919d95466f1fb3b1`
- Span count: `53`
- Dominant span: `quotes-nplusone-load` (~`631 ms`)
- Symptom: many repeated EF database spans (`main`) caused by one query per row.

### 100-word diagnosis note

This trace showed the slow span was quotes-nplusone-load because the endpoint first fetched IDs, then executed one EF query per ID, creating N+1 round trips. Jaeger showed 53 spans and request times around 634-736 ms. Most time was spent in repeated database spans, not in API framework overhead. I'd fix it by replacing the loop with one set-based EF query that returns the same projection in a single SQL command, keeping ordering and limit. After the fix, new traces showed only 3 spans and quotes-optimized-load around 14 ms, with endpoint latency around 18-40 ms after warm-up, confirming the slow span was removed.

### Trace evidence (after fix)

- Service: `QuotesApi`
- Endpoint: `GET /quotes/slow-nplusone` (same route, optimized logic)
- Typical duration after warm-up: ~`18-40 ms`
- Example trace ID: `3cd754319a19fbb2a063c135a033e6c4`
- Span count: `3`
- Dominant span now: `quotes-optimized-load` (~`14 ms`)
- Result: N+1 fan-out is gone.

### Bonus: KQL to find similar slow endpoints in Application Insights

```kusto
requests
| where timestamp > ago(24h)
| where success == true
| where cloud_RoleName == "QuotesApi"
| summarize
    Count = count(),
    P50Ms = percentile(duration, 50),
    P95Ms = percentile(duration, 95),
    P99Ms = percentile(duration, 99),
    MaxMs = max(duration)
  by name, operation_Name
| where P95Ms > 500ms
| order by P95Ms desc
```

```kusto
dependencies
| where timestamp > ago(24h)
| where cloud_RoleName == "QuotesApi"
| where type == "SQL"
| summarize SqlCalls = count(), AvgSqlMs = avg(duration), P95SqlMs = percentile(duration, 95)
  by operation_Name
| order by SqlCalls desc
```
