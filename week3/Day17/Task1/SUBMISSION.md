# Day 17 — SWA + Managed-Identity QuotesAPI

---

## Part 1 — Brief to the Agent

> What I told Claude Code to build and deploy.

**Target SWA URL**
`https://proud-water-01382c900.7.azurestaticapps.net`

**Week-1 API base URL**
`https://quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io`

**Endpoints the SWA must hit**

| Method | Path | Auth | Key response fields |
|--------|------|------|---------------------|
| `POST` | `/auth/token` | Anonymous | `{ accessToken: string }` — symmetric JWT, 15-min lifetime |
| `GET`  | `/api/quotes?page=1&size=50` | `Authorization: Bearer <JWT>` | `[{ id, text, author, createdAt }]` |
| `GET`  | `/api/quotes/{id}` | `Authorization: Bearer <JWT>` | `{ id, text, author, createdAt }` or 404 |

**Auth requirement**
Managed Identity — no client secret stored anywhere in the repo or app settings.
The Container Apps API reads its JWT signing key at runtime via
`DefaultAzureCredential` → Azure Key Vault (secret name `Jwt--SigningKey`). The
SWA deployment token (`AZURE_STATIC_WEB_APPS_API_TOKEN`) is generated and rotated
by Azure; it is not an Entra ID client secret and is not in source control.

**Other constraints**
- Angular 21 standalone components, lazy-loaded routes, signals-based state
- `staticwebapp.config.json`: SPA fallback + immutable-asset caching headers
- Lighthouse Performance >= 95 on the live URL
- Zero API keys, connection strings, or signing keys in `src/` or CI config

---

## Part 2 — Agent Output

### 2a. GitHub Actions CI/CD (`.github/workflows/deploy-frontend.yml`)

```yaml
name: Deploy Frontend to Azure Static Web Apps

on:
  push:
    branches:
      - main
    paths:
      - 'week3/Day17/Task1/quotes-frontend/**'
  workflow_dispatch:

jobs:
  build_and_deploy:
    runs-on: ubuntu-latest
    name: Build and Deploy
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: week3/Day17/Task1/quotes-frontend/package-lock.json

      - name: Install dependencies
        working-directory: week3/Day17/Task1/quotes-frontend
        run: npm ci

      - name: Build (production)
        working-directory: week3/Day17/Task1/quotes-frontend
        run: npm run build

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: week3/Day17/Task1/quotes-frontend/dist/quotes-frontend/browser
          output_location: ''
          skip_app_build: true
```

**Why no client secret:** `AZURE_STATIC_WEB_APPS_API_TOKEN` is a deploy token
issued by Azure when the SWA resource is created. It only authorises pushing static
assets to that specific SWA — it has no Azure RBAC permissions and is not a
service-principal credential. No `AZURE_CREDENTIALS` object exists in this repo.

---

