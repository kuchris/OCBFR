# OCBFR Global validation

Date: 2026-09-30. Build against Dalamud 15.0.3.6, using `rebuild01` C# source. No binary IL patches. Sharing build version: 2.3.0.6. Display name: OCBFR. Author: kuchris. The original assembly/internal identifier and commands remain compatible with saved settings.

## Live checks

| Stage | Result | Evidence |
| --- | --- | --- |
| 01 UI recursion fix | Passed | User opened `/ocnchest` for 10 seconds without another crash. Both window overrides now call `base.PostDraw()`. |
| 02 unload fix | Passed | User unloaded, reloaded and reopened the UI successfully. New-version unload no longer produced the VerificationSession null exception. |
| 03 English jobs | Passed | User confirmed Freelancer → Treasuresight → White Mage. Log recorded English job commands and action 41651 accepted. |
| 04 coffer trigger | Debug full-route path passed; real threshold pending | Force-full after a scan started treasure. North inner route started 17:56:26; position fallback progressed to outer route at 18:00:44. Both routes produced actual loot. User confirmed completion at approximately 18:07. |
| 05 reentry reset | Passed for debug exit/reentry | At 18:07:30 the plugin cancelled old activity; at 18:07:32 it requested entry; at 18:07:41 it received the sync handshake and started a fresh cycle. User confirmed reentry and scanning. Subsequent scan retries require further count-message diagnosis. |
| 06 dismount/scan sequencing | Dismount and job change passed; scan completion failed | Log at 18:11 showed the game using singular `1 bronze coffer`. The plural-only parser did not finish scanning, and a pending combat timer started movement during retries. User corrected the initial report after noticing movement. |
| 07 singular counts and scan pause | Passed | User confirmed stationary scanning and combat resumed after counts. Log at 18:14:25 recorded silver 0 / bronze 1; only at 18:14:30 did BOCCHI resume. |
| 08 return completion/history | Complete North cycle passed | Inner completed from position at 18:21:06, outer at 18:27:02. History file exists with the completion timestamp. Automatic leave at 18:27:07, reentry handshake at 18:27:19 and fresh scan followed. User confirmed completion. This run used force-full, so it does not prove the real full-coffer threshold. |
| 09 sharing build | Offline and North live scan passed | Removes unused embedded verification connection data and disables its factory; updates assembly version to 2.3.0.1. Unload/reload completed without error. At 18:29:03, a mounted start completed scan with silver 0 / bronze 0, changed back to White Mage, resumed combat at 18:29:08, and emergency-stopped at 18:29:11. Route and scanning logic is unchanged from stage 08. |
| 10 remove verification and unattended notifications | Offline and live passed | User confirmed the removed UI, scanning and reload work. Removes verification session, window, UI, account signature scan, authorization guards, Server酱 notification code and settings. Removes Omni.Verification / System.Management / System.CodeDom from the package. Logout cancellation remains covered. Live config was saved as schema 4 with no old notification fields. Author metadata updated to kuchris at the user's request. |
| 11 OCBFR branding | Offline and reload passed | Updates visible plugin name, window title, header, product metadata and author. Retains assembly/internal ID, window layout IDs and commands so existing settings remain usable. Dalamud completed unload/reload at 18:52:30 without error. |
| 12 bilingual UI | Offline and English-client live passed | User confirmed Traditional Chinese/English switching, settings/history/tooltips, buttons and a scan. At 19:14:21 the scan read 0/0, used the canonical English White Mage command, resumed combat at 19:14:26 and emergency-stopped at 19:14:32. UI language is persisted; legacy widget and window IDs are retained. |
| 13 localized game messages | Offline and English-client reentry live passed; Japanese/patched live pending | User confirmed scans, reentry and stop. Version 2.3.0.5 loaded at 19:26:29. Entry synchronization at 19:27:00 and 19:27:34 completed fresh scans at 19:27:11 and 19:27:45; emergency stop at 19:28:07. Japanese formats were checked against local LogMessage data; Simplified/Traditional patch cases passed managed tests. User opted to test only the English client this session. |
| 14 monochrome dashboard, icon and Ko-fi | Offline and user live review passed | User confirmed the bilingual full/compact interface, actions, Ko-fi and scan work. The plugin installer retained its old cached icon; the user cleared it with /xldev → Plugins → Clear cached images/icons and confirmed the new icon appeared. Original crescent/chest icon inspected at 512 and 64 pixels; embedded in DLL and bundled at images/icon.png. 571 managed methods prepared, 63 regression checks passed and 17 gameplay methods match stage 13. |
| 15 remove Instructions page | Offline and user live UI review passed | Version 2.3.0.7 removes the Instructions page and its prose. The existing six DR module commands are retained behind a button in Overview. User confirmed both language views show four pages and the interface works. Installation documents include the confirmed installer-cache refresh steps. 571 managed methods prepared with no failures, 63 regression checks passed, and 17 gameplay methods match stage 14. |
| 16 reviewed Traditional Chinese translations | Offline and user live UI review passed | User screenshot showed incomplete Traditional Chinese conversion in tower notes (戰斗, 后續, 是什么意思). A test against the real UiText.Render method failed on version 2.3.0.7. Version 2.3.0.8 replaces build-time character conversion with a reviewed explicit catalog; the generator rejects missing or extra keys. Tower notes and common UI terminology are corrected. User confirmed the tower page renders correctly in Traditional Chinese and English. Dalamud completed unload/reload at 19:56:24. Audited 259 catalog entries and 160 static UI literals; 571 methods prepared and 65 managed checks passed. Plugin.cs is byte-for-byte unchanged and the 17 gameplay method comparison also passed. |

