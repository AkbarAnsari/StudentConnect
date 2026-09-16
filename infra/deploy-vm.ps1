#requires -Version 5.1
<#
.SYNOPSIS
    Builds StudentConnect locally and deploys it to the single-VM production host
    provisioned by infra/vm.bicep.

.DESCRIPTION
    Mirrors the local Docker workflow documented in README.md (host-build, then
    runtime-only container images) but ships the result to a remote VM over SSH/SCP
    instead of running docker compose locally. Safe to re-run: it only copies files and
    runs `docker compose up --build -d`, it never deletes remote data.

.PARAMETER VmHost
    Public IP or FQDN of the target VM (see the `publicIpAddress`/`fqdn` outputs of the
    vm.bicep deployment).

.PARAMETER SshUser
    SSH username on the VM. Defaults to azureuser (matches vm.bicep's default).

.PARAMETER SshKeyPath
    Path to the private key matching the public key deployed via vm.bicepparam.

.EXAMPLE
    ./infra/deploy-vm.ps1 -VmHost 20.1.2.3 -SshKeyPath ~/.ssh/id_ed25519
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$VmHost,

    [string]$SshUser = "azureuser",

    [Parameter(Mandatory = $true)]
    [string]$SshKeyPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Assert-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "'$name' was not found on PATH. Install the OpenSSH client (Windows Optional Features) and retry."
    }
}

Assert-Command "ssh"
Assert-Command "scp"

Write-Host "==> Building backend (dotnet publish)..." -ForegroundColor Cyan
dotnet publish backend/StudentConnect.Api/StudentConnect.Api.csproj -c Release -o backend/publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "==> Building frontend (npm run build)..." -ForegroundColor Cyan
Push-Location frontend
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "npm run build failed." }
Pop-Location

$remoteDir = "/opt/studentconnect"
$sshTarget = "$SshUser@$VmHost"
$sshArgs = @("-i", $SshKeyPath, "-o", "StrictHostKeyChecking=accept-new")

Write-Host "==> Ensuring remote directories exist..." -ForegroundColor Cyan
ssh @sshArgs $sshTarget "sudo mkdir -p $remoteDir/backend/StudentConnect.Api $remoteDir/backend/database $remoteDir/frontend && sudo chown -R ${SshUser}: $remoteDir"

Write-Host "==> Copying backend artifacts..." -ForegroundColor Cyan
scp @sshArgs backend/StudentConnect.Api/Dockerfile "${sshTarget}:$remoteDir/backend/StudentConnect.Api/Dockerfile"
scp @sshArgs -r backend/publish "${sshTarget}:$remoteDir/backend/"
scp @sshArgs -r backend/database "${sshTarget}:$remoteDir/backend/"

Write-Host "==> Copying frontend artifacts..." -ForegroundColor Cyan
scp @sshArgs -r frontend/Dockerfile "${sshTarget}:$remoteDir/frontend/Dockerfile"
scp @sshArgs -r frontend/dist "${sshTarget}:$remoteDir/frontend/"
scp @sshArgs frontend/nginx.conf "${sshTarget}:$remoteDir/frontend/nginx.conf"

Write-Host "==> Copying compose file..." -ForegroundColor Cyan
scp @sshArgs docker-compose.prod.yml "${sshTarget}:$remoteDir/docker-compose.prod.yml"

Write-Host "==> Checking for remote .env..." -ForegroundColor Cyan
$envExists = ssh @sshArgs $sshTarget "test -f $remoteDir/.env && echo yes || echo no"
if ($envExists.Trim() -eq "no") {
    Write-Warning "No .env found on the VM at $remoteDir/.env. Copy .env.example there, fill in real secrets, then re-run this script."
    Write-Host "  scp -i $SshKeyPath .env.example ${sshTarget}:$remoteDir/.env" -ForegroundColor Yellow
    exit 1
}

Write-Host "==> Starting the stack (docker compose up --build -d)..." -ForegroundColor Cyan
ssh @sshArgs $sshTarget "cd $remoteDir && sudo docker compose -f docker-compose.prod.yml up --build -d"

Write-Host "==> Deployment complete. Checking container status..." -ForegroundColor Cyan
ssh @sshArgs $sshTarget "cd $remoteDir && sudo docker compose -f docker-compose.prod.yml ps"

Write-Host "Done. Visit http://$VmHost" -ForegroundColor Green
