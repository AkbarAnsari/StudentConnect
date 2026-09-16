// StudentConnect on Azure Kubernetes Service (AKS) — provided for completeness only. NOT
// recommended for this app's scale: a real k8s deployment needs an ingress controller,
// cert-manager (or similar) for TLS, and Kubernetes manifests (Deployment/Service/Ingress)
// for db/api/frontend that are NOT included here — this file provisions the CLUSTER only.
// Every other option in this folder (VM, Container Apps, App Service, ACI) reaches the same
// outcome with far less to operate. Reach for this only if you already run other workloads
// on Kubernetes and want to consolidate, or specifically need k8s-native tooling.
//
// Resource count if you stop here: 1 AKS cluster (control plane, free tier) + 1 node pool
// (billed as regular VMs) + 1 Container Registry + role assignment. Still need to add,
// separately: an ingress controller, TLS certificates, and application manifests before
// anything is actually reachable.
//
// Cost note: the control plane is free (Free tier); you pay for the node VMs 24/7 regardless
// of app traffic. Even a single Standard_B2s node is roughly $30/month — MORE than the VM
// option for equivalent compute, before you've deployed anything, because AKS assumes a node
// pool sized for multiple workloads/replicas, not one small app.
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1/2: not worth it — every other option here reaches the same outcome with far less
//     to operate; even one Standard_B2s node costs more than the equivalent VM/App Service
//     tier before you've deployed anything.
//   Tier 3 (2,000+ students, 200+, the only tier this makes sense for): 3+ Standard_D2s_v3+
//     nodes for real HA, a Deployment+HorizontalPodAutoscaler for stateless web replicas, and
//     a separate single-replica Deployment for the background workers (same constraint as
//     every other option, expressed as Kubernetes manifests instead of Bicep).
//
// Deploy with:
//   az deployment group create -g <resource-group> -f aks.bicep -p aks.bicepparam

@description('Short environment name used in resource names, e.g. dev, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('VM size for the (single, by default) system node pool.')
param nodeVmSize string = 'Standard_B2s'

@description('Number of nodes in the system node pool. Use at least 2-3 for any real workload; 1 is only for the cheapest possible evaluation.')
param nodeCount int = 1

@description('Kubernetes version. Leave empty to use AKS\'s current default.')
param kubernetesVersion string = ''

var resourcePrefix = 'studentconnect-${environmentName}'
var acrName = toLower(replace('stuconnacr${environmentName}${uniqueString(resourceGroup().id)}', '-', ''))

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource aks 'Microsoft.ContainerService/managedClusters@2024-05-01' = {
  name: '${resourcePrefix}-aks'
  location: location
  sku: {
    name: 'Base'
    tier: 'Free'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: '${resourcePrefix}-aks'
    kubernetesVersion: empty(kubernetesVersion) ? null : kubernetesVersion
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        count: nodeCount
        vmSize: nodeVmSize
        osType: 'Linux'
        osDiskSizeGB: 30
        type: 'VirtualMachineScaleSets'
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
    }
  }
}

// AKS's own kubelet identity (auto-created) needs AcrPull so nodes can pull images without
// storing registry credentials in Kubernetes secrets.
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, aks.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
    // AcrPull
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

output clusterName string = aks.name
output acrLoginServer string = containerRegistry.properties.loginServer
output getCredentialsCommand string = 'az aks get-credentials -g ${resourceGroup().name} -n ${aks.name}'