## Outstanding live checks

- Confirm real silver ≥ 8 or bronze ≥ 30 automatically starts treasure; debug force-full is not evidence for the real threshold.
- Normal position completion persisted history in live testing; no-leave history persistence was tested offline only.
- Global South profile has not yet been tested in this session.
- Japanese and Chinese-patched Global clients need in-game scan, reentry and loot validation. Offline message tests do not prove live client support for every patch.
- The complete North treasure route was live-tested before localization. The new localization build was live-tested for UI, scans and reentry; it was not used for another full inner/outer run.

## Offline scope

The verification tool loads and prepares managed methods and executes state transitions with fake Dalamud services. It does not execute native game actions and does not replace live game testing.

At stage 10, managed IL preparation succeeded for 528 methods, and all regression cases passed. The checks cover UI recursion, counts and thresholds, scan pause and job sequencing, return readiness, one-time history persistence, reentry reset, South profile selection, logout cancellation, and absence of verification types/dependencies and notification settings. Existing compiler warnings are retained; successful compilation alone is not a stability claim.

At stage 13, 561 managed methods prepared successfully with no failures and 63 checks passed. Additional checks cover UI switching/cache refresh, canonical job commands, legacy ImGui identity, both UI languages with localized coffer thresholds, Japanese/full-width/Chinese empty scans and counts, invalid and wrong-channel messages, job rejection retries, localized entry synchronization and acknowledgements, Japanese/Chinese loot quantities, thousands separators, verified bare Japanese items, and exclusion of another player's loot. The release build has 17 existing compiler warnings and no errors.

A syntax-token comparison against stage 11 confirmed 17 existing gameplay methods are unchanged, including start/stop, scan preparation, dismount, scan completion, treasure start/update/return, territory handling, entry handshake, combat job switching, tower movement, currency movement and command sending. Changes are confined to UI presentation/configuration and the chat message/loot parser. UI translations are embedded in the DLL; no new runtime dependencies are shipped.

Temporary `[DEBUG-ocn-guard]` and `[DEBUG-ocn-scan]` instrumentation was removed in stage 08.
