using System;
using System.Numerics;

namespace NorthIslandChestPlugin;

internal sealed class TreasurePlayerGuard
{
	private static readonly TimeSpan PlayerWaitLimit = TimeSpan.FromSeconds(15L);

	private static readonly TimeSpan TeleportLimit = TimeSpan.FromMinutes(3L);

	private static readonly TimeSpan MountLimit = TimeSpan.FromSeconds(12L);

	private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1L);

	private const float OriginToleranceSquared = 0.25f;

	private Vector3 teleportOrigin;

	private DateTime teleportStartedAt;

	private DateTime? detectionStartedAt;

	private DateTime? mountDeadline;

	private DateTime nextMountAttempt;

	private DateTime? dismountDeadline;

	private DateTime nextDismountAttempt;

	public bool IsActive { get; private set; }

	public bool RequiresMount { get; private set; }

	public void Begin(Vector3 origin, DateTime now, bool requiresMount)
	{
		Reset();
		IsActive = true;
		RequiresMount = requiresMount;
		teleportOrigin = origin;
		teleportStartedAt = now;
	}

	public bool ShouldCheckPlayers(Vector3? position)
	{
		if (IsActive && position.HasValue)
		{
			Vector3 valueOrDefault = position.GetValueOrDefault();
			if (IsFinite(valueOrDefault))
			{
				if (!detectionStartedAt.HasValue)
				{
					return Vector3.DistanceSquared(valueOrDefault, teleportOrigin) > 0.25f;
				}
				return true;
			}
		}
		return false;
	}

	public TreasureGuardAction Update(DateTime now, TreasurePlayerSnapshot player)
	{
		if (!IsActive)
		{
			return TreasureGuardAction.Inactive;
		}
		if (!detectionStartedAt.HasValue)
		{
			if (!ShouldCheckPlayers(player.Position))
			{
				if (!(now - teleportStartedAt >= TeleportLimit))
				{
					return TreasureGuardAction.WaitForTeleport;
				}
				return TreasureGuardAction.TeleportTimedOut;
			}
			detectionStartedAt = now;
		}
		if (dismountDeadline.HasValue)
		{
			return UpdateDismount(now, player);
		}
		Vector3? position = player.Position;
		if (position.HasValue)
		{
			Vector3 valueOrDefault = position.GetValueOrDefault();
			if (IsFinite(valueOrDefault))
			{
				if (player.NearbyPlayer)
				{
					mountDeadline = null;
					nextMountAttempt = DateTime.MinValue;
					if (now - detectionStartedAt.Value > PlayerWaitLimit)
					{
						dismountDeadline = now + MountLimit;
						nextDismountAttempt = DateTime.MinValue;
						return UpdateDismount(now, player);
					}
					return TreasureGuardAction.WaitForPlayers;
				}
				if (player.Mounting)
				{
					if (!mountDeadline.HasValue || !(now >= mountDeadline.Value))
					{
						return TreasureGuardAction.WaitForMount;
					}
					return TreasureGuardAction.MountTimedOut;
				}
				if (!player.CanMove)
				{
					return TreasureGuardAction.WaitForMovement;
				}
				if (!RequiresMount || player.Mounted)
				{
					return TreasureGuardAction.StartTreasure;
				}
				if (mountDeadline.HasValue && now >= mountDeadline.Value)
				{
					return TreasureGuardAction.MountTimedOut;
				}
				if (now < nextMountAttempt)
				{
					return TreasureGuardAction.WaitForMount;
				}
				DateTime valueOrDefault2 = mountDeadline.GetValueOrDefault();
				if (!mountDeadline.HasValue)
				{
					valueOrDefault2 = now + MountLimit;
					mountDeadline = valueOrDefault2;
				}
				nextMountAttempt = now + RetryInterval;
				return TreasureGuardAction.Mount;
			}
		}
		return TreasureGuardAction.WaitForMovement;
	}

	private TreasureGuardAction UpdateDismount(DateTime now, TreasurePlayerSnapshot player)
	{
		Vector3? position = player.Position;
		if (position.HasValue)
		{
			Vector3 valueOrDefault = position.GetValueOrDefault();
			if (IsFinite(valueOrDefault) && !player.Mounted && !player.Mounting && player.CanMove)
			{
				return TreasureGuardAction.SwitchCrystal;
			}
		}
		if (now >= dismountDeadline.Value)
		{
			return TreasureGuardAction.DismountTimedOut;
		}
		if (player.Position.HasValue && player.Mounted && !player.Mounting && player.CanMove && now >= nextDismountAttempt)
		{
			nextDismountAttempt = now + RetryInterval;
			return TreasureGuardAction.Dismount;
		}
		return TreasureGuardAction.WaitForDismount;
	}

	public void Reset()
	{
		IsActive = false;
		RequiresMount = false;
		detectionStartedAt = (mountDeadline = (dismountDeadline = null));
		teleportOrigin = default;
		teleportStartedAt = (nextMountAttempt = (nextDismountAttempt = DateTime.MinValue));
	}

	private static bool IsFinite(Vector3 position)
	{
		if (float.IsFinite(position.X) && float.IsFinite(position.Y))
		{
			return float.IsFinite(position.Z);
		}
		return false;
	}
}
