// StudentConnect — minimal-footprint, cost-optimized Azure deployment.
//
// This is the RECOMMENDED default deployment path: a single small Linux VM running the
// exact same docker-compose stack used for local development (Postgres + API + Angular/
// nginx, all three containers on one box). It deliberately avoids Azure Database for
// PostgreSQL, App Service, Static Web Apps, Key Vault, Storage Account, Managed Identity,
// and Log Analytics/App Insights — one VM is enough for a pilot-scale deployment and keeps
// both the Azure bill and the number of services to operate to a minimum.
//
// `main.bicep` remains in this folder as an alternative if/when you outgrow a single VM and
// want managed PaaS services (HA Postgres, autoscaling App Service, etc).
//
// Sizing by load tier (see README.md "Load tiers used below" for the full reasoning):
//   Tier 1 (<300 students, ~5-15 concurrent):  vmSize=Standard_B1ms (default below)
//   Tier 2 (300-2,000 students, ~30-100):      vmSize=Standard_B2s or Standard_B2ms
//   Tier 3 (2,000+ students, 200+):            not recommended — a single VM has no
//     horizontal scale-out story; move to an App Service/Container Apps option instead.
//
// Networking is kept to the minimum needed for the VM to exist AND actually be reachable:
// a VNet, a subnet, an NSG, a NIC, and a public IP. The NSG is NOT optional/deferrable —
// confirmed via a real deployment: Standard SKU public IPs (used here; Basic SKU is being
// retired by Azure) are "secure by default" and silently DENY all inbound internet traffic
// unless an NSG explicitly allows it, and some subscriptions additionally have a governance
// policy that auto-attaches a deny-all NSG to any subnet that doesn't already have one. Both
// behaviors make the site completely unreachable (connection times out, not "refused") with
// no NSG at all, regardless of what cloud-init's `ufw` allows at the OS level. Only the
// public IP is separately billed (~$3.65/month for a Standard static IP) — the NSG itself is
// free.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f vm.bicep -p vm.bicepparam \
//     -p sshPublicKey="$(Get-Content ~/.ssh/id_ed25519.pub)"

@description('Short environment name used in resource names, e.g. dev, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('VM size. Standard_B1ms (2 GiB RAM) is the cheapest size comfortable for Postgres + API + nginx together; Standard_B2s gives more headroom.')
param vmSize string = 'Standard_B1ms'

@description('Admin username for SSH access.')
param adminUsername string = 'azureuser'

@description('Public key contents (e.g. ~/.ssh/id_ed25519.pub) — password auth is disabled.')
@secure()
param sshPublicKey string

@description('Size of the data disk (GB) used to persist Postgres data and uploaded files, independent of the OS disk lifecycle.')
param dataDiskSizeGB int = 32

@description('Optional globally-unique DNS label for the public IP, e.g. "studentconnect-dev" -> studentconnect-dev.eastus.cloudapp.azure.com. Leave empty to skip.')
param dnsLabelPrefix string = ''

@description('Provision a Recovery Services Vault + daily backup policy for the VM (adds a small monthly cost, ~$5).')
param enableBackup bool = false

var resourcePrefix = 'studentconnect-${environmentName}'

// --- Networking (minimal: 1 VNet, 1 subnet with an NSG, 1 static public IP) ---------------

resource nsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${resourcePrefix}-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowSsh'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '22'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowHttpHttps'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRanges: ['80', '443']
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${resourcePrefix}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.20.0.0/24']
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '10.20.0.0/27'
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
    ]
  }
}

resource publicIp 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: '${resourcePrefix}-ip'
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    dnsSettings: empty(dnsLabelPrefix) ? null : {
      domainNameLabel: dnsLabelPrefix
    }
  }
}

resource nic 'Microsoft.Network/networkInterfaces@2024-05-01' = {
  name: '${resourcePrefix}-nic'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: vnet.properties.subnets[0].id
          }
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
  }
}

// --- Compute -----------------------------------------------------------------------------

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: '${resourcePrefix}-vm'
  location: location
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    osProfile: {
      computerName: '${resourcePrefix}-vm'
      adminUsername: adminUsername
      linuxConfiguration: {
        disablePasswordAuthentication: true
        ssh: {
          publicKeys: [
            {
              path: '/home/${adminUsername}/.ssh/authorized_keys'
              keyData: sshPublicKey
            }
          ]
        }
      }
      customData: loadFileAsBase64('cloud-init.yaml')
    }
    storageProfile: {
      imageReference: {
        publisher: 'Canonical'
        offer: '0001-com-ubuntu-server-jammy'
        sku: '22_04-lts-gen2'
        version: 'latest'
      }
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: 'StandardSSD_LRS'
        }
        diskSizeGB: 30
      }
      dataDisks: [
        {
          lun: 0
          createOption: 'Empty'
          diskSizeGB: dataDiskSizeGB
          managedDisk: {
            storageAccountType: 'StandardSSD_LRS'
          }
        }
      ]
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
        }
      ]
    }
  }
}

// --- Optional backup (off by default — keep service count to zero unless opted in) -----

resource recoveryVault 'Microsoft.RecoveryServices/vaults@2024-04-01' = if (enableBackup) {
  name: '${resourcePrefix}-rsv'
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {}
}

resource backupPolicy 'Microsoft.RecoveryServices/vaults/backupPolicies@2024-04-01' = if (enableBackup) {
  parent: recoveryVault
  name: 'daily-vm-backup'
  properties: {
    backupManagementType: 'AzureIaasVM'
    schedulePolicy: {
      schedulePolicyType: 'SimpleSchedulePolicy'
      scheduleRunFrequency: 'Daily'
      scheduleRunTimes: ['2024-01-01T02:00:00Z']
    }
    retentionPolicy: {
      retentionPolicyType: 'LongTermRetentionPolicy'
      dailySchedule: {
        retentionTimes: ['2024-01-01T02:00:00Z']
        retentionDuration: {
          count: 14
          durationType: 'Days'
        }
      }
    }
  }
}

resource backupFabric 'Microsoft.RecoveryServices/vaults/backupFabrics/protectionContainers/protectedItems@2024-04-01' = if (enableBackup) {
  name: '${recoveryVault.name}/Azure/iaasvmcontainer;iaasvmcontainerv2;${resourceGroup().name};${vm.name}/vm;iaasvmcontainerv2;${resourceGroup().name};${vm.name}'
  properties: {
    protectedItemType: 'Microsoft.Compute/virtualMachines'
    policyId: backupPolicy.id
    sourceResourceId: vm.id
  }
}

output publicIpAddress string = publicIp.properties.ipAddress
output fqdn string = empty(dnsLabelPrefix) ? '' : publicIp.properties.dnsSettings.fqdn
output sshCommand string = 'ssh ${adminUsername}@${publicIp.properties.ipAddress}'
