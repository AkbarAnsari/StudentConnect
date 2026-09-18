param(
    [string]$ResourceGroup = "studentconnect-vm-rg",
    [string]$VmName = "studentconnect-dev-vm",
    [switch]$SkipBuild
)

# Redeploys ONLY the frontend container to the Azure VM — for when you've changed
# Angular/nginx source and don't need to touch the API, database, or .env. Ships the build
# entirely through `az vm run-command` (see infra/stage-via-runcommand.ps1's header/README
# for why: direct ssh/scp may be blocked by your network even though the VM itself is fine),
# so no SSH key or network path to the VM's port 22 is required — only `az` CLI access.
#
# Usage:
#   ./infra/redeploy-frontend-azure.ps1
#   ./infra/redeploy-frontend-azure.ps1 -SkipBuild   # reuse frontend/dist as-is
#
# For a backend-only change, the same pattern applies: package
# backend/{StudentConnect.Api/Dockerfile,publish,database} instead of frontend/{...}, and
# target the `api` service in the final `docker compose up --build -d` step.

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $SkipBuild) {
    Write-Host "==> Building frontend (npm run build)..." -ForegroundColor Cyan
    Push-Location frontend
    npm run build
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "npm run build failed." }
    Pop-Location
}

Write-Host "==> Packaging frontend/{Dockerfile,dist,nginx.conf}..." -ForegroundColor Cyan
$staging = "$env:TEMP\studentconnect-frontend-only"
$tarPath = "$env:TEMP\studentconnect-frontend-only.tar.gz"
Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
Remove-Item -Force $tarPath -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$staging\frontend" | Out-Null
Copy-Item frontend\Dockerfile "$staging\frontend\Dockerfile"
Copy-Item -Recurse frontend\dist "$staging\frontend\dist"
Copy-Item frontend\nginx.conf "$staging\frontend\nginx.conf"
tar -czf $tarPath -C $staging .
if (-not (Test-Path $tarPath)) { throw "Failed to create $tarPath." }
Write-Host "Packaged $((Get-Item $tarPath).Length) bytes."

Write-Host "==> Staging to the VM via az vm run-command (chunked, no ssh/scp needed)..." -ForegroundColor Cyan
& "$PSScriptRoot\stage-via-runcommand.ps1" -ResourceGroup $ResourceGroup -VmName $VmName -TarPath $tarPath

Write-Host "==> Rebuilding the frontend container on the VM..." -ForegroundColor Cyan
$rebuildScript = "$env:TEMP\studentconnect-rebuild-frontend.txt"
@"
cd /opt/studentconnect
docker compose -f docker-compose.prod.yml up --build -d frontend
docker compose -f docker-compose.prod.yml ps
"@ | Set-Content -Path $rebuildScript -NoNewline
az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$rebuildScript"
if ($LASTEXITCODE -ne 0) { throw "Rebuilding the frontend container failed." }

Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
Remove-Item -Force $tarPath, $rebuildScript -ErrorAction SilentlyContinue

Write-Host "Done. The frontend container has been rebuilt and restarted on the VM." -ForegroundColor Green
