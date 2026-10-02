using System.Collections.Generic;
using System.Linq;

namespace NorthIslandChestPlugin;

// Observe live instances, not loot messages or scan totals. An already-open
// coffer on the first observation is a baseline, never a new opening.
internal sealed class CofferOpeningCounter
{
    private readonly Dictionary<ulong, bool> opened = new();
    private readonly HashSet<ulong> visible = new();
    private uint territory;
    internal int Count { get; private set; }

    internal void Reset()
    {
        Count = 0;
        opened.Clear();
        visible.Clear();
        territory = 0;
    }

    internal void BeginFrame(uint currentTerritory)
    {
        if (territory != currentTerritory) opened.Clear();
        territory = currentTerritory;
        visible.Clear();
    }

    internal void Observe(ulong instanceId, bool isOpened)
    {
        visible.Add(instanceId);
        if (opened.TryGetValue(instanceId, out bool wasOpened) && !wasOpened && isOpened)
            Count++;
        // Once opened, keep the instance latched even if a later frame fades it.
        opened[instanceId] = isOpened || (opened.TryGetValue(instanceId, out wasOpened) && wasOpened);
    }

    internal void EndFrame()
    {
        foreach (ulong id in opened.Keys.Where(id => !visible.Contains(id)).ToArray())
            opened.Remove(id);
    }
}
