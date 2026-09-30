using System;
using System.Collections.Generic;
using System.Linq;

namespace NorthIslandChestPlugin;

internal static class CurrencyPurchasePlan
{
	internal static int ConfiguredMaximum(CurrencyShopItem item, int trigger, PurchaseQuantityMode mode)
	{
		if (item.Cost <= 0 || item.ReceiveCount <= 0)
		{
			return 0;
		}
		int num = Math.Max(0, trigger) / item.Cost;
		if (item.Unique)
		{
			num = Math.Min(num, 1 / item.ReceiveCount);
		}
		return num;
	}

	internal static int ClampConfiguredQuantity(CurrencyShopItem item, int trigger, PurchaseQuantityMode mode, int quantity)
	{
		int num = ConfiguredMaximum(item, trigger, mode);
		if (num != 0)
		{
			return Math.Clamp(quantity, 1, num);
		}
		return 0;
	}

	internal static bool NormalizeOptions(IReadOnlyList<CurrencyShopItem> catalog, Dictionary<string, CurrencyItemOption> options, int trigger)
	{
		bool result = false;
		foreach (CurrencyShopItem item in catalog)
		{
			if (options.TryGetValue(item.Key, out CurrencyItemOption value) && value != null)
			{
				int num = ClampConfiguredQuantity(item, trigger, value.Mode, value.Quantity);
				bool flag = value.Mode != PurchaseQuantityMode.PerBatch;
				if (num != value.Quantity || flag)
				{
					value.Quantity = num;
					value.Mode = PurchaseQuantityMode.PerBatch;
					result = true;
				}
			}
		}
		return result;
	}

	internal static int Quantity(int requested, PurchaseQuantityMode mode, int owned, int budget, int cost, int receiveCount, bool unique, int capacity)
	{
		if (requested <= 0 || owned < 0 || budget < 0 || cost <= 0 || receiveCount <= 0 || capacity <= 0)
		{
			return 0;
		}
		int val = ((mode == PurchaseQuantityMode.TargetOwned) ? (Math.Max(0, requested - owned) / receiveCount) : requested);
		if (unique)
		{
			capacity = Math.Min(capacity, Math.Max(0, 1 - owned));
		}
		return Math.Max(0, Math.Min(val, Math.Min(budget / cost, capacity / receiveCount)));
	}

	internal static List<CurrencyPurchaseRequest> Build(IslandProfile profile, Plugin.PurchaseSettings settings, Func<CurrencyKind, IReadOnlyList<CurrencyShopItem>> catalog, Func<uint, int> count, PurchaseBag? bag = null)
	{
		List<CurrencyPurchaseRequest> list = new List<CurrencyPurchaseRequest>();
		Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
		CurrencyKind[] values = Enum.GetValues<CurrencyKind>();
		foreach (CurrencyKind currencyKind in values)
		{
			bool flag = currencyKind == CurrencyKind.Silver;
			uint num = (flag ? profile.SilverCurrencyItemId : profile.GoldCurrencyItemId);
			int num2 = count(num);
			int num3 = (flag ? settings.SilverTriggerAmount : settings.GoldTriggerAmount);
			if (num2 < num3)
			{
				continue;
			}
			Dictionary<string, CurrencyItemOption> options = (flag ? settings.SilverItems : settings.GoldItems);
			foreach (CurrencyShopItem item in from x in catalog(currencyKind)
				where options.TryGetValue(x.Key, out CurrencyItemOption value) && (value?.Enabled ?? false)
				orderby options[x.Key].Priority descending, x.EventId, x.ItemId
				select x)
			{
				CurrencyItemOption currencyItemOption = options[item.Key];
				int num4 = count(item.ItemId);
				if (num4 >= 0)
				{
					num4 += dictionary.GetValueOrDefault(item.ItemId);
					int num5 = Quantity(ClampConfiguredQuantity(item, num3, PurchaseQuantityMode.PerBatch, currencyItemOption.Quantity), PurchaseQuantityMode.PerBatch, num4, num2, item.Cost, item.ReceiveCount, item.Unique, bag?.Capacity(item.ItemId, item.HighQuality, item.StackSize) ?? int.MaxValue);
					if (num5 != 0)
					{
						list.Add(new CurrencyPurchaseRequest(currencyKind, flag ? profile.SilverCurrencyName : profile.GoldCurrencyName, num, item.EventId, item.Name, item.ItemId, item.Cost, num5, item.ReceiveCount, item.HighQuality, item.StackSize, item.Unique));
						num2 -= num5 * item.Cost;
						dictionary[item.ItemId] = dictionary.GetValueOrDefault(item.ItemId) + num5 * item.ReceiveCount;
						bag?.Reserve(item.ItemId, item.HighQuality, item.StackSize, num5 * item.ReceiveCount);
					}
				}
			}
		}
		return list;
	}
}
