// StudentConnect on Azure App Service "multi-container" (Docker Compose) + managed
// PostgreSQL — packages api+frontend into ONE Web App (via infra/app-service-docker-compose.yml)
// instead of two, at the cost of relying on a feature Microsoft has not meaningfully invested
// in for years (favoring Azure Container Apps for new multi-container workloads instead).
// Provided for completeness/comparison — prefer infra/app-service-minimal.bicep (2 plain
// single-container Web Apps) or infra/container-apps.bicep for anything beyond
// experimentation.
//
// Resource count: 1 App Service Plan + 1 Web App + 1 Container Registry + 1 Postgres Flexible
// Server = 4 resource types — the fewest of any container-based PaaS option here.
//
// Cost note: ~$13/month (Linux B1 plan) + ~$12-15/month (Postgres Burstable B1ms) +
// ~$5/month (ACR Basic) ≈ $30-33/month.
//
// Known limitations vs. the other options:
//  - Multi-container Web Apps use EPHEMERAL local storage — this is exactly why Postgres is
//    NOT one of the containers here (see app-service-docker-compose.yml's header comment).
//  - Health-check/restart behavior for individual containers within the group is weaker than
//    Container Apps or a real orchestrator.
//  - Same nginx.conf caveat as the other container options: proxy_pass must target the
//    internal container port directly (App Service's multi-container networking exposes
//    other containers by their compose service name on the SAME app, e.g. `http://api:8080`
//    actually DOES work here unlike Container Apps/App Service-single-container, because
//    App Service's multi-container feature runs all services on one shared Docker network
//    per app — so this is the one option that needs NO nginx.conf changes).
//
// Images must exist in the registry before this Web App can start — build & push first:
//   az acr build -r <acrName> -g <rg> -t studentconnect-api:latest -f backend/StudentConnect.Api/Dockerfile backend
//   az acr build -r <acrName> -g <rg> -t studentconnect-frontend:latest frontend
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1 (<300 students, ~5-15 concurrent):  appServicePlanSku=B1, Postgres Standard_B1ms —
//     realistically the only tier this option is meant for.
//   Tier 2+: possible with a bigger plan SKU, but frontend and api can't scale independently
//     in one Web App — prefer app-service-minimal.bicep once you're past Tier 1.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f app-service-compose.bicep -p app-service-compose.bicepparam \
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

var composeTemplate = loadTextContent('app-service-docker-compose.yml')
var composeRendered = replace(replace(composeTemplate, '__ACR_LOGIN_SERVER__', containerRegistry.properties.loginServer), '__IMAGE_TAG__', imageTag)

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  // Multi-container Web Apps only support registry username/password auth (no managed
  // identity pull support the way single-container/Container Apps do), so the admin user
  // must stay enabled here and its password is read into the app settings below.
  properties: { adminUserEnabled: true }
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

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${resourcePrefix}-app'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion: 'COMPOSE|${base64(composeRendered)}'
      appSettings: [
        { name: 'DOCKER_REGISTRY_SERVER_URL', value: 'https://${containerRegistry.properties.loginServer}' }
        { name: 'DOCKER_REGISTRY_SERVER_USERNAME', value: containerRegistry.listCredentials().username }
        { name: 'DOCKER_REGISTRY_SERVER_PASSWORD', value: containerRegistry.listCredentials().passwords[0].value }
        { name: 'WEBSITES_PORT', value: '80' }
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

output appUrl string = 'https://${webApp.properties.defaultHostName}'
output acrLoginServer string = containerRegistry.properties.loginServer
