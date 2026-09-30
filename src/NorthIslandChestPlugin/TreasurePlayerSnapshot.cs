using System.Numerics;

namespace NorthIslandChestPlugin;

internal readonly record struct TreasurePlayerSnapshot(Vector3? Position, bool CanMove, bool NearbyPlayer, bool Mounted, bool Mounting);
