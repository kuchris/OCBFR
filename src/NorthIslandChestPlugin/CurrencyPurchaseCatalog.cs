using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace NorthIslandChestPlugin;

internal sealed class CurrencyPurchaseCatalog
{
	private readonly Dictionary<(IslandTarget, CurrencyKind), IReadOnlyList<CurrencyShopItem>> cache = new Dictionary<(IslandTarget, CurrencyKind), IReadOnlyList<CurrencyShopItem>>();

	internal static uint[] Shops(IslandTarget island, CurrencyKind kind)
	{
		switch (island)
		{
		case IslandTarget.NorthHorn:
			switch (kind)
			{
			case CurrencyKind.Silver:
				return new uint[2] { 1771027u, 1771028u };
			case CurrencyKind.Gold:
				return new uint[1] { 1771029u };
			}
			break;
		case IslandTarget.SouthHorn:
			switch (kind)
			{
			case CurrencyKind.Silver:
				return new uint[4] { 1770927u, 1770928u, 1770929u, 1770946u };
			case CurrencyKind.Gold:
				return new uint[3] { 1770930u, 1770931u, 1770947u };
			}
			break;
		}
		return Array.Empty<uint>();
	}

	internal IReadOnlyList<CurrencyShopItem> Get(IslandProfile profile, CurrencyKind kind)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		(IslandTarget, CurrencyKind) key = (profile.Target, kind);
		if (cache.TryGetValue(key, out IReadOnlyList<CurrencyShopItem> value))
		{
			return value;
		}
		uint currencyId = ((kind == CurrencyKind.Silver) ? profile.SilverCurrencyItemId : profile.GoldCurrencyItemId);
		List<CurrencyShopItem> list = new List<CurrencyShopItem>();
		uint[] array = Shops(profile.Target, kind);
		foreach (uint num in array)
		{
			SpecialShop shop = LuminaGetter.GetRow<SpecialShop>(num) ?? throw new InvalidOperationException($"商店数据不可用：{num}");
			list.AddRange(Read(shop, currencyId, (uint id) => LuminaGetter.GetRow<Item>(id)));
		}
		cache[key] = list;
		return list;
	}

	internal static List<CurrencyShopItem> Read(SpecialShop shop, uint currencyId, Func<uint, Item?> getItem)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		List<CurrencyShopItem> list = new List<CurrencyShopItem>();
		var enumerator = shop.Item.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				SpecialShop.ItemStruct current = enumerator.Current;
				SpecialShop.ItemStruct.ReceiveItemsStruct[] array = ((IEnumerable<SpecialShop.ItemStruct.ReceiveItemsStruct>)(object)current.ReceiveItems).Where((SpecialShop.ItemStruct.ReceiveItemsStruct x) =>
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					return x.Item.RowId != 0;
				}).ToArray();
				SpecialShop.ItemStruct.ItemCostsStruct[] array2 = ((IEnumerable<SpecialShop.ItemStruct.ItemCostsStruct>)(object)current.ItemCosts).Where((SpecialShop.ItemStruct.ItemCostsStruct x) =>
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					return x.ItemCost.RowId != 0 || x.CurrencyCost != 0;
				}).ToArray();
				bool flag = array.Length != 1 || array2.Length != 1 || array2[0].ItemCost.RowId != currencyId;
				uint currencyCost;
				if (!flag)
				{
					currencyCost = array2[0].CurrencyCost;
					bool flag2 = ((currencyCost > int.MaxValue || currencyCost == 0) ? true : false);
					flag = flag2;
				}
				if (flag || array2[0].CollectabilityCost != 0 || array2[0].CostType != 0)
				{
					continue;
				}
				SpecialShop.ItemStruct.ReceiveItemsStruct val = array[0];
				currencyCost = val.ReceiveCount;
				if ((currencyCost <= int.MaxValue && currencyCost != 0) || 1 == 0)
				{
					Item? val2 = getItem(val.Item.RowId);
					if (!val2.HasValue)
					{
						throw new InvalidOperationException($"物品数据不可用：{val.Item.RowId}");
					}
					Item value = val2.Value;
					string name = ((object)value.Name/*cast due to constrained. prefix*/).ToString();
					if (!IsSoulFragment(val.Item.RowId, name))
					{
						uint rowId = shop.RowId;
						uint rowId2 = val.Item.RowId;
						uint currencyCost2 = array2[0].CurrencyCost;
						uint receiveCount = val.ReceiveCount;
						bool receiveHq = val.ReceiveHq;
						value = val2.Value;
						int stackSize = checked((int)value.StackSize);
						value = val2.Value;
						list.Add(new CurrencyShopItem(rowId, rowId2, name, (int)currencyCost2, (int)receiveCount, receiveHq, stackSize, value.IsUnique));
					}
				}
			}
			return list;
		}
		finally
		{
			((IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
		}
	}

	internal static bool IsSoulFragment(uint id, string name)
	{
		bool flag;
		switch (id)
		{
		case 47751u:
		case 47752u:
		case 47753u:
		case 47754u:
		case 47755u:
		case 47756u:
		case 47757u:
		case 48748u:
		case 48749u:
		case 49823u:
		case 49824u:
		case 49825u:
		case 51967u:
		case 51968u:
		case 51969u:
		case 51970u:
		case 51971u:
		case 51972u:
		case 51973u:
		case 51974u:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return name.Contains("灵魂碎晶", StringComparison.Ordinal);
		}
		return true;
	}
}
