namespace NorthIslandChestPlugin;

internal sealed record CurrencyShopItem(uint EventId, uint ItemId, string Name, int Cost, int ReceiveCount, bool HighQuality, int StackSize, bool Unique)
{
	internal string Key => $"{EventId}:{ItemId}:{(HighQuality ? 1 : 0)}";

	internal bool Matches(CurrencyPurchaseRequest request)
	{
		if (EventId == request.EventId && ItemId == request.RewardItemId && Cost == request.Cost && ReceiveCount == request.ReceiveCount && HighQuality == request.HighQuality && StackSize == request.StackSize)
		{
			return Unique == request.Unique;
		}
		return false;
	}
}
