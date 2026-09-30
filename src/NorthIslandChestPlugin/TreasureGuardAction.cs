namespace NorthIslandChestPlugin;

internal enum TreasureGuardAction
{
	Inactive,
	WaitForTeleport,
	WaitForPlayers,
	WaitForMovement,
	WaitForMount,
	Mount,
	StartTreasure,
	Dismount,
	WaitForDismount,
	SwitchCrystal,
	TeleportTimedOut,
	MountTimedOut,
	DismountTimedOut
}
