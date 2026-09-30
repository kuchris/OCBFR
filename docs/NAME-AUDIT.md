# OCBFR name audit

Checked on 2026-09-30 for version 2.3.0.12.

Visible plugin name, main title/header, author, product/title metadata, command help and support link use **OCBFR / kuchris**. No Angelways or OCFA branding remains in source UI. The source project is now **src/OCBFR.csproj**; the build script and README use that path.

The remaining legacy identifiers are intentional compatibility keys:

| Identifier | Location | Reason retained |
| --- | --- | --- |
| `OCNFarmer` | AssemblyName, DLL/manifest filename and catalogue InternalName | Existing installer updates continue to target the installed plugin. Changing this requires a separate installer/config migration. |
| `OCNFarmer` | Dalamud config directory and host log prefix | Supplied by the existing internal ID; preserves user settings and treasure records. |
| `OCNFarmerFull`, `OCNFarmerSimplified`, `OCNFarmerTreasureHistory` | Text after `###` in window names | Hidden ImGui IDs preserve saved window positions and sizes. These suffixes are not displayed. |
| `NorthIslandChestPlugin` | Private source namespace, earlier config migration paths | Keeps existing serialized config type names and legacy record migration compatible. |
| `/ocnchest`, `/ocnstart`, `/ocnstop` | Registered commands and instructions | Existing user macros remain valid. Command renaming was previously discussed, but not requested for implementation. |
| `plugins/OCNFarmer/icon.png` | Catalogue icon URL | Existing installer icon location; visible icon and branding are OCBFR. |

Installation instructions mention the literal legacy filenames, config directory and log prefix where users need them. Ordinary visible prose uses OCBFR. Test code references the same legacy types/IDs to check compatibility, not to label the plugin.

OmenTools, GuerrillaNtp and TinyPinyin assembly references and bundled DLLs have been removed. The plugin reads sheets through Dalamud `IDataManager`, uses native agent/addon events for purchases, and has its own minimal `ShopEventBridge` for shop session start/completion. No OmenTools service initialization or packet hooks remain. The prior interop license notice is retained independently of runtime dependencies; no license has been added for this project.
