using System;
using System.Collections.Generic;

namespace NorthIslandChestPlugin;

public sealed class TreasureRecord
{
	public DateTime CompletedAt { get; set; }

	public IslandTarget Island { get; set; }

	public TreasureMode Mode { get; set; }

	public Dictionary<string, int> Loot { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
}
