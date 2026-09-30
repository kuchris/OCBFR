using System;
using System.Collections.Generic;
using System.Linq;

namespace NorthIslandChestPlugin;

internal sealed class PurchaseBag(int emptySlots, IEnumerable<(uint ItemId, bool HighQuality, int Quantity)> stacks)
{
	private int empty = emptySlots;

	private readonly List<(uint ItemId, bool HighQuality, int Quantity)> slots = stacks.ToList();

	internal int Capacity(uint itemId, bool hq, int stackSize)
	{
		if (stackSize <= 0)
		{
			return 0;
		}
		long num = slots.Where(((uint ItemId, bool HighQuality, int Quantity) x) => x.ItemId == itemId && x.HighQuality == hq).Sum(((uint ItemId, bool HighQuality, int Quantity) x) => Math.Max(0L, (long)stackSize - (long)x.Quantity));
		return (int)Math.Min(2147483647L, num + (long)empty * (long)stackSize);
	}

	internal void Reserve(uint itemId, bool hq, int stackSize, int quantity)
	{
		if (quantity < 0 || quantity > Capacity(itemId, hq, stackSize))
		{
			throw new InvalidOperationException("Insufficient inventory capacity");
		}
		for (int i = 0; i < slots.Count; i++)
		{
			if (quantity <= 0)
			{
				break;
			}
			(uint, bool, int) tuple = slots[i];
			if (tuple.Item1 == itemId && tuple.Item2 == hq)
			{
				int num = Math.Min(quantity, Math.Max(0, stackSize - tuple.Item3));
				slots[i] = (itemId, hq, tuple.Item3 + num);
				quantity -= num;
			}
		}
		while (quantity > 0)
		{
			int num2 = Math.Min(quantity, stackSize);
			slots.Add((itemId, hq, num2));
			empty--;
			quantity -= num2;
		}
	}
}
