using System;

namespace NorthIslandChestPlugin;

internal sealed class MovableWait
{
	private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1L);

	private DateTime? readySince;

	private uint readyTerritory;

	private uint? exitTerritory;

	public bool IsActive { get; private set; }

	public void Begin(DateTime now, bool movable, uint territory, bool requireTerritoryExit = false)
	{
		Reset();
		IsActive = true;
		exitTerritory = (requireTerritoryExit ? new uint?(territory) : ((uint?)null));
		IsReady(now, movable, territory);
	}

	public bool IsReady(DateTime now, bool movable, uint territory)
	{
		if (!IsActive)
		{
			return false;
		}
		if (!movable || territory == 0 || territory == exitTerritory)
		{
			readySince = null;
			return false;
		}
		if (!readySince.HasValue || readyTerritory != territory)
		{
			readySince = now;
			readyTerritory = territory;
		}
		return now - readySince.Value >= SettleTime;
	}

	public void Reset()
	{
		IsActive = false;
		readySince = null;
		readyTerritory = 0u;
		exitTerritory = null;
	}
}
