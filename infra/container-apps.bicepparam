using 'container-apps.bicep'

param environmentName = 'dev'
param location = 'eastus'
param postgresAdminLogin = 'studentconnect_admin'

// Provide real values at deploy time — do not commit secrets:
//   az deployment group create -g <rg> -f container-apps.bicep -p container-apps.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>
param postgresAdminPassword = ''
param jwtSecret = ''

param imageTag = 'latest'
