# OCBFR

OCBFR is an Occult Crescent automation plugin for Dalamud by kuchris. It scans coffers, runs treasure routes, exchanges currency, handles re-entry and keeps a searchable loot history. Its interface supports Traditional Chinese and English. Current version: **2.3.0.14**, Dalamud API **15**.

The source is publicly available here. No project license has been added. The OmenTools notice for the previous interop implementation is retained under `notices/`. Installation ZIPs are available in this repository's [Releases](https://github.com/kuchris/OCBFR/releases). The [DalamudPlugins catalogue](https://github.com/kuchris/DalamudPlugins#ocbfr) provides the installer subscription.

## Build

Requires Windows, the .NET 10 SDK, and an installed Dalamud API 15 environment. No OmenTools, GuerrillaNtp or TinyPinyin DLLs are required. The plugin uses Dalamud services and its own small currency-shop event bridge. Dalamud and game data are supplied by the local game installation.

```powershell
.\Build.ps1
.\tools\Verify.ps1
.\tools\Package.ps1
```

The build script finds the newest installed Dalamud Hooks directory. To select references manually:

```powershell
dotnet build .\src\OCBFR.csproj -c Release -p:DalamudLibPath="<Hooks directory>"
```

`Package.ps1` creates `dist/OCBFR-2.3.0.14.zip` with the plugin DLL and manifest at the ZIP root, ready for the Dalamud installer. It includes only runtime files, installation documents and checksums; no source, PDBs, personal configuration or game logs.

## Layout

- `src/`: complete C# project, bilingual UI and localized game-message parser.
- `notices/`: retained third-party interop notice; no dependency DLLs.
- `images/` and `assets/`: packaged icon and editable original SVG.
- `manifest/`: plugin metadata for public binary distribution.
- `tools/`: build verification, translation audit/generator, icon renderer and optional local game-data inspection.
- `docs/`: [English installation](docs/INSTALL.en.md), [繁體中文安裝](docs/INSTALL.zh-TW.md), and [test status](docs/TEST-STATUS.md).

## Names and compatibility

The project is `src/OCBFR.csproj`; visible titles, metadata, help text and branding use **OCBFR**. The assembly/internal installer ID, config paths, private namespace and hidden ImGui IDs keep their existing values to preserve installed-plugin updates, settings, records and window layouts. These technical identifiers are not UI branding. See the [name audit](docs/NAME-AUDIT.md) for every retained identifier.

The loot-history page has item search, date/island filters, summary counts and separate item-total/run views. Stored loot keys and quantities are unchanged.

## UI translations

The Traditional Chinese catalog is explicitly reviewed in `tools/ui-traditional.json`. English text and phantom job display mappings live in `tools/make-ui-translations.py`.

```powershell
python -X utf8 .\tools\make-ui-translations.py
.\tools\Audit-Translations.ps1
```

The generator rejects missing Traditional Chinese keys. Verification checks the actual embedded translation catalog and settings migration. Changes to presentation must retain existing ImGui identities, stored English phantom job names, route names and numeric shard indices.

## Release

1. Build, verify and perform the relevant in-game checks. Preserve the documented limits.
2. Update the assembly version, informational version, manifest and installation documents together.
3. Run `Package.ps1`; verify the ZIP contents and checksums.
4. Commit and push source changes to this repository.
5. Publish the flat installation ZIP as a release asset in `kuchris/OCBFR`. The catalogue's icon is kept under `plugins/OCNFarmer/` in `kuchris/DalamudPlugins`.
6. Update only the `OCNFarmer` object in the public `repo.json`, including version, timestamps, download links and icon URL.

The catalogue points installation and update links to releases in `kuchris/OCBFR`. Keep the assembly/internal ID `OCNFarmer` to preserve existing settings. Commands are now `/ocbchest` (open UI), `/ocbstart` (start) and `/ocbstop` (stop); update any macros that used the old commands.

English-client North workflows have been tested in-game. South, Japanese and Chinese-patched clients, the real full-coffer threshold and some ancillary features still need the checks listed in `docs/TEST-STATUS.md`. Offline success does not prove those live behaviors.

[Support on Ko-fi](https://ko-fi.com/kuchris).

## Credits

Gugu contributed to the South Horn workflow and currency purchasing. Third-party notices are retained under `notices/`.
