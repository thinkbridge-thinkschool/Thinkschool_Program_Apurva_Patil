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
