namespace NorthIslandChestPlugin;

internal readonly record struct CurrencyPurchaseRequest(CurrencyKind Currency, string CurrencyName, uint CurrencyItemId, uint EventId, string RewardName, uint RewardItemId, int Cost, int Quantity, int ReceiveCount = 1, bool HighQuality = false, int StackSize = 999, bool Unique = false, int TargetOwned = 0);
