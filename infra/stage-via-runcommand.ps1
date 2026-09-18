param(
    [string]$ResourceGroup = "studentconnect-vm-rg",
    [string]$VmName = "studentconnect-dev-vm",
    [string]$TarPath = "$env:TEMP\studentconnect-deploy.tar.gz",
    # 200,000 chars reliably fails silently (empty result, no error) against this VM's Run
    # Command agent; 180,000 reliably works. Staying well under that with margin for the
    # surrounding heredoc wrapper text.
    [int]$ChunkChars = 150000
)

$ErrorActionPreference = "Stop"
$chunkDir = "$env:TEMP\sc-chunks"
Remove-Item -Recurse -Force $chunkDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $chunkDir | Out-Null

Write-Host "Reading and base64-encoding $TarPath..."
$bytes = [IO.File]::ReadAllBytes($TarPath)
$b64 = [Convert]::ToBase64String($bytes)
$total = [Math]::Ceiling($b64.Length / $ChunkChars)
Write-Host "Total base64 length: $($b64.Length) chars, $total chunks of ~$ChunkChars chars each"

# Reset the remote payload file.
$resetScript = "$chunkDir\reset.txt"
"rm -f /tmp/payload.b64; touch /tmp/payload.b64" | Set-Content -Path $resetScript -NoNewline
az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$resetScript" -o none
if ($LASTEXITCODE -ne 0) { throw "Failed to reset remote payload file (exit $LASTEXITCODE)." }
Write-Host "Remote payload file reset."

for ($i = 0; $i -lt $total; $i++) {
    $start = $i * $ChunkChars
    $len = [Math]::Min($ChunkChars, $b64.Length - $start)
    $chunk = $b64.Substring($start, $len)
    $scriptFile = "$chunkDir\chunk_$i.txt"
    $content = "cat >> /tmp/payload.b64 << 'CHUNKEOF'`n$chunk`nCHUNKEOF"
    Set-Content -Path $scriptFile -Value $content -NoNewline
    az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$scriptFile" -o none
    if ($LASTEXITCODE -ne 0) { throw "Chunk $($i+1)/$total failed to send (exit $LASTEXITCODE)." }
    Write-Host "Sent chunk $($i+1)/$total"
}

Write-Host "Verifying remote payload size before extracting..."
$expectedLen = $b64.Length
$sizeCheckScript = "$chunkDir\sizecheck.txt"
"wc -c < /tmp/payload.b64" | Set-Content -Path $sizeCheckScript -NoNewline
$remoteSizeRaw = az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$sizeCheckScript" --query "value[0].message" -o tsv
$remoteSizeRaw = ($remoteSizeRaw -join "`n")
Write-Host "Remote size report: $remoteSizeRaw"
if ($remoteSizeRaw -notmatch "(\d+)") {
    throw "Could not parse remote payload size from: $remoteSizeRaw"
}
# The remote file has one trailing newline appended per chunk, so it will be
# $expectedLen + $total bytes larger than the raw base64 string length — verify at least
# $expectedLen bytes made it across rather than an exact match.
$remoteSize = [int]$Matches[1]
if ($remoteSize -lt $expectedLen) {
    throw "Remote payload is too small: got $remoteSize bytes, expected at least $expectedLen. Transfer is corrupt — do not extract."
}
Write-Host "Remote payload size OK ($remoteSize bytes, expected >= $expectedLen)."

Write-Host "Decoding and extracting on the VM..."
$extractScript = "$chunkDir\extract.txt"
@"
set -e
tr -d '\n' < /tmp/payload.b64 > /tmp/payload.clean.b64
base64 -d /tmp/payload.clean.b64 > /tmp/studentconnect-deploy.tar.gz
mkdir -p /opt/studentconnect
tar xzf /tmp/studentconnect-deploy.tar.gz -C /opt/studentconnect
rm -f /tmp/studentconnect-deploy.tar.gz /tmp/payload.b64 /tmp/payload.clean.b64
ls -la /opt/studentconnect
"@ | Set-Content -Path $extractScript -NoNewline
az vm run-command invoke -g $ResourceGroup -n $VmName --command-id RunShellScript --scripts "@$extractScript"
if ($LASTEXITCODE -ne 0) { throw "Extraction failed on the VM (exit $LASTEXITCODE)." }

Remove-Item -Recurse -Force $chunkDir -ErrorAction SilentlyContinue
Write-Host "Done."
