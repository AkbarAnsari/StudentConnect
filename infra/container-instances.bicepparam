using 'container-instances.bicep'

param environmentName = 'dev'
param location = 'eastus'

// Must be globally unique per region.
param dnsLabel = 'studentconnect-dev'

// Provide real values at deploy time — do not commit secrets:
//   az deployment group create -g <rg> -f container-instances.bicep -p container-instances.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>
param postgresAdminPassword = ''
param jwtSecret = ''

param imageTag = 'latest'
