// StudentConnect on Azure Container Instances (ACI) — the simplest "just run my containers"
// option: one container group runs db + api + frontend together, closest in spirit to
// `docker compose up` but with no VM to patch. Provided for completeness/comparison; NOT
// recommended beyond a quick demo/proof-of-concept because ACI has no built-in ingress,
// TLS/custom-domain, or load balancing — you'd need to bolt on Application Gateway or Front
// Door for real HTTPS, which works against the "few services" goal this comparison is for.
//
// Resource count: 1 Storage Account (Azure Files, for Postgres data persistence) +
// 1 Container Registry (api/frontend images only — postgres is pulled from Docker Hub
// directly) + 1 Container Group + 1 user-assigned identity for ACR pull = 4 resource types.
//
// Cost note: ACI bills per vCPU-second/GiB-second for the whole group, all three containers,
// 24/7 (no scale-to-zero for a restartPolicy=Always group) — roughly $25-30/month for the
// small sizes below, comparable to the VM option but with none of a VM's flexibility.
//
// IMPORTANT deployment nuance: containers in an ACI group share ONE network namespace/IP —
// there is no per-container DNS name like Docker Compose provides. frontend/nginx.conf's
// `proxy_pass http://api:8080/api/;` must become `proxy_pass http://localhost:8080/api/;`
// before building the frontend image for this target.
//
// Images must exist in the registry before the group can start — build & push first:
//   az acr build -r <acrName> -g <rg> -t studentconnect-api:latest -f backend/StudentConnect.Api/Dockerfile backend
//   az acr build -r <acrName> -g <rg> -t studentconnect-frontend:latest frontend
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1 (<300 students, ~5-15 concurrent):  1 vCPU/1Gi per container (the defaults below)
//     — good for a demo/proof-of-concept, not a real pilot deployment.
//   Tier 2/3: NOT supported by this model — one container group is fundamentally one
//     instance of everything, including Postgres; migrate to vm.bicep or an App
//     Service/Container Apps option before outgrowing Tier 1.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f container-instances.bicep -p container-instances.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>

@description('Short environment name used in resource names, e.g. dev, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Globally-unique DNS label for the public IP, e.g. "studentconnect-dev" -> studentconnect-dev.eastus.azurecontainer.io.')
param dnsLabel string

@secure()
@description('PostgreSQL superuser password (runs inside the container group, not a managed service, for this option).')
param postgresAdminPassword string

@secure()
@description('Signing secret for the app\'s local JWT authentication.')
param jwtSecret string

@description('Container image tag to deploy for both api and frontend (must already exist in the registry).')
param imageTag string = 'latest'

var resourcePrefix = 'studentconnect-${environmentName}'
var acrName = toLower(replace('stuconnacr${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))
var storageAccountName = toLower(replace('stuconn${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))
var fileShareName = 'postgres-data'
var postgresDatabaseName = 'studentconnect_db'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource groupIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-aci-identity'
  location: location
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, groupIdentity.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    principalId: groupIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // AcrPull
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: true
  }
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource postgresShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileServices
  name: fileShareName
  properties: { shareQuota: 32 }
}

resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: '${resourcePrefix}-group'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${groupIdentity.id}': {} }
  }
  properties: {
    osType: 'Linux'
    restartPolicy: 'Always'
    imageRegistryCredentials: [
      { server: containerRegistry.properties.loginServer, identity: groupIdentity.id }
    ]
    ipAddress: {
      type: 'Public'
      dnsNameLabel: dnsLabel
      ports: [ { protocol: 'Tcp', port: 80 } ]
    }
    volumes: [
      {
        name: 'postgres-data'
        azureFile: {
          shareName: fileShareName
          storageAccountName: storageAccount.name
          storageAccountKey: storageAccount.listKeys().keys[0].value
        }
      }
    ]
    containers: [
      {
        name: 'db'
        properties: {
          image: 'postgres:16-alpine'
          resources: { requests: { cpu: 1, memoryInGB: 1 } }
          environmentVariables: [
            { name: 'POSTGRES_DB', value: postgresDatabaseName }
            { name: 'POSTGRES_USER', value: 'studentconnect' }
            { name: 'POSTGRES_PASSWORD', secureValue: postgresAdminPassword }
          ]
          volumeMounts: [
            { name: 'postgres-data', mountPath: '/var/lib/postgresql/data' }
          ]
        }
      }
      {
        name: 'api'
        properties: {
          image: '${containerRegistry.properties.loginServer}/studentconnect-api:${imageTag}'
          resources: { requests: { cpu: 1, memoryInGB: 1 } }
          environmentVariables: [
            { name: 'ConnectionStrings__DefaultConnection', value: 'Host=localhost;Port=5432;Database=${postgresDatabaseName};Username=studentconnect;Password=${postgresAdminPassword};SSL Mode=Disable' }
            { name: 'Jwt__Secret', secureValue: jwtSecret }
            { name: 'Jwt__Issuer', value: 'StudentConnect' }
            { name: 'Jwt__Audience', value: 'StudentConnect' }
            { name: 'Storage__Provider', value: 'Local' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      }
      {
        name: 'frontend'
        properties: {
          image: '${containerRegistry.properties.loginServer}/studentconnect-frontend:${imageTag}'
          resources: { requests: { cpu: 1, memoryInGB: 1 } }
          ports: [ { protocol: 'Tcp', port: 80 } ]
        }
      }
    ]
  }
}

output fqdn string = containerGroup.properties.ipAddress.fqdn
output appUrl string = 'http://${containerGroup.properties.ipAddress.fqdn}'
output acrLoginServer string = containerRegistry.properties.loginServer
