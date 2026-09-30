using System.Numerics;

namespace NorthIslandChestPlugin;

internal sealed class IslandProfile
{
	internal const uint NorthTerritoryId = 1346u;

	internal const uint SouthTerritoryId = 1252u;

	public required IslandTarget Target { get; init; }

	public required uint TerritoryId { get; init; }

	public required string ChapterName { get; init; }

	public required string EntryCommand { get; init; }

	public required Vector3 CrystalMoveTarget { get; init; }

	public required Vector3 CurrencyExchangeAnchor { get; init; }

	public required string[] ShardKeywords { get; init; }

	public required uint SilverCurrencyItemId { get; init; }

	public required uint GoldCurrencyItemId { get; init; }

	public required uint HealthCheckCurrencyItemId { get; init; }

	public required string SilverCurrencyName { get; init; }

	public required string GoldCurrencyName { get; init; }

	public required string HealthCheckCurrencyName { get; init; }

	public required uint SilverEventId { get; init; }

	public required uint GoldEventId { get; init; }

	public bool SupportsFixative { get; init; }

	internal static IslandProfile North { get; } = new IslandProfile
	{
		Target = IslandTarget.NorthHorn,
		TerritoryId = 1346u,
		ChapterName = "蜃景幻界新月岛 北征之章",
		EntryCommand = "/pdrfe ocn",
		CrystalMoveTarget = new Vector3(882f, 258.5f, 882f),
		CurrencyExchangeAnchor = new Vector3(882f, 258.5f, 882f),
		// [GLOBAL] 小水晶改用 ptp 數字索引（Daily Routines 的 ptp 比對的是
		// LuminaWrapper.GetPlaceName，跟隨客戶端語言，中文名在英文客戶端配對不到）。
		// 對應由遊戲 PlaceName 表核實（日文漢字 == 中文關鍵字）：
		//   妖火 -> 5 (Unhallowed Hamlet,  妖火の漁村)
		//   城塞 -> 1 (The Crown of Karnak, カルナック城塞)
		//   圣堂 -> 2 (Sinking Sanctuary,  沈んだ聖堂前)
		//   遗迹 -> 3 (Suspended Masonry,  浮遊遺跡)
		//   街道 -> 4 (Moldering Outskirts, 腐敗した市街地前)
		ShardKeywords = new string[5] { "5", "1", "2", "3", "4" },
		SilverCurrencyItemId = 51975u,
		GoldCurrencyItemId = 51976u,
		HealthCheckCurrencyItemId = 51975u,
		SilverCurrencyName = "十二城邦白银币",
		GoldCurrencyName = "十二城邦白金币",
		HealthCheckCurrencyName = "十二城邦白银币",
		SilverEventId = 1771028u,
		GoldEventId = 1771029u,
		SupportsFixative = true
	};

	internal static IslandProfile South { get; } = new IslandProfile
	{
		Target = IslandTarget.SouthHorn,
		TerritoryId = 1252u,
		ChapterName = "蜃景幻界新月岛 南征之章",
		EntryCommand = "/pdrfe ocs",
		CrystalMoveTarget = new Vector3(834f, 73f, -696f),
		CurrencyExchangeAnchor = new Vector3(834f, 73f, -696f),
		// [GLOBAL] 小水晶改用 ptp 數字索引（原因同北征之章）。
		//   遗迹 -> 1 (The Wanderer's Haven, 放浪神聖域跡前)
		//   洞窟 -> 2 (Crystallized Caverns, 水晶洞窟前)
		//   古树 -> 3 (Eldergrowth, 古樹の湿原前)
		//   石塔 -> 4 (Stonemarsh, 石塔水沼前)
		ShardKeywords = new string[4] { "1", "2", "3", "4" },
		SilverCurrencyItemId = 45043u,
		GoldCurrencyItemId = 45044u,
		HealthCheckCurrencyItemId = 45043u,
		SilverCurrencyName = "十二城邦银币",
		GoldCurrencyName = "十二城邦金币",
		HealthCheckCurrencyName = "十二城邦银币",
		SilverEventId = 1770928u,
		GoldEventId = 1770930u,
		SupportsFixative = false
	};

	internal static IslandProfile Resolve(IslandTarget target)
	{
		if (target != IslandTarget.SouthHorn)
		{
			return North;
		}
		return South;
	}
}
