using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    private readonly CofferOpeningCounter cofferOpeningCounter = new();
    private readonly Dictionary<uint, bool> countedCofferTypes = new();

    private unsafe void UpdateOpenedCofferCount()
    {
        if (!running) return;
        bool canObserve = IsIsland() && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51];
        cofferOpeningCounter.BeginFrame(canObserve ? clientState.TerritoryType : 0);
        if (canObserve)
        {
            foreach (var obj in objects)
            {
                if (obj == null || obj.ObjectKind != ObjectKind.Treasure || obj.Address == IntPtr.Zero) continue;
                if (!countedCofferTypes.TryGetValue(obj.BaseId, out bool isCounted))
                {
                    isCounted = data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().TryGetRow(obj.BaseId, out var row)
                        && row.SGB.RowId is 1596 or 1597;
                    countedCofferTypes[obj.BaseId] = isCounted;
                }
                if (!isCounted) continue;
                var native = (NativeTreasure*)obj.Address;
                cofferOpeningCounter.Observe(obj.GameObjectId, (native->Flags & NativeTreasure.TreasureFlags.Opened) != 0);
            }
        }
        cofferOpeningCounter.EndFrame();
    }

    private bool ResetTreasureStatistics()
    {
        try
        {
            // Commit the empty history before changing the visible statistics.
            PersistTreasureRecords(new List<TreasureRecord>());
            treasureRecords.Clear();
            treasureLoot.Clear();
            cofferOpeningCounter.Reset();
            return true;
        }
        catch (Exception error)
        {
            log.Error(error, "重置寻宝统计失败", Array.Empty<object>());
            return false;
        }
    }
}
