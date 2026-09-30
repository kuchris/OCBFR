namespace NorthIslandChestPlugin;

public sealed class CurrencyItemOption
{
	public bool Enabled { get; set; }

	public int Quantity { get; set; } = 1;

	public PurchaseQuantityMode Mode { get; set; }

	public int Priority { get; set; } = 100;
}
