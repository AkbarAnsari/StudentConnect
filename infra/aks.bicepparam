using 'aks.bicep'

param environmentName = 'dev'
param location = 'eastus'
param nodeVmSize = 'Standard_B2s'
param nodeCount = 1
param kubernetesVersion = ''
