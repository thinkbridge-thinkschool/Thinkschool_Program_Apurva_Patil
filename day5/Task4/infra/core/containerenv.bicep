param name string
param location string
param tags object = {}
param logAnalyticsWorkspaceCustomerId string
param logAnalyticsWorkspaceSharedKey string

resource containerEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceCustomerId
        sharedKey: logAnalyticsWorkspaceSharedKey
      }
    }
  }
}

output id string = containerEnv.id
output name string = containerEnv.name
output defaultDomain string = containerEnv.properties.defaultDomain
