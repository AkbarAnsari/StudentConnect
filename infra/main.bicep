// StudentConnect Azure infrastructure — prepared for future deployment (Phase 24), not
// deployed yet. Deploy with:
//   az deployment group create -g <resource-group> -f main.bicep -p main.bicepparam
//
// Deliberately keeps local JWT/database-driven authentication (no Entra ID) per the
// phase-15 roadmap's explicit decision to defer identity-provider migration.

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
@description('Signing secret for the app\'s local JWT authentication (see Infrastructure/Auth/JwtTokenService.cs).')
param jwtSecret string

@description('Origin of the deployed Angular frontend, added to the API\'s CORS allow-list.')
param frontendOrigin string

var resourcePrefix = 'studentconnect-${environmentName}'
// Storage account names must be globally unique, lowercase, alphanumeric only.
var storageAccountName = toLower(replace('stuconn${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))
var keyVaultName = take('kv-${resourcePrefix}-${uniqueString(resourceGroup().id)}', 24)
var postgresServerName = '${resourcePrefix}-pg'
var postgresDatabaseName = 'studentconnect_db'

// --- Observability -----------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${resourcePrefix}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${resourcePrefix}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// --- Identity ------------------------------------------------------------------------

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-api-identity'
  location: location
}

// --- Secrets ---------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource postgresPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'postgres-admin-password'
  properties: {
    value: postgresAdminPassword
  }
}

resource jwtSecretSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'jwt-secret'
  properties: {
    value: jwtSecret
  }
}

// Lets the API's managed identity read secrets — matches the DefaultAzureCredential
// pattern already used by Infrastructure/Storage/AzureBlobStorage.cs.
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiIdentity.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // Key Vault Secrets User
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

// --- Data ------------------------------------------------------------------------------

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: postgresDatabaseName
}

// Allows Azure services (the App Service backend) to reach the server without a private
// network — acceptable for this project's scale; a production-grade setup would use
// VNet integration + a delegated subnet instead.
resource postgresAllowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: false
    accessTier: 'Hot'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource learningMaterialsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'learning-materials'
  properties: {
    publicAccess: 'None'
  }
}

// Matches the DefaultAzureCredential-based access already implemented in
// Infrastructure/Storage/AzureBlobStorage.cs — no account key or connection string needed.
resource storageBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiIdentity.id, 'StorageBlobDataContributor')
  scope: storageAccount
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // Storage Blob Data Contributor
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

// --- Compute -----------------------------------------------------------------------------

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${resourcePrefix}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource apiApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${resourcePrefix}-api'
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        { name: 'AZURE_CLIENT_ID', value: apiIdentity.properties.clientId }
        { name: 'ConnectionStrings__DefaultConnection', value: 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=@Microsoft.KeyVault(SecretUri=${postgresPasswordSecret.properties.secretUri})' }
        { name: 'Jwt__Secret', value: '@Microsoft.KeyVault(SecretUri=${jwtSecretSecret.properties.secretUri})' }
        { name: 'Storage__Provider', value: 'AzureBlob' }
        { name: 'AzureBlobStorage__AccountUrl', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'AzureBlobStorage__ContainerName', value: learningMaterialsContainer.name }
        { name: 'AllowedOrigins__0', value: frontendOrigin }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      ]
    }
  }
}

resource frontendApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: '${resourcePrefix}-web'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output frontendUrl string = 'https://${frontendApp.properties.defaultHostname}'
output postgresServerFqdn string = postgresServer.properties.fullyQualifiedDomainName
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
