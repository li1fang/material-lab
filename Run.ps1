param(
    [ValidateSet('Prepare','Build','Campus','Test','Verify')][string]$Action = 'Build',
    [string]$Output = (Join-Path $PSScriptRoot 'Output')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Honour other workstreams' serial builds when checked out inside AI RTS.
if ($IsWindows -and (Test-Path "$repo/clients/unity/Get-EditorProcess.ps1")) {
    $deadline = (Get-Date).AddMinutes(15)
    do {
        $busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
            $_.ExecutablePath -like '*\Editor\Unity.exe' -and
            $_.CommandLine -match '(?i)-batchmode' -and
            $_.CommandLine.Replace('/','\').Contains($repo,[StringComparison]::OrdinalIgnoreCase)
        }
        if (!$busy) { break }
        if ((Get-Date) -gt $deadline) { throw 'Another repository batch editor is still running.' }
        Start-Sleep -Seconds 2
    } while ($true)
}
$work = [IO.Path]::GetFullPath($Output)
$project = Join-Path $work 'ExampleProject'
$package = Join-Path $project 'Packages/org.materiallab.destruction'
$sample = Join-Path $project 'Assets/BranchLab'
$env:MATERIAL_LAB_OUTPUT = $work
if (Test-Path "$project/Temp/UnityLockfile") {
    try {
        $handle = [IO.File]::Open("$project/Temp/UnityLockfile", 'Open', 'ReadWrite', 'None')
        $handle.Dispose()
    } catch [IO.IOException] { throw "Sample project is in use: $project" }
}
foreach ($dir in $package,$sample,"$project/Assets/Editor","$project/ProjectSettings") {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
# Embedded UPM package, just as installed by a downstream project.
Copy-Item "$PSScriptRoot/Package/*" $package -Recurse -Force
if (Test-Path "$PSScriptRoot/Package/Samples~/BranchLab") {
    Copy-Item "$PSScriptRoot/Package/Samples~/BranchLab/*" $sample -Recurse -Force
}
if (Test-Path "$PSScriptRoot/Host/BranchBuild.cs") {
    Copy-Item "$PSScriptRoot/Host/BranchBuild.cs" "$project/Assets/Editor/" -Force
}
$campus = Join-Path $project 'Assets/ModularCampus'
New-Item -ItemType Directory -Force $campus | Out-Null
Copy-Item "$PSScriptRoot/Package/Samples~/ModularCampus/*" $campus -Recurse -Force
Copy-Item "$PSScriptRoot/Host/manifest.json" "$project/Packages/" -Force
Copy-Item "$PSScriptRoot/Host/ProjectVersion.txt" "$project/ProjectSettings/" -Force
if (Test-Path "$PSScriptRoot/Host/packages-lock.json") {
    Copy-Item "$PSScriptRoot/Host/packages-lock.json" "$project/Packages/" -Force
}
if ($Action -eq 'Prepare') { return }
if ($Action -eq 'Test') {
    foreach ($mode in 'EditMode','PlayMode') {
        $xml = Join-Path $work ($mode.ToLowerInvariant() + '.xml')
        unity test $project --mode $mode --output $xml
        if ($LASTEXITCODE -ne 0) { throw "Tests failed. Check CLI output and $xml" }
        [xml]$report = Get-Content -Raw $xml
        if ([int]$report.'test-run'.passed -lt 1 -or [int]$report.'test-run'.failed -ne 0) { throw "Missing passing tests in $mode." }
    }
    return
}
$method = if ($Action -eq 'Campus') { 'BranchBuild.Campus' } elseif ($Action -eq 'Build') { 'BranchBuild.Windows' } else { 'BranchBuild.Verify' }
unity run $project --no-tail --log-file "$work/$Action.log" -- -nographics -executeMethod $method
if ($LASTEXITCODE -ne 0 -or !(Select-String -SimpleMatch "MATERIAL_LAB_OK $Action" "$work/$Action.log" -Quiet)) {
    throw "Unity $Action failed. See $work/$Action.log"
}
Get-Item "$work/$Action.log"