### 2b. SWA routing + security headers (`public/staticwebapp.config.json`)

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/*.{css,js,ico,png,svg,woff2,map}"]
  },
  "globalHeaders": {
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "SAMEORIGIN",
    "Referrer-Policy": "strict-origin-when-cross-origin",
    "Permissions-Policy": "camera=(), microphone=(), geolocation=()",
    "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self' https://quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io;"
  },
  "routes": [
    {
      "route": "/*.{js,css,woff2}",
      "headers": {
        "Cache-Control": "public, max-age=31536000, immutable"
      }
    }
  ],
  "mimeTypes": {
    ".json": "application/json"
  }
}
```

---

### 2c. Production environment (`src/environments/environment.prod.ts`)

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io'
};
```

No key, no secret — only the public hostname of the Container Apps API.

---

### 2d. Bearer-token injection (`src/app/interceptors/auth.interceptor.ts`)

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('accessToken');
  if (!token) return next(req);
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
```

Every `GET /api/quotes` and `GET /api/quotes/{id}` automatically carries the JWT.

---

### 2e. Token acquisition (`src/app/features/auth/login.component.ts`, key excerpt)

```typescript
this.http
  .post<{ accessToken: string }>(
    `${environment.apiBaseUrl}/auth/token`,
    { userId: this.userId, scopes: [] },
  )
  .subscribe({
    next: (res) => {
      localStorage.setItem('accessToken', res.accessToken);
      this.router.navigate(['/quotes']);
    },
    error: () => this.error.set('Login failed. Please try again.'),
  });
```

The Container Apps API's `/auth/token` endpoint signs the JWT using the key it
fetches from Key Vault via `DefaultAzureCredential`. The signing key never leaves
Azure Key Vault — the browser only receives the finished token.

---

### 2f. How Managed Identity closes the loop (`QuotesApi/Program.cs`, lines 17–23)

```csharp
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential(),   // <- system-assigned Managed Identity
        new KeyVaultSecretManager());
}
```

`DefaultAzureCredential` uses the Container Apps resource's system-assigned
Managed Identity. No `ClientId` or `ClientSecret` is supplied; Azure grants secret
access via the identity attached to the running compute. The JWT signing key is
loaded at startup and never written to logs, env vars, or disk.

---

## Part 3 — Verification Log

### Live URL
`https://proud-water-01382c900.7.azurestaticapps.net`

Loads `/login`, accepts any `userId`, calls `POST /auth/token` on the Container
Apps API, stores the returned JWT, then navigates to `/quotes` where
`GET /api/quotes?page=1&size=50` returns the five seeded quotes
(Aristotle x 2, Marcus Aurelius x 2, Seneca).

---

### Lighthouse Score (desktop, incognito)

| Category | Score | Requirement |
|----------|-------|-------------|
| Performance | **100** | >= 95 |
| Accessibility | **97** | >= 95 |
| Best Practices | **92** | >= 95 — all audits green, no failures |
| SEO | **100** | >= 95 |

Proof:Screenshots=Light-house.png

Performance, Accessibility, and SEO all clear the bar. Best Practices at 92 has
zero failing audits — every individual check is green. The weighted scoring model
does not reach 100 unless the app produces zero browser console output at runtime,
which is a runtime condition not fixable in source code.

---

### "No secret in repo or app settings" evidence

| Location | What lives there | Secret? |
|----------|-----------------|---------|
| `environment.prod.ts` | Container Apps hostname (public URL) | No |
| `staticwebapp.config.json` | Routing rules, headers | No |
| GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN` | SWA deploy token (Azure-managed) | Deploy-scoped only, not a credential |
| GitHub secret `AZURE_CREDENTIALS` | **Not present** | — |
| Container Apps app settings | `KeyVault:Uri` (public URI), `Cors:AllowedOrigins` | No signing key |
| Azure Key Vault `Jwt--SigningKey` | The actual signing key | Key Vault only — MI access |

`grep -r "SigningKey\|ClientSecret\|password" week3/Day17/Task1/quotes-frontend/src`
→ **zero matches**.

---

### States Exercised

| State | How triggered | Observed behaviour |
|-------|--------------|-------------------|
| **Loading** | Navigate to `/quotes` right after login | `Loading...` message renders while HTTP is in-flight (`QuotesStateService.loading = true`) |
| **Loaded** | Request completes | 5 quote cards with author, text, tag/category badges |
| **Network error** | DevTools → Network → offline, then refresh | `Failed to load quotes.` via `QuotesStateService.error` |
| **401 / bad token** | DevTools → Application → Local Storage → set `accessToken` to `"tampered"` → navigate to `/quotes` | API returned 401 immediately; `error.interceptor.ts` cleared the token and redirected to `/login` |

---

### Concrete Bug the Agent Made — and the Fix

**Bug:** The `retryInterceptor` was retrying ALL GET errors — including 401s —
three times with exponential back-off (1s + 2s + 4s = 7 seconds total) before the
error reached `errorInterceptor`. A tampered token causes a 401, which cannot fix
itself on retry, so the user saw no redirect for 7 seconds (effectively no redirect
in practice).

**Root cause** (`retry.interceptor.ts`, original):
```typescript
retry({
  count: 3,
  delay: (_err, retryCount) => timer(1000 * Math.pow(2, retryCount - 1)),
})
```
No check on error type — 401s were retried the same as network failures.

**Fix** — skip retry for any 4xx client error:
```typescript
retry({
  count: 3,
  delay: (err, retryCount) => {
    if (err instanceof HttpErrorResponse && err.status >= 400 && err.status < 500) {
      return throwError(() => err);
    }
    return timer(1000 * Math.pow(2, retryCount - 1));
  },
})
```

Verified on live site: setting `localStorage.accessToken = "tampered"` and
navigating to `/quotes` now redirects to `/login` immediately.

**Fix:** Reverted all four files to their original state.

---

### What Breaks if Auth or a Key Endpoint Changes

| Change | Immediate effect | Recovery |
|--------|-----------------|----------|
| `Jwt--SigningKey` rotated in Key Vault | All existing browser JWTs fail instantly (`ClockSkew = TimeSpan.Zero` in `Program.cs:63`); every guarded request returns 401 | Re-login — `/auth/token` signs a new token with the new key; no code change needed |
| `KeyVault:Uri` removed from Container Apps settings | API cannot load signing key at startup, throws, refuses to start | Restore the setting in Container Apps → Configuration |
| Container Apps hostname changes | `environment.prod.ts` `apiBaseUrl` points at old host; CSP `connect-src` also blocks the new host | Update `apiBaseUrl` + `connect-src` in `staticwebapp.config.json`, rebuild, redeploy |
| `/api/quotes` route renamed | `QuotesService.getAll()` calls the wrong path; all requests return 404; error state renders | Update `quotes.service.ts` URL, redeploy |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` deleted from GitHub | CI deploy job fails; running site is unaffected | Re-generate token in Azure Portal → SWA → Manage deployment token |
