# OCBFR Global 2.3.0.12

Author: kuchris. Built from the C# source for Dalamud API 15. The DLL, manifest filename, internal ID and commands remain `OCNFarmer` so existing settings continue to work.

## Install from the catalogue

Add this URL in `/xlsettings` → Experimental → Custom Plugin Repositories, enable it and save:

```text
https://raw.githubusercontent.com/kuchris/DalamudPlugins/main/repo.json
```

Open `/xlplugins`, refresh and install **OCBFR**. Configure Daily Routines, vnavmesh and BOCCHI as described below before starting. [Source code](https://github.com/kuchris/OCBFR) is publicly available; the catalogue distributes the compiled plugin.

If you previously used a DEV copy, emergency-stop and unload it, then disable its Dev Plugin Location before installing the catalogue version. Keep the existing Dalamud OCNFarmer configuration folder. Do not load both copies together.

## Manual DEV installation or update

1. Extract the entire package into a permanent folder, for example `C:\OCBFR\`. Keep all DLLs and `OCNFarmer.json` together, including the `images` subfolder.
2. Open `/xlsettings` → Experimental → Dev Plugin Locations and add the full DLL path, for example `C:\OCBFR\OCNFarmer.dll`. Enable and save it.
3. Load **OCBFR** in `/xlplugins`, then open `/ocnchest`.
4. Before updating, use Emergency stop, unload the plugin, replace the package and reload. Keep only one development-plugin location. Preserve your existing Dalamud plugin configuration folder.

## Languages

The monochrome dashboard shows the current task and coffer counts above three workspace pages: Overview, Purchases and Test tools. Start/stop, emergency stop, history and Ko-fi controls remain at the bottom. Full/Compact view switching is at the top; compact view retains the primary controls. Previously undersized windows are resized to the new minimum usable dimensions.

The new crescent/chest icon is embedded for the header and included as `images/icon.png` for the development-plugin list. Keep this folder when updating. If the plugin list shows its cached old icon, open `/xlplugins`, then `/xldev` → Plugins → Clear cached images/icons. Run `/xldev` again to hide the menu. Reloading the plugin or reopening the list alone does not clear this cache.

The Ko-fi button opens [ko-fi.com/kuchris](https://ko-fi.com/kuchris) only when clicked.

Use **Language / 語言** at the top of either UI view to select **Traditional Chinese** or **English**. The choice is saved. The default is Traditional Chinese.

Traditional Chinese labels and tooltips use an explicit, reviewed translation catalog, including settings and status messages. Runtime character conversion is not required.

UI translation changes displayed text only. Phantom jobs remain stored and sent to Daily Routines in English; shard teleports use numeric indices. Purchase settings, discard presets, window identities, route commands and recorded loot are retained. Game item names follow the game data/messages and are not rewritten in saved records.

The chat parser accepts English, Japanese, and common Simplified/Traditional Chinese formats used by Global localization patches. It covers coffer counts, empty scans, entry synchronization, rejected phantom job switches and loot quantities independently of the UI language. Japanese formats were checked against local game data. **Japanese and Chinese-patched clients have not yet been tested in-game.** Other patch wording may need additional matching. This release is for Global; China-region servers are outside this release's scope.

## Required plugins and setup

Install and configure **Daily Routines**, **vnavmesh** and **BOCCHI** separately. This package includes only OCBFR's library dependencies.

Enable these Daily Routines modules using **Enable required DR modules** at the bottom of Overview or the commands below:

```text
/pdr load OccultCrescentHelper
/pdr load BetterMKDSupportJobList
/pdr load PhantomJobSwitchCommand
/pdr load AutoCommenceDuty
/pdr load InstantLeaveDuty
/pdr load FieldEntryCommand
```

- Keep **Daily Routines' UI language set to Simplified Chinese**. The established route commands use the route names `内环` and `外环`. OCBFR's UI language and the game client's language can differ from DR's UI language.
- In DR's Occult Crescent helper, enable automatic opening of nearby treasure coffers and retain its normal distance settings.
- Disable BOCCHI's automatic duty rotation and rotation when population is low. OCBFR controls exit and re-entry.
- Select the target island and an unlocked combat phantom job in OCBFR. Phantom White Mage is the default.

## Run and stop

Leave both Debug checkboxes off for normal operation. Press **Start** or use `/ocnstart`.

The plugin pauses combat/navigation, dismounts, switches to Phantom Freelancer, casts Treasuresight and reads coffer counts. Below the threshold it switches back to the combat phantom job. At **8 silver coffers or 30 bronze coffers**, it starts the treasure route. Returning to camp completes the inner leg, then starts the outer leg. The outer return saves a record, leaves duty, re-enters and performs a fresh scan.

Return detection uses a position within 60 yalms of the target island's base camp and waits through casting/area transitions. It does not require Action chat messages. Entry waits for the localized item-level sync message and a ready character.

Use **Emergency stop** to stop the plugin and the external route/navigation. If you only use `/ocnstop`, also use `/pdr ptreasure abort` and `/vnav stop` when needed. `/ocnchest` opens the UI.

## Debug and verification

**Treat coffers as full** simulates the threshold after a successful scan; it does not change actual game coffers. **Stay in duty** tests the inner and outer legs without leaving. If force-full remains enabled, a fresh scan after re-entry starts another cycle; emergency-stop after your test.

English-client North scans, UI language switching and exit/re-entry have been tested in-game. A complete North route was validated before this localization change. South, the real full-coffer threshold, Japanese/patched-client live behavior, and purchase functionality are not fully live-validated. Read `TEST-STATUS.md` for precise scope.

Logs: `%APPDATA%\XIVLauncher\dalamud.log`, filter `[OCNFarmer]`. Saved loot records: `treasure-records.json` under Dalamud's OCNFarmer configuration directory. Diagnostic logs retain their established wording.

Omni verification and unattended Server酱 notifications are removed. No personal configuration, keys, character data or game logs are included. OmenTools, GuerrillaNtp and TinyPinyin DLLs are no longer required. Only the plugin DLL is shipped; Dalamud supplies the host assemblies. The previous interop license notice is retained. File checksums are in `SHA256SUMS.txt`.
