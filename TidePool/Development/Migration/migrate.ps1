$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath('D:\awestlakeselfstudy')
$projectRoot = [IO.Path]::GetFullPath('E:\jianmowenjian\unityproject\TIDEPOOL\TidePool')
$development = Join-Path $projectRoot 'Development'
$staging = Join-Path $sourceRoot 'pool_project_migration'
$folders = @('unity_hall_expansion','unity_monument','unity_pool_lighting','unity_water_cycle')
$pending = @('command.txt','camera-command.txt','letter-command.txt','polish-command.txt','unity-command.txt','dream-command.txt')
$map = [ordered]@{
    'HallCameraFixEditor.cs'='unity_water_cycle'
    'MonumentHallEditor.cs'='unity_monument'
    'PoolHallDreamEditor.cs'='unity_hall_expansion'
    'PoolHallExpansionEditor.cs'='unity_hall_expansion'
    'PoolHallLampEditor.cs'='unity_pool_lighting'
    'PoolLetterFlowEditor.cs'='unity_water_cycle'
    'PoolWaterPolishEditor.cs'='unity_water_cycle'
    'PoolWaterCycleEditor.cs'='unity_water_cycle'
}
if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'))) { throw 'Wrong Unity project root' }
if (Test-Path -LiteralPath $development) { throw 'Development already exists; inspect before rerunning migration' }
foreach ($folder in $folders) {
    $source = Join-Path $sourceRoot $folder
    if (!(Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        if ($file.Name -in $pending) { throw "Pending Unity command: $($file.FullName)" }
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected reparse point' }
    }
}
$state = Get-Content -LiteralPath (Join-Path $sourceRoot 'unity_monument\status.txt') -Raw
if ($state -notmatch 'playing=False' -or $state -notmatch 'stage=0') { throw "Unity must be idle: $state" }
New-Item -ItemType Directory -Path (Join-Path $development 'Migration\OriginalEditorSources') -Force | Out-Null
$manifest = [Collections.Generic.List[object]]::new()
foreach ($folder in $folders) {
    $source = Join-Path $sourceRoot $folder
    $dest = Join-Path $development $folder
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $rel = $file.FullName.Substring($source.Length + 1)
        $target = Join-Path $dest $rel
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
        $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        $volatile = $file.Name -match '(status|ready)' 
        if (!$volatile -and (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $hash) { throw "Copy mismatch: $rel" }
        $manifest.Add([pscustomobject]@{Folder=$folder;Relative=$rel;Bytes=$file.Length;SHA256=$hash;Volatile=$volatile})
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $development 'Migration\copied-files.json') -Encoding utf8
$protected = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\_Game') -Recurse -File |
    Where-Object { $_.Extension -eq '.unity' -or ($_.Extension -eq '.cs' -and $_.FullName -notmatch '\\Editor\\') }
$protectedHashes = @($protected | ForEach-Object { [pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
$protectedHashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $development 'Migration\scene-runtime-before.json') -Encoding utf8
$utf8 = [Text.UTF8Encoding]::new($false)
foreach ($entry in $map.GetEnumerator()) {
    $path = Join-Path $projectRoot ('Assets\_Game\Editor\' + $entry.Key)
    $text = [IO.File]::ReadAllText($path)
    $pattern = 'const string (Out|OutputRoot)\s*=\s*"D:/awestlakeselfstudy/' + $entry.Value + '";'
    if ([regex]::Matches($text,$pattern).Count -ne 1) { throw "Unexpected path declaration: $path" }
    Copy-Item -LiteralPath $path -Destination (Join-Path $development ('Migration\OriginalEditorSources\' + $entry.Key))
    $replacement = 'static readonly string $1 = TidePoolDevelopmentPaths.Folder("' + $entry.Value + '");'
    [IO.File]::WriteAllText($path, [regex]::Replace($text,$pattern,$replacement), $utf8)
}
Copy-Item -LiteralPath (Join-Path $staging 'TidePoolDevelopmentPaths.cs') -Destination (Join-Path $projectRoot 'Assets\_Game\Editor\TidePoolDevelopmentPaths.cs')
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Documentation') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $staging '浮力与手写IK学习实操计划.md') -Destination (Join-Path $projectRoot 'Documentation\浮力与手写IK学习实操计划.md')
Copy-Item -LiteralPath (Join-Path $staging 'Development-README.md') -Destination (Join-Path $development 'README.md')
Copy-Item -LiteralPath (Join-Path $staging 'migrate.ps1') -Destination (Join-Path $development 'Migration\migrate.ps1')
foreach ($item in $protectedHashes) { if ((Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash -ne $item.SHA256) { throw "Protected file changed: $($item.Path)" } }
# Request refresh through the already-running old editor bridge exactly once.
$refresh = Join-Path $sourceRoot 'unity_monument\command.pending'
[IO.File]::WriteAllText($refresh, 'refresh', [Text.Encoding]::ASCII)
Move-Item -LiteralPath $refresh -Destination (Join-Path $sourceRoot 'unity_monument\command.txt')
"Copied $($manifest.Count) files. Updated 8 editor output paths. Scenes and runtime sources unchanged. Waiting for Unity import."
