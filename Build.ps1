param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\build.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
