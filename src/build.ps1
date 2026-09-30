# OCBFR 建置腳本
#
# 用途：自動偵測目前 Dalamud 使用的 addon\Hooks\<version> 目錄，再交給 MSBuild。
# 原因：Dalamud 每次更新會建立新版本目錄並刪除舊的，若 csproj 硬編碼路徑就會突然編譯失敗
#       （症狀：大量 "The type or namespace name 'Dalamud' could not be found"）。
#
# 用法：
#   pwsh -File build.ps1
#   pwsh -File build.ps1 -Configuration Debug

param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$hooksRoot = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks'
if (-not (Test-Path $hooksRoot)) {
    throw "找不到 Dalamud Hooks 目錄：$hooksRoot（請先執行一次 XIVLauncher / Dalamud）"
}

# 挑選「有 Dalamud.dll」且版本號最新的目錄；若全部同版本則取最後修改時間最新者。
$candidates = Get-ChildItem $hooksRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'Dalamud.dll') } |
    ForEach-Object {
        $dll = Join-Path $_.FullName 'Dalamud.dll'
        $ver = $null
        try { $ver = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version } catch { }
        [pscustomobject]@{
            Name     = $_.Name
            FullName = $_.FullName
            Version  = $ver
            Modified = $_.LastWriteTime
        }
    }

if (-not $candidates) {
    throw "在 $hooksRoot 找不到任何含 Dalamud.dll 的目錄"
}

# 優先：版本號最大；同版本時：修改時間最新。'dev' 目錄排在最後（除非它是唯一選擇）。
$ordered = $candidates | Sort-Object `
    @{ Expression = { if ($_.Version) { $_.Version } else { [version]'0.0.0.0' } }; Descending = $true }, `
    @{ Expression = { if ($_.Name -eq 'dev') { 0 } else { 1 } }; Descending = $true }, `
    @{ Expression = { $_.Modified }; Descending = $true }

$chosen = $ordered | Select-Object -First 1
Write-Host "Dalamud 參考組件：$($chosen.Name)  (version $($chosen.Version))" -ForegroundColor Cyan

$proj = Join-Path $PSScriptRoot 'OCBFR.csproj'
dotnet build $proj -c $Configuration -p:DalamudLibPath="$($chosen.FullName)"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "建置失敗（exit $LASTEXITCODE）" -ForegroundColor Red
    exit $LASTEXITCODE
}

$outDll = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows7.0\OCNFarmer.dll"
if (Test-Path $outDll) {
    Write-Host ""
    Write-Host "輸出：$outDll" -ForegroundColor Green
    Write-Host ("大小：{0:N0} bytes" -f (Get-Item $outDll).Length)
}
