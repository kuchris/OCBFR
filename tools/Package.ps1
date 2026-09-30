param([string]$SourceDll = '')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $SourceDll) { $SourceDll = Join-Path $workspace 'src\bin\Release\net10.0-windows7.0\OCNFarmer.dll' }
$SourceDll = [IO.Path]::GetFullPath($SourceDll)
if (-not (Test-Path -LiteralPath $SourceDll)) { throw 'Build the release DLL first with Build.ps1' }
$version = [Reflection.AssemblyName]::GetAssemblyName($SourceDll).Version.ToString()
$name = 'OCBFR-' + $version
$package = Join-Path $workspace ('dist\' + $name)
$archive = Join-Path $workspace ('dist\' + $name + '.zip')
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $package 'OCNFarmer.dll') -Force
Copy-Item -LiteralPath (Join-Path $workspace 'docs\THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $package 'THIRD-PARTY-NOTICES.txt') -Force
$manifest = Get-Content -LiteralPath (Join-Path $workspace 'manifest\OCNFarmer.json') -Raw | ConvertFrom-Json
if ($manifest.AssemblyVersion -ne $version) { throw 'Manifest and assembly versions differ' }
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $package 'OCNFarmer.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $workspace 'docs\INSTALL.en.md') -Destination (Join-Path $package 'README.en.md') -Force
Copy-Item -LiteralPath (Join-Path $workspace 'docs\INSTALL.zh-TW.md') -Destination (Join-Path $package '安裝說明.md') -Force
Copy-Item -LiteralPath (Join-Path $workspace 'docs\TEST-STATUS.md') -Destination (Join-Path $package 'TEST-STATUS.md') -Force
New-Item -ItemType Directory -Path (Join-Path $package 'images') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'images\icon.png') -Destination (Join-Path $package 'images\icon.png') -Force
$allowed = @('OCNFarmer.dll','OCNFarmer.json','README.en.md','安裝說明.md','TEST-STATUS.md','SHA256SUMS.txt','images/icon.png','THIRD-PARTY-NOTICES.txt')
$files = Get-ChildItem -LiteralPath $package -Recurse -File
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($package,$file.FullName).Replace('\','/')
    if ($relative -notin $allowed) { throw ('Unexpected package file: ' + $relative) }
}
$files | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetRelativePath($package,$_.FullName).Replace('\','/')
} | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Encoding utf8
# Compress package contents, keeping the plugin DLL at the ZIP root.
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $archive -Force
[pscustomobject]@{ Version = $version; Package = $package; Archive = $archive; Sha256 = (Get-FileHash -LiteralPath $archive).Hash }
