param(
    [string]$ResourceGroup = "studentconnect-vm-rg",
    [string]$VmName = "studentconnect-dev-vm",
    [string]$LocalDbContainer = "student-portal-db-1",
    [string]$LocalDbUser = "studentconnect",
    [string]$LocalDbName = "studentconnect_db",
    [string]$RemoteComposeDir = "/opt/studentconnect",
    [string]$RemoteDbService = "db",
    [string]$RemoteApiService = "api",
    [int]$ChunkChars = 150000
)

# Migrates the LOCAL docker-compose Postgres database to the Azure VM's docker-compose
# Postgres database, entirely through `az vm run-command` (no ssh/scp required — see
# infra/stage-via-runcommand.ps1's header/README/memory notes for why). Does a full
# --clean --if-exists restore, i.e. this REPLACES whatever data currently exists on the VM
# with the local database's current contents. Stops the api container during the restore to
# avoid serving stale/partial reads mid-swap, then restarts it.

$ErrorActionPreference = "Stop"
$chunkDir = "$env:TEMP\sc-data-migration"
Remove-Item -Recurse -Force $chunkDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $chunkDir | Out-Null

Write-Host "Dumping local database ($LocalDbContainer / $LocalDbName)..."
docker exec $LocalDbContainer pg_dump -U $LocalDbUser -d $LocalDbName --clean --if-exists -Fc -f /tmp/studentconnect_db.dump
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }
$dumpPath = "$chunkDir\studentconnect_db.dump"
docker cp "${LocalDbContainer}:/tmp/studentconnect_db.dump" $dumpPath
if (-not (Test-Path $dumpPath)) { throw "Failed to copy dump out of the local container." }
$dumpSize = (Get-Item $dumpPath).Length
Write-Host "Dump size: $dumpSize bytes"

Write-Host "Base64-encoding and chunking..."
$bytes = [IO.File]::ReadAllBytes($dumpPath)
$b64 = [Convert]::ToBase64String($bytes)
$total = [Math]::Ceiling($b64.Length / $ChunkChars)
Write-Host "Total base64 length: $($b64.Length) chars, $total chunk(s)"

$resetScript = "$chunkDir\reset.txt"
"rm -f /tmp/dump.b64; touch /tmp/dump.b64" | Set-Content -Path $resetScript -NoNewline
az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$resetScript" -o none
if ($LASTEXITCODE -ne 0) { throw "Failed to reset remote payload file." }

for ($i = 0; $i -lt $total; $i++) {
    $start = $i * $ChunkChars
    $len = [Math]::Min($ChunkChars, $b64.Length - $start)
    $chunk = $b64.Substring($start, $len)
    $scriptFile = "$chunkDir\chunk_$i.txt"
    $content = "cat >> /tmp/dump.b64 << 'CHUNKEOF'`n$chunk`nCHUNKEOF"
    Set-Content -Path $scriptFile -Value $content -NoNewline
    az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$scriptFile" -o none
    if ($LASTEXITCODE -ne 0) { throw "Chunk $($i+1)/$total failed to send." }
    Write-Host "Sent chunk $($i+1)/$total"
}

Write-Host "Verifying remote payload size..."
$sizeCheckScript = "$chunkDir\sizecheck.txt"
"wc -c < /tmp/dump.b64" | Set-Content -Path $sizeCheckScript -NoNewline
$remoteSizeRaw = az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$sizeCheckScript" --query "value[0].message" -o tsv
$remoteSizeRaw = ($remoteSizeRaw -join "`n")
if ($remoteSizeRaw -notmatch "(\d+)") { throw "Could not parse remote size from: $remoteSizeRaw" }
$remoteSize = [int]$Matches[1]
if ($remoteSize -lt $b64.Length) {
    throw "Remote payload too small: got $remoteSize, expected at least $($b64.Length). Aborting before touching the live database."
}
Write-Host "Remote payload size OK ($remoteSize bytes)."

Write-Host "Decoding, copying into the db container, stopping api, restoring, restarting api..."
$restoreScript = "$chunkDir\restore.txt"
@"
set -e
tr -d '\n' < /tmp/dump.b64 > /tmp/dump.clean.b64
base64 -d /tmp/dump.clean.b64 > /tmp/studentconnect_db.dump
cd $RemoteComposeDir
docker compose -f docker-compose.prod.yml stop $RemoteApiService
docker cp /tmp/studentconnect_db.dump `$(docker compose -f docker-compose.prod.yml ps -q $RemoteDbService):/tmp/studentconnect_db.dump
docker compose -f docker-compose.prod.yml exec -T $RemoteDbService pg_restore -U studentconnect -d studentconnect_db --clean --if-exists /tmp/studentconnect_db.dump || true
docker compose -f docker-compose.prod.yml start $RemoteApiService
rm -f /tmp/dump.b64 /tmp/dump.clean.b64 /tmp/studentconnect_db.dump
docker compose -f docker-compose.prod.yml ps
"@ | Set-Content -Path $restoreScript -NoNewline
az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$restoreScript"

Remove-Item -Recurse -Force $chunkDir -ErrorAction SilentlyContinue
Write-Host "Done."
