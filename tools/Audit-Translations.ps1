$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path (Join-Path $workspace 'artifacts') -Force | Out-Null
dotnet run --project (Join-Path $PSScriptRoot 'LocalizeSource') -- (Join-Path $workspace 'src\NorthIslandChestPlugin\Plugin.cs') --inventory > (Join-Path $workspace 'artifacts\ui-inventory.json')
if ($LASTEXITCODE -ne 0) { throw 'Source inventory failed' }
dotnet run --project (Join-Path $PSScriptRoot 'LocalizeSource') -- (Join-Path $workspace 'src\NorthIslandChestPlugin\Plugin.Ui.cs') --inventory > (Join-Path $workspace 'artifacts\dashboard-inventory.json')
if ($LASTEXITCODE -ne 0) { throw 'Dashboard inventory failed' }
dotnet run --project (Join-Path $PSScriptRoot 'LocalizeSource') -- (Join-Path $workspace 'src\NorthIslandChestPlugin\Plugin.History.cs') --inventory > (Join-Path $workspace 'artifacts\history-inventory.json')
if ($LASTEXITCODE -ne 0) { throw 'History inventory failed' }
python -X utf8 (Join-Path $PSScriptRoot 'audit-ui-translations.py')
if ($LASTEXITCODE -ne 0) { throw 'UI translation audit failed' }
