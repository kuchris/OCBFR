using System;
using System.Numerics;

namespace NorthIslandChestPlugin;

internal sealed class WorkflowProgressWatchdog
{
    private static readonly TimeSpan NoProgressLimit = TimeSpan.FromMinutes(30);
    private bool initialized;
    private uint lastTerritory;
    private Vector3 lastPosition;
    private string lastStage;
    private int lastOpenedCount;
    private DateTime lastProgressAt;

    internal void Reset() => initialized = false;

    internal bool IsStalled(DateTime now, uint territory, Vector3 position, string stage, int openedCount, bool eligible)
    {
        if (!eligible || !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            Reset();
            return false;
        }
        if (!initialized || territory != lastTerritory || stage != lastStage || openedCount != lastOpenedCount ||
            Vector3.DistanceSquared(position, lastPosition) >= 9f)
        {
            initialized = true;
            lastTerritory = territory;
            lastPosition = position;
            lastStage = stage;
            lastOpenedCount = openedCount;
            lastProgressAt = now;
            return false;
        }
        return now - lastProgressAt >= NoProgressLimit;
    }
}
