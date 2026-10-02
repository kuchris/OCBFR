using System;
using Dalamud.Game.ClientState.Conditions;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    private const int MaxShardTeleportAttempts = 3;
    private int shardTeleportAttempts;
    private readonly WorkflowProgressWatchdog workflowProgressWatchdog = new();

    private void RetryShardTeleportOrRecover()
    {
        // Wait for combat, casting and area transitions to finish before sending
        // another command. The expired guard continues to check this each frame.
        var player = objects.LocalPlayer;
        if (player == null || !IsPlayerMovable() || condition[ConditionFlag.InCombat])
        {
            status = "传送超时，等待角色可动后重试...";
            return;
        }
        if (shardTeleportAttempts >= MaxShardTeleportAttempts)
        {
            RecoverIslandWorkflow("小水晶传送连续失败");
            return;
        }
        shardTeleportAttempts++;
        treasurePlayerGuard.Begin(player.Position, DateTime.UtcNow, treasurePlayerGuard.RequiresMount);
        Send("/pdr ptp " + currentCrystal);
        status = "小水晶传送失败，正在重试...";
        log.Warning($"小水晶传送重试 {shardTeleportAttempts}/{MaxShardTeleportAttempts}，水晶={currentCrystal}，坐标={player.Position}", Array.Empty<object>());
    }

    private bool UpdateRecoveryWatchdog(DateTime now)
    {
        var player = objects.LocalPlayer;
        bool eligible = running && IsIsland() && player != null && IsPlayerMovable() &&
            !condition[ConditionFlag.InCombat] && !currencyBuyer.IsBusy && !islandRecoveryPending &&
            treasurePhase != TreasurePhase.LeaveDuty && treasurePhase != TreasurePhase.Reentry;
        string stage = $"{treasurePhase}:{waitingForEntry}:{waitingForScan}:{currencyPurchaseMoveActive}:{bocchiEnabled}";
        if (!workflowProgressWatchdog.IsStalled(now, clientState.TerritoryType, player?.Position ?? default,
                stage, cofferOpeningCounter.Count, eligible)) return false;
        RecoverIslandWorkflow("连续 30 分钟没有流程进展");
        return true;
    }

    private void RecoverIslandWorkflow(string reason)
    {
        if (!running || !IsIsland() || !IsPlayerMovable() || condition[ConditionFlag.InCombat] ||
            treasurePhase == TreasurePhase.LeaveDuty || treasurePhase == TreasurePhase.Reentry) return;
        log.Warning($"自动恢复：{reason}；地图={clientState.TerritoryType}，阶段={treasurePhase}，坐标={objects.LocalPlayer?.Position}", Array.Empty<object>());
        Send("/bocchiillegal off");
        Send("/pdr ptreasure abort");
        Send("/vnav stop");
        if (config.TreasureModeSelection == TreasureMode.XszRun) Send("/xsz-occult-treasure stop");
        if (treasureLoot.Count > 0) SaveTreasureRecord();
        ResetIslandCycle();
        // Keep running and retain the run's coffer count. Reuse the existing
        // leave/reentry handshake so no old route or delayed scan can resume.
        treasurePhase = TreasurePhase.LeaveDuty;
        BeginMovementWait(treasureMovementWait);
        status = "流程无进展，准备退岛重进...";
    }
}
