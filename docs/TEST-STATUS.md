# Validation

Current release: **2.3.0.14**, built against Dalamud **15.0.3.6** on 2026-09-30. Versions 2.3.0.13 and 2.3.0.14 change commands and product descriptions; gameplay is unchanged from 2.3.0.12.

## Checked in-game

| Behavior | Scope |
| --- | --- |
| Load, unload and reload | No observed crash after the UI recursion and disposal fixes. |
| Traditional Chinese / English UI | Dashboard, loot history, filters, search and existing records reviewed. |
| Scanning | English-client North Horn: dismount, stationary Freelancer scan, coffer counts and return to the combat job. |
| Treasure route | North inner and outer routes, actual loot, position-based return, history persistence and exit/re-entry. Route was triggered with the force-full Debug option. |
| Re-entry | Fresh state and scan; no resumption of the previous route. |
| Purchases | One North silver-currency transaction: Heavens' Eye Materia XI ×1. Quantity, currency deduction, shop closure and the following scan confirmed. |
| Commands | /ocbchest, /ocbstart and /ocbstop confirmed working. |

## Offline verification

`tools/Verify.ps1` prepares managed methods and runs state transitions with fake Dalamud services. The latest build passed **73 checks** and prepared **595 methods** without failures. Coverage includes scan sequencing, localized messages, thresholds, returns, history filtering, re-entry, settings migration and shop packet layouts. No OmenTools, GuerrillaNtp or TinyPinyin assembly references remain.

These checks do not execute native game actions.

## Pending in-game checks

- South Horn workflows.
- Japanese and Chinese-patched Global clients. Message formats have offline coverage.
- Starting a route from real silver ≥8 / bronze ≥30 counts; Debug force-full is not proof of the real threshold.
- Gold-currency transactions, multi-shop queues and large purchase batches.
- History persistence in the Debug stay-in-duty mode (checked offline).
