# OCBFR

<p align="center">
  <img src="images/icon.png" alt="OCBFR crescent moon and treasure chest" width="240">
</p>

OCBFR automates Occult Crescent coffer scanning, treasure routes, currency purchases and re-entry, with a searchable loot history and a **Traditional Chinese / English** interface.

## Install

Add this URL in `/xlsettings` → **Experimental** → **Custom Plugin Repositories**, then install **OCBFR** from `/xlplugins`:

```text
https://raw.githubusercontent.com/kuchris/DalamudPlugins/main/repo.json
```

Requires **Daily Routines**, **vnavmesh** and **BOCCHI**. Keep Daily Routines in **Simplified Chinese** for the treasure route names. Complete the setup before starting:

[English setup](docs/INSTALL.en.md) · [繁體中文安裝](docs/INSTALL.zh-TW.md) · [Downloads](https://github.com/kuchris/OCBFR/releases)

## Commands

| Command | Action |
| --- | --- |
| `/ocbchest` | Open the interface |
| `/ocbstart` | Start |
| `/ocbstop` | Stop |

Use **Emergency stop** in the interface to also stop external navigation and treasure routes.

## Build

Requires Windows, the **.NET 10 SDK** and **Dalamud API 15** references from the local installation.

```powershell
.\Build.ps1
.\tools\Verify.ps1
.\tools\Package.ps1
```

The build detects the installed Dalamud Hooks directory. To choose references manually:

```powershell
dotnet build .\src\OCBFR.csproj -c Release -p:DalamudLibPath="<Hooks directory>"
```

Packages are created under `dist/`. See [validation status](docs/TEST-STATUS.md) for tested behavior and pending client checks.

[Support on Ko-fi](https://ko-fi.com/kuchris)
