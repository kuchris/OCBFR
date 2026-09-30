# OCBFR

Source repository for the Global Occult Crescent treasure farmer maintained by kuchris. Current version: **2.3.0.9**, Dalamud API **15**.

The source is publicly available here. No project license has been added; bundled dependencies retain their existing licenses. Installation ZIPs and the plugin icon are distributed through the [DalamudPlugins catalogue](https://github.com/kuchris/DalamudPlugins#ocbfr).

## Build

Requires Windows, the .NET 10 SDK, and an installed Dalamud API 15 environment. The required OmenTools, GuerrillaNtp and TinyPinyin libraries are included in `dependencies/`. Dalamud and game data are supplied by the local game installation.

```powershell
.\Build.ps1
.\tools\Verify.ps1
.\tools\Package.ps1
```

The build script finds the newest installed Dalamud Hooks directory. To select references manually:

```powershell
dotnet build .\src\OCNFarmer.csproj -c Release -p:DalamudLibPath="<Hooks directory>"
```

`Package.ps1` creates `dist/OCBFR-2.3.0.9.zip` with the plugin DLL and manifest at the ZIP root, ready for the Dalamud installer. It includes only runtime files, installation documents and checksums; no source, PDBs, personal configuration or game logs.

## Layout

- `src/`: complete C# project, bilingual UI and localized game-message parser.
- `dependencies/`: required libraries and OmenTools' license.
- `images/` and `assets/`: packaged icon and editable original SVG.
- `manifest/`: plugin metadata for public binary distribution.
- `tools/`: build verification, translation audit/generator, icon renderer and optional local game-data inspection.
- `docs/`: [English installation](docs/INSTALL.en.md), [繁體中文安裝](docs/INSTALL.zh-TW.md), and [test status](docs/TEST-STATUS.md).

## UI translations

The Traditional Chinese catalog is explicitly reviewed in `tools/ui-traditional.json`. English text and phantom job display mappings live in `tools/make-ui-translations.py`.

```powershell
python -X utf8 .\tools\make-ui-translations.py
.\tools\Audit-Translations.ps1
```

The generator rejects missing Traditional Chinese keys. Verification checks the actual embedded translation catalog and tower labels. Changes to presentation must retain existing ImGui identities, stored English phantom job names, route names and numeric shard indices.

## Release

1. Build, verify and perform the relevant in-game checks. Preserve the documented limits.
2. Update the assembly version, informational version, manifest and installation documents together.
3. Run `Package.ps1`; verify the ZIP contents and checksums.
4. Commit and push source changes to this repository.
5. Publish the flat installation ZIP as a release asset in public `kuchris/DalamudPlugins`; publish `images/icon.png` there under `plugins/OCNFarmer/`.
6. Update only the `OCNFarmer` object in the public `repo.json`, including version, timestamps, download links and icon URL.

The catalogue continues to distribute installation ZIPs from `kuchris/DalamudPlugins`. Keep the assembly/internal ID `OCNFarmer` and `/ocnchest`, `/ocnstart`, `/ocnstop` commands to preserve existing settings.

English-client North workflows have been tested in-game. South, Japanese and Chinese-patched clients, the real full-coffer threshold and some ancillary features still need the checks listed in `docs/TEST-STATUS.md`. Offline success does not prove those live behaviors.

[Support on Ko-fi](https://ko-fi.com/kuchris).
