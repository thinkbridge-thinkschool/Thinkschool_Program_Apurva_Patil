# Azure Container Apps Setup — Task 3 Output

## Region Note

`centralindia` was used (allowed by the `D Y Patil Education Society` tenant).
Environment domain: `purpleflower-cae11894.centralindia.azurecontainerapps.io`

---

## Commands Run

### 1. Create Resource Group

```bash
az group create -n thinkschool-rg -l centralindia
```

### 2. Create Container Apps Environment

```bash
az containerapp env create -n thinkschool-env -g thinkschool-rg -l centralindia
```

### 3. Create Container App

```bash
az containerapp create \
  --name quotesapi \
  --resource-group thinkschool-rg \
  --environment thinkschool-env \
  --image mcr.microsoft.com/dotnet/samples:aspnetapp \
  --ingress external \
  --target-port 8080 \
  --min-replicas 0 \
  --max-replicas 5 \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 10
```

---

## az containerapp env show — Output

Command run:
```
az containerapp env show -n thinkschool-env -g thinkschool-rg
```

```json
{
  "id": "/subscriptions/9f41fde6-a1a4-41ca-92f8-f86e4f25abe6/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env",
  "location": "Central India",
  "name": "thinkschool-env",
  "properties": {
    "appLogsConfiguration": {
      "destination": "log-analytics",
      "logAnalyticsConfiguration": {
        "customerId": "e2e88fde-a8d6-4dac-8fd6-1c827961afe8",
        "dynamicJsonColumns": false,
        "sharedKey": null
      }
    },
    "customDomainConfiguration": {
      "customDomainVerificationId": "607A7245DA081DE89A17944A3EAF4E352C618437A1BD4942E93B2150AFE47195",
      "dnsSuffix": null
    },
    "daprConfiguration": {
      "version": "1.16.4-msft.6"
    },
    "defaultDomain": "purpleflower-cae11894.centralindia.azurecontainerapps.io",
    "environmentMode": "WorkloadProfiles",
    "eventStreamEndpoint": "https://centralindia.azurecontainerapps.dev/subscriptions/9f41fde6-a1a4-41ca-92f8-f86e4f25abe6/resourceGroups/thinkschool-rg/managedEnvironments/thinkschool-env/eventstream",
    "kedaConfiguration": {
      "version": "2.18.1"
    },
    "peerAuthentication": {
      "mtls": {
        "enabled": false
      }
    },
    "peerTrafficConfiguration": {
      "encryption": {
        "enabled": false
      }
    },
    "provisioningState": "Succeeded",
    "publicNetworkAccess": "Enabled",
    "staticIp": "4.224.10.31",
    "workloadProfiles": [
      {
        "enableFips": false,
        "name": "Consumption",
        "workloadProfileType": "Consumption"
      }
    ],
    "zoneRedundant": false
  },
  "resourceGroup": "thinkschool-rg",
  "systemData": {
    "createdAt": "2026-05-24T12:16:23.5114303",
    "createdBy": "ApurvaPatil.beds21@dypgroup.edu.in",
    "createdByType": "User",
    "lastModifiedAt": "2026-05-24T12:16:23.5114303",
    "lastModifiedBy": "ApurvaPatil.beds21@dypgroup.edu.in",
    "lastModifiedByType": "User"
  },
  "type": "Microsoft.App/managedEnvironments"
}
```

### Key Fields

| Field | Value |
|---|---|
| `provisioningState` | `Succeeded` |
| `defaultDomain` | `purpleflower-cae11894.centralindia.azurecontainerapps.io` |
| `staticIp` | `4.224.10.31` |
| `environmentMode` | `WorkloadProfiles` |
| `kedaConfiguration.version` | `2.18.1` |
| `daprConfiguration.version` | `1.16.4-msft.6` |
| `peerAuthentication.mtls.enabled` | `false` |
| `appLogsConfiguration.destination` | `log-analytics` |

---

## az containerapp show — Output

Command run:
```
az containerapp show -n quotesapi -g thinkschool-rg
```

```json
{
  "id": "/subscriptions/9f41fde6-a1a4-41ca-92f8-f86e4f25abe6/resourceGroups/thinkschool-rg/providers/Microsoft.App/containerapps/quotesapi",
  "location": "Central India",
  "name": "quotesapi",
  "properties": {
    "configuration": {
      "activeRevisionsMode": "Single",
      "ingress": {
        "allowInsecure": false,
        "external": true,
        "fqdn": "quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io",
        "targetPort": 8080,
        "traffic": [
          {
            "latestRevision": true,
            "weight": 100
          }
        ],
        "transport": "Auto"
      }
    },
    "latestReadyRevisionName": "quotesapi--1awz2x8",
    "latestRevisionFqdn": "quotesapi--1awz2x8.purpleflower-cae11894.centralindia.azurecontainerapps.io",
    "latestRevisionName": "quotesapi--1awz2x8",
    "provisioningState": "Succeeded",
    "runningStatus": "Running",
    "template": {
      "containers": [
        {
          "image": "mcr.microsoft.com/dotnet/samples:aspnetapp",
          "name": "quotesapi",
          "resources": {
            "cpu": 0.5,
            "ephemeralStorage": "2Gi",
            "memory": "1Gi"
          }
        }
      ],
      "scale": {
        "cooldownPeriod": 300,
        "maxReplicas": 5,
        "minReplicas": 0,
        "pollingInterval": 30,
        "rules": [
          {
            "http": {
              "metadata": {
                "concurrentRequests": "10"
              }
            },
            "name": "http-rule"
          }
        ]
      }
    },
    "workloadProfileName": "Consumption"
  },
  "resourceGroup": "thinkschool-rg",
  "systemData": {
    "createdAt": "2026-05-24T12:28:03.4708935",
    "createdBy": "ApurvaPatil.beds21@dypgroup.edu.in",
    "createdByType": "User",
    "lastModifiedAt": "2026-05-24T12:28:03.4708935",
    "lastModifiedBy": "ApurvaPatil.beds21@dypgroup.edu.in",
    "lastModifiedByType": "User"
  },
  "type": "Microsoft.App/containerApps"
}
```

### Key Fields

| Field | Value |
|---|---|
| `provisioningState` | `Succeeded` |
| `runningStatus` | `Running` |
| `ingress.external` | `true` |
| `ingress.fqdn` | `quotesapi.purpleflower-cae11894.centralindia.azurecontainerapps.io` |
| `ingress.targetPort` | `8080` |
| `latestRevisionName` | `quotesapi--1awz2x8` |
| `scale.minReplicas` | `0` (scale-to-zero) |
| `scale.maxReplicas` | `5` |
| `scale.rules[0].name` | `http-rule` |
| `scale.rules[0].http.metadata.concurrentRequests` | `10` |
| `workloadProfileName` | `Consumption` |
| `container.cpu` | `0.5` vCPU |
| `container.memory` | `1Gi` |
