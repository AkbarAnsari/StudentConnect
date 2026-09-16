using 'vm.bicep'

param environmentName = 'dev'
param location = 'eastus'
param vmSize = 'Standard_B1ms'
param adminUsername = 'azureuser'

// Provide real values at deploy time — do not commit secrets or your real IP:
//   az deployment group create -g <rg> -f vm.bicep -p vm.bicepparam \
//     -p sshPublicKey="$(Get-Content ~/.ssh/id_ed25519.pub -Raw)" \
//     -p sshSourceAddressPrefix="<your-public-ip>/32"
param sshPublicKey = ''
param sshSourceAddressPrefix = ''

// Optional: set to a globally-unique label to get <label>.<region>.cloudapp.azure.com
param dnsLabelPrefix = ''

param dataDiskSizeGB = 32
param enableBackup = false
