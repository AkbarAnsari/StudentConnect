// StudentConnect on Azure Container Apps (ACA) + managed PostgreSQL — the recommended
// alternative to infra/vm.bicep when you'd rather not own OS patching/disk management and
// are willing to pay a bit more for a managed platform. Two Container Apps (api, frontend)
// run in one (free) Container Apps Environment with minReplicas=1 so the API's in-process
// BackgroundService workers (announcement/homework/learning-material dispatch, operational
// governance) never stop running — this rules out scale-to-zero.
//
// Resource count: Container Apps Environment + Log Analytics (required by the environment) +
// Container Registry + 2 Container Apps + Postgres Flexible Server + a user-assigned identity
// for ACR pull. ~5-6 resource types — more than the VM option, fewer than infra/main.bicep
// (no Key Vault/Storage Account/App Insights/Static Web App).
//
// Cost note: Container Apps Consumption plan bills per vCPU-second/GiB-second even at
// minReplicas=1 — for two always-on apps at 0.5 vCPU/1 GiB each this is roughly $35-45/month,
// which is MORE than the VM option. Choose this for less ops burden (no OS, auto HTTPS,
// built-in scaling), not for the lowest possible bill.
//
// IMPORTANT deployment nuance (not solved by Bicep alone): frontend/nginx.conf's
// `proxy_pass http://api:8080/api/;` relies on Docker Compose's short DNS alias "api", which
// does not exist in Container Apps. Before deploying the frontend image here, repoint that
// proxy_pass at the api Container App's internal FQDN (this template writes it to the
// frontend app's API_INTERNAL_FQDN setting — you still need to template nginx.conf, e.g. via
// envsubst in the container's entrypoint, to actually use it).
//
// Images must exist in the registry before the Container Apps are created — build & push
// first:
//   az acr build -r <acrName> -g <rg> -t studentconnect-api:latest -f backend/StudentConnect.Api/Dockerfile backend
//   az acr build -r <acrName> -g <rg> -t studentconnect-frontend:latest frontend
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1 (<300 students, ~5-15 concurrent):  api 0.5 vCPU/1Gi, frontend 0.25 vCPU/0.5Gi,
//     both maxReplicas=1 (the defaults below), Postgres Standard_B1ms.
//   Tier 2 (300-2,000 students, ~30-100):      api 0.5-1 vCPU/1-2Gi, STILL maxReplicas=1
//     (background-worker constraint — see README.md); frontend can go maxReplicas=2-3 with
//     an HTTP-concurrency scale rule since nginx is stateless. Postgres Standard_B2s/B2ms.
//   Tier 3 (2,000+ students, 200+):            api 1-2 vCPU/2-4Gi; split into a stateless
//     "web" Container App (maxReplicas 2-10, hosted services disabled) + a single-replica
//     "worker" Container App for the BackgroundServices, to scale api without duplicate
//     processing. Postgres General Purpose Standard_D4ds_v4+ with zone-redundant HA + replica.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f container-apps.bicep -p container-apps.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>

@description('Short environment name used in resource names, e.g. dev, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

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

@description('vCPU/memory for the api Container App. 0.5 vCPU / 1 GiB is the minimum ACA allows.')
param apiResources object = { cpu: json('0.5'), memory: '1Gi' }

@description('vCPU/memory for the frontend Container App.')
param frontendResources object = { cpu: json('0.25'), memory: '0.5Gi' }

var resourcePrefix = 'studentconnect-${environmentName}'
var acrName = toLower(replace('stuconnacr${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))
var postgresServerName = '${resourcePrefix}-pg'
var postgresDatabaseName = 'studentconnect_db'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${resourcePrefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-aca-identity'
  location: location
}

// Lets the Container Apps pull images from ACR without admin credentials/passwords.
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, apiIdentity.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // AcrPull
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${resourcePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
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

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${resourcePrefix}-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${apiIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      // Internal-only: not reachable from the public internet, only from the frontend app
      // inside this Container Apps Environment.
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        { server: containerRegistry.properties.loginServer, identity: apiIdentity.id }
      ]
      secrets: [
        { name: 'postgres-password', value: postgresAdminPassword }
        { name: 'jwt-secret', value: jwtSecret }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${containerRegistry.properties.loginServer}/studentconnect-api:${imageTag}'
          resources: apiResources
          env: [
            { name: 'ConnectionStrings__DefaultConnection', value: 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${'$'}(POSTGRES_PASSWORD);SSL Mode=Require' }
            { name: 'POSTGRES_PASSWORD', secretRef: 'postgres-password' }
            { name: 'Jwt__Secret', secretRef: 'jwt-secret' }
            { name: 'Jwt__Issuer', value: 'StudentConnect' }
            { name: 'Jwt__Audience', value: 'StudentConnect' }
            { name: 'Storage__Provider', value: 'Local' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}

resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${resourcePrefix}-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${apiIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        transport: 'http'
      }
      registries: [
        { server: containerRegistry.properties.loginServer, identity: apiIdentity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'frontend'
          image: '${containerRegistry.properties.loginServer}/studentconnect-frontend:${imageTag}'
          resources: frontendResources
          // See the header comment: nginx.conf must be templated to proxy_pass here instead
          // of the Docker-Compose-only "api" DNS alias.
          env: [
            { name: 'API_INTERNAL_FQDN', value: apiApp.properties.configuration.ingress.fqdn }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}

output acrLoginServer string = containerRegistry.properties.loginServer
output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output apiInternalFqdn string = apiApp.properties.configuration.ingress.fqdn
