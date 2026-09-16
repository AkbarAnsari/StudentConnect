// StudentConnect on Azure App Service for Containers (2 single-container Web Apps) + managed
// PostgreSQL — a leaner alternative to infra/main.bicep: just App Service + Postgres + a
// registry (no Key Vault, Storage Account, Application Insights, or Static Web App). Good
// middle ground between infra/vm.bicep (cheapest, you own the OS) and infra/container-apps.bicep
// (more managed, pricier) — App Service Basic tier is Always On by default, so the API's
// hosted BackgroundService workers run continuously same as the other options.
//
// Resource count: 1 App Service Plan + 2 Web Apps + 1 Container Registry + 1 Postgres Flexible
// Server + 1 user-assigned identity for ACR pull = 5 resource types.
//
// Cost note: ~$13/month (Linux B1 plan, shared by both web apps) + ~$12-15/month (Postgres
// Burstable B1ms) + ~$5/month (ACR Basic) ≈ $30-33/month — similar to infra/main.bicep's
// total but with far fewer moving parts to configure/secure.
//
// Images must exist in the registry before these Web Apps can start — build & push first:
//   az acr build -r <acrName> -g <rg> -t studentconnect-api:latest -f backend/StudentConnect.Api/Dockerfile backend
//   az acr build -r <acrName> -g <rg> -t studentconnect-frontend:latest frontend
//
// Same nginx.conf caveat as infra/container-apps.bicep: `proxy_pass http://api:8080/api/;`
// relies on Docker Compose's "api" DNS alias, which doesn't exist here. Repoint it at the
// api Web App's default hostname (written to the frontend app's API_INTERNAL_FQDN setting)
// before building the frontend image for this target.
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1 (<300 students, ~5-15 concurrent):  appServicePlanSku=B1, Postgres Standard_B1ms
//   Tier 2 (300-2,000 students, ~30-100):      appServicePlanSku=S1, Postgres Standard_B2s/B2ms
//     — autoscale the frontend app freely; keep the api app at 1 instance (see the
//     background-worker constraint in README.md before scaling it out).
//   Tier 3 (2,000+ students, 200+):            appServicePlanSku=P1v3, Postgres General
//     Purpose Standard_D4ds_v4+ with zone-redundant HA; consider migrating to main.bicep
//     for Key Vault/observability at this scale.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f app-service-minimal.bicep -p app-service-minimal.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>

@description('Short environment name used in resource names, e.g. dev, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service Plan SKU. B1 is the cheapest tier with Always On support.')
param appServicePlanSku string = 'B1'

@description('PostgreSQL Flexible Server administrator login (not the app role used at runtime).')
param postgresAdminLogin string = 'studentconnect_admin'

@secure()
@description('PostgreSQL Flexible Server administrator password.')
param postgresAdminPassword string

@secure()
@description('Signing secret for the app\'s local JWT authentication.')
param jwtSecret string

@description('Container image tag to deploy for both api and frontend (must already exist in the registry).')
param imageTag string = 'latest'

var resourcePrefix = 'studentconnect-${environmentName}'
var acrName = toLower(replace('stuconnacr${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))
var postgresServerName = '${resourcePrefix}-pg'
var postgresDatabaseName = 'studentconnect_db'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-appservice-identity'
  location: location
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, appIdentity.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // AcrPull
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: { storageSizeGB: 32, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: postgresDatabaseName
}

resource postgresAllowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${resourcePrefix}-plan'
  location: location
  kind: 'linux'
  sku: { name: appServicePlanSku }
  properties: { reserved: true }
}

resource apiApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${resourcePrefix}-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${appIdentity.id}': {} }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion: 'DOCKER|${containerRegistry.properties.loginServer}/studentconnect-api:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: appIdentity.properties.clientId
      appSettings: [
        { name: 'AZURE_CLIENT_ID', value: appIdentity.properties.clientId }
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'ConnectionStrings__DefaultConnection', value: 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require' }
        { name: 'Jwt__Secret', value: jwtSecret }
        { name: 'Jwt__Issuer', value: 'StudentConnect' }
        { name: 'Jwt__Audience', value: 'StudentConnect' }
        { name: 'Storage__Provider', value: 'Local' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      ]
    }
  }
}

resource frontendApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${resourcePrefix}-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${appIdentity.id}': {} }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion: 'DOCKER|${containerRegistry.properties.loginServer}/studentconnect-frontend:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: appIdentity.properties.clientId
      appSettings: [
        { name: 'AZURE_CLIENT_ID', value: appIdentity.properties.clientId }
        { name: 'WEBSITES_PORT', value: '80' }
        // See the header comment: nginx.conf must be templated to proxy_pass here instead
        // of the Docker-Compose-only "api" DNS alias.
        { name: 'API_INTERNAL_FQDN', value: apiApp.properties.defaultHostName }
      ]
    }
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output frontendUrl string = 'https://${frontendApp.properties.defaultHostName}'
output acrLoginServer string = containerRegistry.properties.loginServer
