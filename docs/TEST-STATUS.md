# Validation

Current local build: **2.3.0.16**, built against Dalamud **15.0.3.6** on 2026-10-02. Adds a combined bronze/silver coffer opening count and statistics reset. The earlier in-game results below predate these additions.

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

`tools/Verify.ps1` prepares managed methods and runs state transitions with fake Dalamud services. The latest build passed **78 checks** and prepared **606 methods** without failures. Coverage includes scan sequencing, localized messages, thresholds, returns, history filtering, re-entry, settings migration and shop packet layouts. No OmenTools, GuerrillaNtp or TinyPinyin assembly references remain.

New checks use simulated treasure objects with native-layout flags to exercise the actual coffer polling path: bronze and silver openings combine, repeated frames and loot messages do not add counts, already-open coffers and despawns are ignored, re-entry keeps the total, accepted Start resets it, and saved statistics reset persists. A failed reset preserves the visible history. Traditional Chinese / English UI audit passed with no issues.

The opening counter observes bronze/silver coffer instances changing from closed to opened while running in the selected island. It starts at zero on each accepted Start, retains the result while stopped, and also resets when statistics are cleared. Reset clears all saved loot history and pending loot after an in-window confirmation. Openings outside the observation period, including a complete opening/despawn between framework updates, cannot be recovered from loot messages.

These checks do not execute native game actions.

## Pending in-game checks

- Bronze/silver opening counts and statistics reset, including the new controls in both UI languages. No in-game verification of these additions has been performed yet.
- South Horn workflows.
- Japanese and Chinese-patched Global clients. Message formats have offline coverage.
- Starting a route from real silver ≥8 / bronze ≥30 counts; Debug force-full is not proof of the real threshold.
- Gold-currency transactions, multi-shop queues and large purchase batches.
- History persistence in the Debug stay-in-duty mode (checked offline).
