using 'main.bicep'

param environmentName = 'dev'
param location = 'eastus'
param postgresAdminLogin = 'studentconnect_admin'

// Provide real values at deploy time — do not commit secrets:
//   az deployment group create -g <rg> -f main.bicep -p main.bicepparam \
//     -p postgresAdminPassword=<secret> -p jwtSecret=<secret> -p frontendOrigin=<url>
param postgresAdminPassword = ''
param jwtSecret = ''
param frontendOrigin = 'https://studentconnect-dev-web.azurestaticapps.net'
