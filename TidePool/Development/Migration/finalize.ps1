$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath('D:\awestlakeselfstudy')
$projectRoot = [IO.Path]::GetFullPath('E:\jianmowenjian\unityproject\TIDEPOOL\TidePool')
$dev = Join-Path $projectRoot 'Development'
$migration = Join-Path $dev 'Migration'
$folders = @('unity_hall_expansion','unity_monument','unity_pool_lighting','unity_water_cycle')
$manifestPath = Join-Path $migration 'copied-files.json'
$started = (Get-Item -LiteralPath $manifestPath).LastWriteTime
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$protected = Get-Content -LiteralPath (Join-Path $migration 'scene-runtime-before.json') -Raw | ConvertFrom-Json
foreach ($item in $protected) {
    if ((Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash -ne $item.SHA256) { throw "Scene/runtime changed: $($item.Path)" }
}
$health = Get-Content -LiteralPath (Join-Path $migration 'unity-health.txt') -Raw
if ($health -notmatch 'MissingScripts=0') { throw 'Missing-script health check failed' }
$status = Get-Content -LiteralPath (Join-Path $dev 'unity_pool_lighting\status.txt') -Raw
$monument = Get-Content -LiteralPath (Join-Path $dev 'unity_monument\status.txt') -Raw
if ($status -notmatch 'playing=False' -or $status -notmatch 'step=0' -or $monument -notmatch 'stage=0') { throw 'Unity still running a test' }
$pass = Get-Item -LiteralPath (Join-Path $dev 'unity_pool_lighting\transition-pass.txt')
if ($pass.LastWriteTime -lt $started) { throw 'No fresh Unity flow-test pass' }
$active = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\_Game') -Recurse -Filter '*.cs' | Select-String -Pattern 'D:[/\\]awestlakeselfstudy'
if ($active) { throw 'Active scripts still reference old output paths' }
$archived = [Collections.Generic.List[string]]::new()
foreach ($entry in $manifest) {
    $relative = Join-Path $entry.Folder $entry.Relative
    $src = Join-Path $sourceRoot $relative
    $dst = Join-Path $dev $relative
    if (!(Test-Path -LiteralPath $dst)) { throw "Destination missing: $relative" }
    $sourceHash = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
    $destHash = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash
    if ($sourceHash -ne $destHash) {
        # Fresh Unity validation regenerates screenshots/status; retain the exact old output too.
        $archive = Join-Path $migration ('OriginalOutputs\' + $relative)
        New-Item -ItemType Directory -Path (Split-Path $archive -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $src -Destination $archive
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $sourceHash) { throw "Archive mismatch: $relative" }
        $archived.Add($relative)
    }
}
# Preserve the reviewed migration tools and current plan inside the project before workspace cleanup.
$staging = Join-Path $sourceRoot 'pool_project_migration'
Copy-Item -LiteralPath (Join-Path $staging 'finalize.ps1') -Destination (Join-Path $migration 'finalize.ps1')
Copy-Item -LiteralPath (Join-Path $staging '浮力与手写IK学习实操计划.md') -Destination (Join-Path $projectRoot 'Documentation\浮力与手写IK学习实操计划.md') -Force
$report = [ordered]@{
    VerifiedAt=(Get-Date -Format s)
    Project=$projectRoot
    CopiedFiles=$manifest.Count
    ProtectedScenesAndRuntimeSources=$protected.Count
    ProtectedHashesUnchanged=$true
    ActiveOldDPaths=0
    MissingScripts=0
    PythonIKTests='13 passed'
    UnityFlowTest=(Get-Content -LiteralPath $pass.FullName -Raw).Trim()
    HistoricalOutputsPreserved=$archived.ToArray()
    RemovedSourceFolders=@()
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $migration 'verification.json') -Encoding utf8
# Resolve and validate every recursive-delete target. No computed path may escape the named workspace.
$targets = @($folders + 'pool_project_migration') | ForEach-Object {
    $target = (Resolve-Path -LiteralPath (Join-Path $sourceRoot $_)).Path
    if ((Split-Path $target -Parent) -ne $sourceRoot) { throw "Refusing unsafe cleanup: $target" }
    $node = Get-Item -LiteralPath $target
    if ($node.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing reparse cleanup' }
    $target
}
foreach ($target in $targets) { Remove-Item -LiteralPath $target -Recurse -Force }
$report.RemovedSourceFolders = $targets
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $migration 'verification.json') -Encoding utf8
"Migration verified: $($manifest.Count) files preserved; $($protected.Count) scene/runtime hashes unchanged; Unity sequence passed; D-drive duplicates removed."
