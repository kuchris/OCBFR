# Validation

Current local build: **2.3.0.17**, built against Dalamud **15.0.3.6** on 2026-10-02. Adds recovery for failed shard teleports, crystal navigation timeouts and prolonged lack of workflow progress. The earlier in-game results below predate these additions and the 2.3.0.16 statistics changes.

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

`tools/Verify.ps1` prepares managed methods and runs state transitions with fake Dalamud services. The latest build passed **86 checks** and prepared **613 methods** without failures. Coverage includes scan sequencing, localized messages, thresholds, returns, history filtering, re-entry, settings migration and shop packet layouts. No OmenTools, GuerrillaNtp or TinyPinyin assembly references remain.

Recovery checks first reproduced two 2.3.0.16 failure paths: a shard teleport timing out and crystal navigation timing out both disabled the workflow. The fix keeps it running. Each shard teleport has the existing three-minute timeout and a maximum of three total attempts. Retries wait for combat/casting to finish. Exhausted attempts or crystal navigation timeout stop external routes and schedule leave/re-entry through the existing movement and entry handshake. Any captured loot is saved as a history record before resetting the interrupted cycle; the run's coffer count is retained. Repeated frames cannot issue duplicate recovery.

The fallback detects 30 continuous minutes without at least three yalms of movement, a workflow-stage change or a coffer opening. Remaining in the same map alone does not trigger it. Combat, loading, unavailable movement, active purchases and leaving/re-entry reset or suspend observation. Tests cover normal movement, phase changes, openings, stopped work and combat/loading exclusions. This does not establish what happened in the friend's session; its log has not been supplied.

New checks use simulated treasure objects with native-layout flags to exercise the actual coffer polling path: bronze and silver openings combine, repeated frames and loot messages do not add counts, already-open coffers and despawns are ignored, re-entry keeps the total, accepted Start resets it, and saved statistics reset persists. A failed reset preserves the visible history. Traditional Chinese / English UI audit passed with no issues.

The opening counter observes bronze/silver coffer instances changing from closed to opened while running in the selected island. It starts at zero on each accepted Start, retains the result while stopped, and also resets when statistics are cleared. Reset clears all saved loot history and pending loot after an in-window confirmation. Openings outside the observation period, including a complete opening/despawn between framework updates, cannot be recovered from loot messages.

These checks do not execute native game actions.

## Pending in-game checks

- Failed shard retry and leave/re-entry recovery, navigation recovery and the 30-minute fallback. These additions have only offline coverage.
- Bronze/silver opening counts and statistics reset, including the new controls in both UI languages. No in-game verification of these additions has been performed yet.
- South Horn workflows.
- Japanese and Chinese-patched Global clients. Message formats have offline coverage.
- Starting a route from real silver ≥8 / bronze ≥30 counts; Debug force-full is not proof of the real threshold.
- Gold-currency transactions, multi-shop queues and large purchase batches.
- History persistence in the Debug stay-in-duty mode (checked offline).
