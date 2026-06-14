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
- Lighthouse Performance ≥ 95 on the live URL
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
    "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self' https://quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io;"
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
        new DefaultAzureCredential(),   // ← system-assigned Managed Identity
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
(Aristotle × 2, Marcus Aurelius × 2, Seneca).

---

### Lighthouse Score (desktop, incognito)

| Category | Score |
|----------|-------|
| Performance | **98** |
| Accessibility | **97** |
| Best Practices | **100** |
| SEO | **100** |

Key factors: Angular 21 production build (tree-shaken, code-split, output-hashed),
`Cache-Control: immutable` on all `*.js / *.css / *.woff2` assets, no
render-blocking third-party scripts, SWA CDN edge node.

---

### "No secret in repo or app settings" evidence

| Location | What lives there | Secret? |
|----------|-----------------|---------|
| `environment.prod.ts` | Container Apps hostname (public URL) | No |
| `staticwebapp.config.json` | Routing rules, headers | No |
| GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN` | SWA deploy token (Azure-managed) | Deploy-scoped, not a credential |
| GitHub secret `AZURE_CREDENTIALS` | **Not present** | — |
| Container Apps app settings | `KeyVault:Uri` (public URI), `Cors:AllowedOrigins` | No signing key |
| Azure Key Vault `Jwt--SigningKey` | The actual signing key | Key Vault only — MI access |

`grep -r "SigningKey\|ClientSecret\|password" week3/Day17/Task1/quotes-frontend/src`
→ **zero matches**.

---

### States Exercised

| State | How triggered | Observed behaviour |
|-------|--------------|-------------------|
| **Loading** | Navigate to `/quotes` right after login | `Loading…` message renders while HTTP is in-flight (`QuotesStateService.loading = true`) |
| **Loaded** | Request completes | 5 quote cards with author, text, tag/category badges |
| **Empty** | Cleared the DB seed and reloaded | `No quotes yet.` renders (`pagedQuotes().length === 0` branch) |
| **Network error** | DevTools → Network → offline, then refresh | `Failed to load quotes.` via `QuotesStateService.error` |
| **401 / bad token** | Set `localStorage.accessToken = "tampered"`, navigated to `/quotes` | API returned 401; `error.interceptor.ts` caught it and redirected to `/login` |

---

### Concrete Bug the Agent Made — and the Fix

**Bug:** The agent (Claude Code) incorrectly assumed "Managed Identity" required
a server-side Azure Functions proxy and rewrote four source files:

- `environment.prod.ts` — changed `apiBaseUrl` to `''` (relative URL),
  silently breaking the live Container Apps hostname
- `environment.ts` — replaced `apiBaseUrl` with two new keys
  (`quotesApiUrl`, `authTokenUrl`) that the rest of the codebase did not expect,
  causing TypeScript compile errors on `environment.apiBaseUrl`
- `quotes.service.ts` — switched to `environment.quotesApiUrl` (undefined in dev)
- `login.component.ts` — switched to `environment.authTokenUrl` (undefined in dev)

**Why it was wrong:** The existing setup already satisfies the MI requirement.
The Container Apps API calls `new DefaultAzureCredential()` at startup
(`Program.cs:20`) to pull `Jwt--SigningKey` from Key Vault. The signing key never
appears in source code or app settings. Adding a proxy only introduced extra
latency and complexity with no security gain.

**Fix:** Reverted all four files to their original state.

---

### What Breaks if Auth or a Key Endpoint Changes

| Change | Immediate effect | Recovery |
|--------|-----------------|----------|
| `Jwt--SigningKey` rotated in Key Vault | All existing browser JWTs fail instantly (`ClockSkew = TimeSpan.Zero` in `Program.cs:63`); every guarded request returns 401 | Re-login — `/auth/token` signs a new token with the new key; no code change |
| `KeyVault:Uri` removed from Container Apps settings | API cannot load signing key at startup, throws, refuses to start | Restore the setting in Container Apps → Configuration |
| Container Apps hostname changes | `environment.prod.ts` `apiBaseUrl` points at old host; CSP `connect-src` also blocks the new host | Update `apiBaseUrl` + `connect-src` in `staticwebapp.config.json`, rebuild, redeploy |
| `/api/quotes` route renamed | `QuotesService.getAll()` calls the wrong path; all requests return 404; error state renders | Update `quotes.service.ts` URL, redeploy |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` deleted from GitHub | CI deploy job fails; **running site is unaffected** | Re-generate token in Azure Portal → SWA → Manage deployment token |
