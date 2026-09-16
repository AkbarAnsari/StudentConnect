using 'app-service-compose.bicep'

param environmentName = 'dev'
param location = 'eastus'
param appServicePlanSku = 'B1'
param postgresAdminLogin = 'studentconnect_admin'

// Provide real values at deploy time — do not commit secrets:
//   az deployment group create -g <rg> -f app-service-compose.bicep -p app-service-compose.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret>
param postgresAdminPassword = ''
param jwtSecret = ''

param imageTag = 'latest'
