targetScope = 'subscription'

// ── Parameters ──────────────────────────────────────────────────────────────
@minLength(1)
@maxLength(64)
@description('Environment name. Used to generate a unique resource-name suffix.')
param environmentName string

@minLength(1)
@description('Azure region for all resources.')
param location string

@description('Container image pushed by azd up. Leave empty on first provision.')
param quotesapiImageName string = ''

@description('Resource group that contains the existing Container Apps Environment.')
param existingContainerAppsEnvRg string = 'thinkschool-rg'

@description('Name of the existing Container Apps Environment to reuse (free tier allows only one).')
param existingContainerAppsEnvName string = 'thinkschool-env'

@description('Location of the existing Container Apps Environment. Container App must match this region.')
param existingContainerAppsEnvLocation string = 'centralindia'

// ── Variables ────────────────────────────────────────────────────────────────
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }
// Reuse the existing environment rather than provisioning a second one.
var existingContainerAppsEnvId = '/subscriptions/${subscription().subscriptionId}/resourceGroups/${existingContainerAppsEnvRg}/providers/Microsoft.App/managedEnvironments/${existingContainerAppsEnvName}'

// ── Resource Group ────────────────────────────────────────────────────────────
resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// ── Log Analytics Workspace ──────────────────────────────────────────────────
module logAnalytics 'core/loganalytics.bicep' = {
  name: 'logAnalytics'
  scope: rg
  params: {
    name: 'log-${resourceToken}'
    location: location
    tags: tags
  }
}

// ── Application Insights ─────────────────────────────────────────────────────
module appInsights 'core/appinsights.bicep' = {
  name: 'appInsights'
  scope: rg
  params: {
    name: 'appi-${resourceToken}'
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

// ── Azure Container Registry ─────────────────────────────────────────────────
module registry 'core/registry.bicep' = {
  name: 'registry'
  scope: rg
  params: {
    name: 'acr${resourceToken}'
    location: location
    tags: tags
  }
}

// ── Container App ─────────────────────────────────────────────────────────────
// Note: no new Container Apps Environment is created here — the subscription
// allows only one, and thinkschool-env (from Task3) is reused via its resource ID.
module quotesapi 'app/quotesapi.bicep' = {
  name: 'quotesapi'
  scope: rg
  params: {
    name: 'ca-quotesapi-${resourceToken}'
    location: existingContainerAppsEnvLocation
    tags: union(tags, { 'azd-service-name': 'quotesapi' })
    imageName: empty(quotesapiImageName) ? 'mcr.microsoft.com/dotnet/samples:aspnetapp' : quotesapiImageName
    containerAppsEnvironmentId: existingContainerAppsEnvId
    containerRegistryLoginServer: registry.outputs.loginServer
    containerRegistryName: registry.outputs.name
    applicationInsightsConnectionString: appInsights.outputs.connectionString
  }
}

// ── Outputs (consumed by azd and printed after azd up) ──────────────────────
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.name
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = existingContainerAppsEnvName
output SERVICE_QUOTESAPI_URI string = quotesapi.outputs.uri
output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.outputs.connectionString
