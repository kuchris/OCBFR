param([string]$DalamudLibPath = '')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $DalamudLibPath) {
    $hooks = Get-ChildItem (Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'Dalamud.dll') } |
        Sort-Object @{Expression = { [Reflection.AssemblyName]::GetAssemblyName((Join-Path $_.FullName 'Dalamud.dll')).Version }; Descending = $true }
    $DalamudLibPath = ($hooks | Select-Object -First 1).FullName
}
if (-not $DalamudLibPath) { throw 'Dalamud references missing' }
$plugin = Join-Path $workspace 'src\bin\Release\net10.0-windows7.0\OCNFarmer.dll'
if (-not (Test-Path -LiteralPath $plugin)) { throw 'Build the release DLL first with Build.ps1' }
Push-Location $workspace
try {
    dotnet run --project (Join-Path $PSScriptRoot 'Verify') -c Release -- $plugin (Join-Path $workspace 'dependencies') $DalamudLibPath --regressions
    if ($LASTEXITCODE -ne 0) { throw 'Managed verification failed' }
} finally { Pop-Location }
