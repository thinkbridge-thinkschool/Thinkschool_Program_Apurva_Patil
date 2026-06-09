# azd Deployment — Task 4 Output

## What azd does vs. Task 3's manual az CLI approach

| Step | Task 3 (manual `az` CLI) | Task 4 (`azd up`) |
|---|---|---|
| Build container image | `docker build` manually | `azd` builds via Docker automatically |
| Push to registry | `docker push` to ACR | `azd` pushes to ACR it provisions |
| Provision infra | Individual `az` commands | Bicep in `infra/` applied at once |
| Deploy container app | `az containerapp create` | Bicep `infra/app/quotesapi.bicep` |
| Get live URL | Copied from `az containerapp show` | Printed as `SERVICE_QUOTESAPI_URI` output |

---

## Pre-requisites

```bash
# Install azd (macOS/Linux via brew)
brew install azd

# Windows (winget)
winget install Microsoft.Azd

# Authenticate
azd auth login
```

---

## Step 1 — azd init (already done — files generated)

Running `azd init` in this directory with "Use code in current directory" → "Confirm and continue"
created the three scaffold files:

```
azure.yaml                  ← service manifest
infra/main.bicep            ← subscription-scoped Bicep entry point
infra/main.parameters.json  ← parameter bindings for azd env vars
```

Additional hand-authored modules created to keep main.bicep clean:

```
infra/core/loganalytics.bicep   ← Log Analytics workspace
infra/core/appinsights.bicep    ← Application Insights (workspace-based)
infra/core/registry.bicep       ← Azure Container Registry (Basic SKU)
infra/core/containerenv.bicep   ← Container Apps Environment
infra/app/quotesapi.bicep       ← Container App definition
```

---

## Step 2 — azure.yaml explained

```yaml
name: quotesapi

services:
  quotesapi:
    project: .          # build context is the repo root (Dockerfile here)
    language: csharp
    host: containerapp  # target platform
    docker:
      path: ./Dockerfile
      context: .
```

`azd up` reads this file to know:
- what to build (the Dockerfile in this directory)
- where to deploy (a Container App)
- which Bicep output to use as the live URL (`SERVICE_QUOTESAPI_URI`)

---

## Step 3 — azd up

```bash
# From inside day5/Task4/
azd up
```

azd prompts for two values on first run:

| Prompt | Value used |
|---|---|
| Environment name | `quotesapi-task4` |
| Azure location | `centralindia` |

These are saved to `.azure/<env-name>/.env` so subsequent runs skip the prompts.

### What happens internally

```
1. azd package  →  docker build -f Dockerfile -t <acr>.azurecr.io/quotesapi:<sha> .
2. azd provision →  az deployment sub create --template-file infra/main.bicep
                    provisions: rg-quotesapi-task4
                                acr<token>
                                log-<token>
                                appi-<token>
                                cae-quotesapi-task4
3. azd deploy   →  docker push <acr>.azurecr.io/quotesapi:<sha>
                    az containerapp update --image <acr>.../quotesapi:<sha>
```

---

## Step 4 — Actual azd up output

```
Provisioning and deploying (azd up)

  (✓) Done: Resource group: rg-questapi-task4 (3.34s)
  (✓) Done: Container Registry: acrygvk6kar7qyrc (9.069s)
  (✓) Done: Log Analytics workspace: log-ygvk6kar7qyrc (21.775s)
  (✓) Done: Application Insights: appi-ygvk6kar7qyrc (1m49.402s)
  (✓) Done: Container App: ca-quotesapi-ygvk6kar7qyrc (19.669s)

  Service    Status    Duration
  ─────────  ────────  ──────────
  ● quotesapi  Done    5m36s
  - Endpoint: https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io/

SUCCESS: Your application was provisioned and deployed to Azure in 14 minutes 45 seconds.
  Provisioning: 1 minute 27 seconds
  Deploying:    13 minutes 17 seconds
```

**Note:** Subscription allows only 1 Container Apps Environment. The existing `thinkschool-env` from Task3 was reused — `infra/main.bicep` was updated to reference it by resource ID instead of creating a new one.

---

## Step 5 — Smoke tests against the live URL

Live URL: `https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io`

### Get a token

```powershell
$response = Invoke-RestMethod -Method Post `
  -Uri "https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io/auth/token" `
  -ContentType "application/json" `
  -Body '{"userId":"apurv","scopes":["quotes.write"]}'
$TOKEN = $response.accessToken
```

### Create a quote

```powershell
Invoke-RestMethod -Method Post `
  -Uri "https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io/quotes" `
  -Headers @{Authorization="Bearer $TOKEN"} `
  -ContentType "application/json" `
  -Body '{"text":"azd makes deploys repeatable"}'
```

Actual response:

```
id ownerId text                         createdAt
-- ------- ----                         ---------
 1 apurv   azd makes deploys repeatable 2026-05-24T16:45:28.8371681Z
```

### List all quotes

```powershell
Invoke-RestMethod `
  -Uri "https://ca-quotesapi-ygvk6kar7qyrc.purpleflower-cae11894.centralindia.azurecontainerapps.io/quotes" `
  -Headers @{Authorization="Bearer $TOKEN"}
```

Actual response:

```
id ownerId text                         createdAt
-- ------- ----                         ---------
 1 apurv   azd makes deploys repeatable 2026-05-24T16:45:28.8371681
```

---

## Infrastructure created by azd up

| Resource | Name pattern | Notes |
|---|---|---|
| Resource Group | `rg-quotesapi-task4` | `rg-<env-name>` |
| Log Analytics | `log-<token>` | Backs container env + App Insights |
| Application Insights | `appi-<token>` | Workspace-based; AI conn string injected as secret |
| Container Registry | `acr<token>` | Basic SKU; admin user enabled for pull credentials |
| Container Apps Env | `cae-quotesapi-task4` | Log Analytics sink configured |
| Container App | `ca-quotesapi-<token>` | Scale 0–5 replicas; HTTP rule at 10 concurrent req |

---

## Key differences from Task 3

1. **Single command** — `azd up` replaces ~10 individual `az` CLI commands.
2. **Repeatable** — run again after a code change: azd rebuilds, pushes, and redeploys only the container (infra is idempotent).
3. **Bicep tracks state** — the ARM deployment is tracked by Azure so re-runs are incremental, not destructive.
4. **Application Insights wired automatically** — the connection string flows from Bicep output → Container App secret → `ApplicationInsights__ConnectionString` env var.
5. **Environment config stored locally** — `.azure/quotesapi-task4/.env` stores subscription ID, location, env name so `azd up` is a one-word deploy from any machine that has run `azd auth login`.

---

## Tearing down

```bash
azd down
# Prompts: "Delete resource group rg-quotesapi-task4? (y/N)"
```

This deletes all provisioned resources in one step.
