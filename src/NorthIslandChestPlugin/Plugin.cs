using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Configuration;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MKDSupportJob = Lumina.Excel.Sheets.MKDSupportJob;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin : IDalamudPlugin, IDisposable
{
	private enum TreasurePhase
	{
		None,
		FirstMove,
		FirstMoveDelay,
		FirstCrystal,
		FirstWaitPlayers,
		XszRunning,
		InnerReturn,
		SecondMove,
		SecondMoveDelay,
		SecondCrystal,
		SecondWaitPlayers,
		OuterReturn,
		LeaveDuty,
		Reentry
	}

	public sealed class PurchaseSettings
	{
		public int ItemsVersion { get; set; }

		public Dictionary<string, CurrencyItemOption> SilverItems { get; set; } = new Dictionary<string, CurrencyItemOption>();

		public Dictionary<string, CurrencyItemOption> GoldItems { get; set; } = new Dictionary<string, CurrencyItemOption>();

		public CurrencyPurchaseMode SilverMode { get; set; }

		public CurrencyPurchaseMode GoldMode { get; set; }

		public int SilverTriggerAmount { get; set; } = 9000;

		public int GoldTriggerAmount { get; set; } = 9000;

		public int SilverCofferQuantity { get; set; } = 20;

		public int GoldCofferQuantity { get; set; } = 20;

		public int SilverFixativeQuantity { get; set; } = 1;

		public int GoldFixativeQuantity { get; set; } = 1;
	}

	public sealed class PluginConfig : IPluginConfiguration
	{
		[NonSerialized]
		private IDalamudPluginInterface? pluginInterface;

		public int Version { get; set; } = 5;

		public UiLanguage UiLanguage { get; set; } = UiLanguage.TraditionalChinese;

		public IslandTarget IslandTarget { get; set; } = IslandTarget.NorthHorn;

		public TreasureMode TreasureModeSelection { get; set; }

		// [GLOBAL] 幻境職業以英文名儲存（見 CombatJobs 的說明）；預設 Phantom White Mage。
		// 舊設定檔若仍存中文名，載入時會因不在清單內而回退為預設值。
		public string CombatJob { get; set; } = "Phantom White Mage";

		public string DiscardPreset { get; set; } = "";

		public bool AutoPurchaseExpanded { get; set; } = true;

		public string PurchaseSearch { get; set; } = "";

		public bool PurchaseEnabledOnly { get; set; }










		public bool SimplifiedUi { get; set; }

		public float WindowWidth { get; set; }

		public float WindowHeight { get; set; }

		public float SimplifiedWindowWidth { get; set; }

		public float SimplifiedWindowHeight { get; set; }

		public Dictionary<string, WindowLayout> WindowLayouts { get; set; } = new Dictionary<string, WindowLayout>();

		public CurrencyPurchaseMode SilverPurchaseMode { get; set; }

		public CurrencyPurchaseMode GoldPurchaseMode { get; set; }

		public int SilverTriggerAmount { get; set; } = 9000;

		public int GoldTriggerAmount { get; set; } = 9000;

		public int SilverCofferQuantity { get; set; } = 20;

		public int GoldCofferQuantity { get; set; } = 20;

		public int SilverFixativeQuantity { get; set; } = 1;

		public int GoldFixativeQuantity { get; set; } = 1;

		public PurchaseSettings NorthPurchase { get; set; } = new PurchaseSettings();

		public PurchaseSettings SouthPurchase { get; set; } = new PurchaseSettings();

		public void Initialize(IDalamudPluginInterface pluginInterface)
		{
			this.pluginInterface = pluginInterface;
		}

		public void Save()
		{
			IDalamudPluginInterface? val = pluginInterface;
			if (val != null)
			{
				val.SavePluginConfig((IPluginConfiguration)(object)this);
			}
		}
	}

	private enum TitleBarIcon
	{
		Play,
		Stop,
		FullView,
		CompactView,
		Sponsor
	}

	private sealed class MainWindow : PersistentWindow
	{
		private bool titleBarSpacingPushed;

		public MainWindow(Plugin plugin, bool simplified = false)
			: base(plugin, "OCBFR v" + PluginVersion + "###OCNFarmer" + (simplified ? "Simplified" : "Full"), simplified ? "Simplified" : "Full", simplified ? DefaultSimplifiedWindowSize : DefaultFullWindowSize, simplified ? new Vector2(520f, 330f) : new Vector2(760f, 580f))
		{
		}

		public override void PreDraw()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			base.PreDraw();
			ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.045f, 0.045f, 0.045f, 0.98f));
			ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.035f, 0.035f, 0.035f, 1f));
			ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.075f, 0.075f, 0.075f, 1f));
			ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.22f, 0.22f, 0.22f, 1f));
			titleBarSpacingPushed = true;
		}

		protected override void DrawContents()
		{
			Owner.DrawStatus();
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (titleBarSpacingPushed)
			{
				ImGui.PopStyleColor(4);
				titleBarSpacingPushed = false;
			}
		}
	}


	public sealed class WindowLayout
	{
		public float Width { get; set; }

		public float Height { get; set; }

		public float? X { get; set; }

		public float? Y { get; set; }
	}

	private abstract class PersistentWindow : Window
	{
		protected readonly Plugin Owner;

		private readonly WindowLayout layout;

		private readonly Vector2 defaultSize;

		private bool restoreLayout = true;

		protected PersistentWindow(Plugin plugin, string title, string layoutKey, Vector2 defaultSize, Vector2? minimumSize)
			: base(title)
		{
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			Owner = plugin;
			this.defaultSize = defaultSize;
			PluginConfig config = plugin.config;
			if (config.WindowLayouts == null)
			{
				Dictionary<string, WindowLayout> dictionary = (config.WindowLayouts = new Dictionary<string, WindowLayout>());
			}
			if (!plugin.config.WindowLayouts.TryGetValue(layoutKey, out WindowLayout value) || value == null)
			{
				value = new WindowLayout();
				if (layoutKey == "Full")
				{
					value.Width = plugin.config.WindowWidth;
					value.Height = plugin.config.WindowHeight;
				}
				else if (layoutKey == "Simplified")
				{
					value.Width = plugin.config.SimplifiedWindowWidth;
					value.Height = plugin.config.SimplifiedWindowHeight;
				}
				plugin.config.WindowLayouts[layoutKey] = value;
			}
			layout = value;
			((Window)this).Flags = (ImGuiWindowFlags)((int)((Window)this).Flags | 0x120);
			WindowSizeConstraints? sizeConstraints;
			if (minimumSize.HasValue)
			{
				Vector2 valueOrDefault = minimumSize.GetValueOrDefault();
				WindowSizeConstraints value2 = new WindowSizeConstraints();
				value2.MinimumSize = valueOrDefault;
				value2.MaximumSize = new Vector2(4000f, 3000f);
				sizeConstraints = value2;
			}
			else
			{
				sizeConstraints = null;
			}
			((Window)this).SizeConstraints = sizeConstraints;
		}

		public override void OnOpen()
		{
			restoreLayout = true;
		}

		public override void PreDraw()
		{
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			if (!restoreLayout)
			{
				return;
			}
			Vector2 vector = new Vector2(layout.Width, layout.Height);
			bool flag = float.IsFinite(vector.X) && float.IsFinite(vector.Y) && vector.X > 0f && vector.Y > 0f;
			WindowSizeConstraints? sizeConstraints = ((Window)this).SizeConstraints;
			if (sizeConstraints.HasValue)
			{
				WindowSizeConstraints valueOrDefault = sizeConstraints.GetValueOrDefault();
				flag &= vector.X >= valueOrDefault.MinimumSize.X && vector.Y >= valueOrDefault.MinimumSize.Y;
				vector = Vector2.Min(vector, valueOrDefault.MaximumSize);
			}
			((Window)this).Size = (flag ? vector : defaultSize);
			((Window)this).SizeCondition = (ImGuiCond)1;
			float? x = layout.X;
			Vector2? position;
			if (x.HasValue)
			{
				float valueOrDefault2 = x.GetValueOrDefault();
				x = layout.Y;
				if (x.HasValue)
				{
					float valueOrDefault3 = x.GetValueOrDefault();
					if (float.IsFinite(valueOrDefault2) && float.IsFinite(valueOrDefault3))
					{
						position = new Vector2(valueOrDefault2, valueOrDefault3);
						goto IL_013c;
					}
				}
			}
			position = null;
			goto IL_013c;
			IL_013c:
			((Window)this).Position = position;
			((Window)this).PositionCondition = (ImGuiCond)1;
		}

		public sealed override void Draw()
		{
			if (restoreLayout)
			{
				restoreLayout = false;
				((Window)this).Size = null;
				((Window)this).Position = null;
			}
			else
			{
				Vector2 vector = ImGui.GetWindowSize() / ImGuiHelpers.GlobalScale;
				Vector2 windowPos = ImGui.GetWindowPos();
				if (float.IsFinite(vector.X) && float.IsFinite(vector.Y) && vector.X > 0f && vector.Y > 0f)
				{
					if (!(MathF.Abs(layout.Width - vector.X) > 0.5f) && !(MathF.Abs(layout.Height - vector.Y) > 0.5f))
					{
						float? x = layout.X;
						if (x.HasValue)
						{
							float valueOrDefault = x.GetValueOrDefault();
							x = layout.Y;
							if (x.HasValue)
							{
								float valueOrDefault2 = x.GetValueOrDefault();
								if (!(MathF.Abs(valueOrDefault - windowPos.X) > 0.5f) && !(MathF.Abs(valueOrDefault2 - windowPos.Y) > 0.5f))
								{
									goto IL_0186;
								}
							}
						}
					}
					layout.Width = vector.X;
					layout.Height = vector.Y;
					layout.X = windowPos.X;
					layout.Y = windowPos.Y;
					Owner.MarkWindowLayoutsDirty();
				}
			}
			goto IL_0186;
			IL_0186:
			DrawContents();
		}

		protected abstract void DrawContents();

		public override void OnClose()
		{
			Owner.FlushWindowLayouts(force: true);
		}
	}

	private static readonly string PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.9.8.0";

	private static readonly IReadOnlyDictionary<string, int> LootStarLevels = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		["无瑕白染剂"] = 1,
		["煤玉黑染剂"] = 1,
		["柔彩粉染剂"] = 1,
		["垂直霓虹墙灯"] = 1,
		["魔法飞床"] = 1,
		["火巨人角笛"] = 1,
		["优雷卡盐蓝燕角笛"] = 1,
		["演技教材·好冷"] = 1,
		["发型样式：发箍式编发"] = 1,
		["劳动十四号认证密钥"] = 1,
		["演技教材·巡视"] = 1,
		["发型样式：飞翔者"] = 1,
		["发型样式：黎明辫"] = 1,
		["恐爪龙角笛"] = 1,
		["加百列III号机认证密钥"] = 1,
		["发型样式：侧马尾辫"] = 1,
		["次品十二城邦金币"] = 1,
		["发型样式：长发"] = 1,
		["发型样式：基拉巴尼亚编发"] = 1,
		["演技教材·陆行鸟之笔"] = 1,
		["大天使之翼"] = 1,
		["发型样式：麻花辫丸子头"] = 1,
		["好运胡萝卜"] = 2,
		["安静蜂鸟笛"] = 2,
		["渡渡鸟角笛"] = 2,
		["水平霓虹墙灯"] = 2,
		["力之新月魔耳饰"] = 3,
		["力之新月魔项链"] = 3,
		["力之新月魔手镯"] = 3,
		["魔之新月魔耳饰"] = 3,
		["魔之新月魔项链"] = 3,
		["魔之新月魔手镯"] = 3
	};

	// [GLOBAL] 幻境職業：改用英文名（MKDSupportJob.NameEnglish）。
	// 原因：Daily Routines 的 /pdr pjob（PhantomJobSwitchCommand）比對「客戶端語言名稱」與 NameEnglish，
	// 而跟隨客戶端語言讀取資料→ 英文客戶端下中文職業名配對不到，會靜靜失敗。
	// 英文名在任何客戶端語言都配對得到，且下拉選單顯示正常（不像純數字 ID）。
	// 名稱由遊戲 MKDSupportJob 表以 Lumina 讀出（Language.English）。
	private string[] CombatJobs = new string[23]
	{
		"Phantom White Mage", "Phantom Samurai", "Phantom Ranger", "Phantom Monk", "Phantom Berserker",
		"Phantom Knight", "Phantom Chemist", "Phantom Cannoneer", "Phantom Time Mage", "Phantom Geomancer",
		"Phantom Bard", "Phantom Dancer", "Phantom Gladiator", "Phantom Mystic Knight", "Phantom Thief",
		"Phantom Oracle", "Phantom Summoner", "Phantom Dragoon", "Phantom Black Mage", "Phantom Ninja",
		"Phantom Necromancer", "Phantom Red Mage", "Phantom Blue Mage"
	};

	private string freelancerJobName = "Phantom Freelancer";

	private void LoadEnglishPhantomJobs()
	{
		try
		{
			var jobs = data.GetExcelSheet<MKDSupportJob>(Dalamud.Game.ClientLanguage.English);
			string freelancer = jobs.GetRow(0).NameEnglish.ToString();
			string[] combat = jobs.Where(job => job.RowId != 0)
				.Select(job => job.NameEnglish.ToString()).Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.Ordinal).ToArray();
			if (string.IsNullOrWhiteSpace(freelancer) || combat.Length == 0)
			{
				throw new InvalidOperationException("MKDSupportJob.NameEnglish is empty");
			}
			freelancerJobName = freelancer;
			CombatJobs = combat;
			log.Information($"[Global] 已读取幻境职业英文名：{freelancerJobName}，战斗职业 {CombatJobs.Length} 个", Array.Empty<object>());
		}
		catch (Exception ex)
		{
			log.Warning(ex, "读取幻境职业英文名失败，使用已核对的 Global 名称", Array.Empty<object>());
		}
	}

	private const uint TreasureGeneralActionSlot = 32u;

	private const ulong GeneralActionTarget = 3758096384uL;

	private const int CurrencyCap = 9999;

	private const string LegacyInternalName = "NorthIslandChestPlugin";

	private const uint UltimateFixativeItemId = 51978u;

	private const uint OldCofferItemId = 47740u;

	private const int MaxSilver = 8;

	private const int MaxCopper = 30;

	private const float BaseX = 39f;

	private const float BaseZ = 39f;

	private const float BaseRadius = 18f;

	private static readonly TimeSpan SubsequentScanInterval = TimeSpan.FromMinutes(10L);

	// [GLOBAL] 由 1 秒改為 5 秒。插件切換幻境職業後等這個時間才施放魔尋寶；
	// 但入島後職業系統需要數秒初始化，期間遊戲會回
	// "You are unable to change phantom jobs at this time."，切換會靜靜失敗，
	// 角色停留在戰鬥職業 → 動作 41651 不存在 → UseAction 失敗。
	private static readonly TimeSpan JobChangeDelay = TimeSpan.FromSeconds(5L);

	private static readonly TimeSpan ReturnScanDelay = TimeSpan.FromSeconds(5L);

	private static readonly TimeSpan CurrencyPurchaseDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan CurrencyPurchaseRetryInterval = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan CurrencyPurchaseRetryTimeout = TimeSpan.FromSeconds(10L);

	private static readonly TimeSpan CurrencyPurchaseMovePollInterval = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan CurrencyPurchaseMoveStartDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan TreasureCommandDelay = TimeSpan.FromMilliseconds(500L);



	private static readonly TimeSpan CrystalMoveTimeout = TimeSpan.FromMinutes(3L);

	private static readonly TimeSpan MountRetryInterval = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan MountRetryTimeout = TimeSpan.FromSeconds(12L);

	private static readonly TimeSpan XszPositionPollInterval = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan XszNoMovementTimeout = TimeSpan.FromSeconds(10L);

	private const uint MountRouletteGeneralActionSlot = 9u;







	private readonly IChatGui chat;

	private readonly IClientState clientState;

	private readonly IObjectTable objects;

	private readonly ICommandManager commands;

	private readonly IFramework framework;

	private readonly ICondition condition;

	private readonly IGameGui gameGui;

	private readonly IPluginLog log;

	private readonly PluginConfig config;

	private IslandProfile activeProfile = IslandProfile.North;

	private readonly CurrencyBuyer currencyBuyer;

	private readonly WindowSystem windows = new WindowSystem("OCNFarmer");

	private readonly MainWindow mainWindow;

	private readonly TreasureHistoryWindow treasureHistoryWindow;

	private readonly List<TreasureRecord> treasureRecords = new List<TreasureRecord>();

	private readonly string treasureRecordPath;

	private readonly string legacyTreasureRecordPath;

	private DateTime pendingScanAt = DateTime.MinValue;

	private bool freelancerJobChangeRequested;

	private DateTime pendingBocchiAt = DateTime.MinValue;

	private DateTime pendingReturnScanAt = DateTime.MinValue;

	private DateTime pendingCurrencyCheckAt = DateTime.MinValue;

	private DateTime pendingPurchaseAt = DateTime.MinValue;

	private DateTime purchaseFailureCooldownUntil = DateTime.MinValue;

	private DateTime purchaseRetryDeadline = DateTime.MinValue;

	private DateTime currencyPurchaseMoveDeadline = DateTime.MinValue;

	private DateTime nextCurrencyPurchaseMoveCheckAt = DateTime.MinValue;

	private DateTime currencyPurchaseMoveStartAt = DateTime.MinValue;

	private bool initialCurrencyCheckPending;

	private string initialCurrencyCheckSource = "首次进岛";

	private DateTime nextAllowedScanAt = DateTime.MinValue;

	private DateTime treasurePhaseAt = DateTime.MinValue;

	private TreasurePhase treasurePhase;

	private string combatJob = "Phantom White Mage";

	private string discardPreset = "";

	private string currentCrystal = "";


	private bool innerLeg;

	private string treasureError = "";

	private bool running;

	private bool bocchiEnabled;

	private bool currencyPurchaseMoveActive;

	private bool islandSwitchPending;

	private bool waitingForEntry;

	private bool entrySyncMessageSeen;

	private bool waitingForScan;

	private bool initialScan;

	private uint observedTerritory;

	private bool islandRecoveryPending;

	// [DEBUG] 「強制視為寶箱已滿」測試開關。
	// 開啟後 CompleteTreasureScan 永遠走 BeginTreasureProcedure()，
	// 所以每次進島後的首次掃描都會重新跑一次完整尋寶，
	// 用來一鍵驗證：寶箱滿 → 內環 → 外環 → 退本 → 重入 → 再掃描。
	private bool debugForceFull;

	// [DEBUG] 「不退本」測試開關：外環跑完唔退本，只結束尋寶流程。
	private bool debugNoLeaveDuty;

	// [GLOBAL] 進島後 N 分鐘自動退本重入（0 = 停用）。
	// 遊戲沒有提供「實例剩餘時間」API（ContentTimeLeft 是狀態效果計時、DutyTimer 是 UI 開關、
	// TimeLimit 是 AFK 限制），所以改用「進島後經過時間」：
	// 設定 150 分鐘就每 2.5 小時換一次新實例，避免實例到期被踢出。
	private int autoLeaveAfterMinutes;

	// 進入島嶼的時間戳（離開島嶼時重設為 MinValue）
	private DateTime islandEnteredAt;

	private int treasureCastAttempts;

	private int silver = -1;

	private int copper = -1;

	private int silverCurrency = -1;

	private int goldCurrency = -1;

	private string currencyPurchaseStatus = "";

	private readonly Dictionary<string, int> treasureLoot = new Dictionary<string, int>(StringComparer.Ordinal);

	private string status = "未运行";















	private DateTime crystalMoveDeadline = DateTime.MinValue;

	private DateTime nextCrystalMoveCheckAt = DateTime.MinValue;

	private DateTime mountRetryDeadline = DateTime.MinValue;

	private DateTime nextMountRetryAt = DateTime.MinValue;


	private Vector3 xszLastPosition;

	private DateTime xszLastPositionChangeAt = DateTime.MinValue;

	private DateTime nextXszPositionCheckAt = DateTime.MinValue;

	private readonly IDataManager data;
	private readonly CurrencyPurchaseCatalog purchaseCatalog;

	private readonly MovableWait islandSwitchMovementWait = new MovableWait();

	private readonly MovableWait treasureMovementWait = new MovableWait();



	private bool logoutHandled;

	private readonly TreasurePlayerGuard treasurePlayerGuard = new TreasurePlayerGuard();

	private static readonly Vector2 DefaultFullWindowSize = new Vector2(980f, 740f);

	private static readonly Vector2 DefaultSimplifiedWindowSize = new Vector2(560f, 360f);




	private readonly IPlayerState playerState;

	private readonly IDalamudPluginInterface pluginInterface;

	private readonly MainWindow simplifiedWindow;


	private bool disposed;



	private int runGeneration;

	private bool windowLayoutsDirty;

	private DateTime nextWindowLayoutSave;


	private const string SponsorUrl = "https://ko-fi.com/kuchris";

	private bool toggleMainUiPending;

	public string Name => "OCBFR";

	private MainWindow SelectedMainWindow
	{
		get
		{
			if (!config.SimplifiedUi)
			{
				return mainWindow;
			}
			return simplifiedWindow;
		}
	}

	private static string NormalizeLootName(string itemName)
	{
		if (string.IsNullOrWhiteSpace(itemName))
		{
			return string.Empty;
		}
		string text = itemName.Trim();
		while (text.Length > 0 && (char.GetUnicodeCategory(text[0]) == UnicodeCategory.PrivateUse || char.IsControl(text[0])))
		{
			text = text.Substring(1).TrimStart();
		}
		return text;
	}

	internal static int GetLootStarLevel(string itemName)
	{
		string text = NormalizeLootName(itemName);
		if (text.Length == 0)
		{
			return 0;
		}
		if (!LootStarLevels.TryGetValue(text, out var value))
		{
			return 0;
		}
		return value;
	}

	internal static string FormatLootName(string itemName)
	{
		string text = NormalizeLootName(itemName);
		int lootStarLevel = GetLootStarLevel(text);
		if (lootStarLevel != 0)
		{
			return new string('☆', lootStarLevel) + text;
		}
		return text;
	}

	internal static IOrderedEnumerable<KeyValuePair<string, int>> OrderLoot(IEnumerable<KeyValuePair<string, int>> loot)
	{
		return (from item in loot
			orderby GetLootStarLevel(item.Key) descending, item.Value
			select item).ThenBy((KeyValuePair<string, int> item) => item.Key, StringComparer.Ordinal);
	}


	public Plugin(IChatGui chat, IClientState clientState, IObjectTable objects, IFramework framework, ICommandManager commands, ICondition condition, IGameGui gameGui, IPluginLog log, IAddonLifecycle addonLifecycle, IDalamudPluginInterface pluginInterface, IPlayerState playerState, ITextureProvider textures, IDataManager data, ISigScanner sigScanner)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected Obj, but got Unknown
		//IL_041d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Expected Obj, but got Unknown
		//IL_0422: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		//IL_0437: Expected Obj, but got Unknown
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_0450: Expected Obj, but got Unknown
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0450: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Expected Obj, but got Unknown
		//IL_046f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Expected Obj, but got Unknown
		//IL_0474: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Expected Obj, but got Unknown
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Expected Obj, but got Unknown
		//IL_04a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ae: Expected Obj, but got Unknown
		//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Expected Obj, but got Unknown
		Plugin plugin = this;
		this.chat = chat;
		this.clientState = clientState;
		this.objects = objects;
		this.commands = commands;
		this.framework = framework;
		this.condition = condition;
		this.gameGui = gameGui;
		this.log = log;
		this.pluginInterface = pluginInterface;
		this.playerState = playerState;
		this.data = data;
		TryLoadBrandIcon(textures);
		purchaseCatalog = new CurrencyPurchaseCatalog(data);
		LoadEnglishPhantomJobs();
		config = LoadPluginConfig(pluginInterface);
		config.Initialize(pluginInterface);
		UiText.Language = config.UiLanguage;
		string pluginConfigDirectory = pluginInterface.GetPluginConfigDirectory();
		treasureRecordPath = Path.Combine(pluginConfigDirectory, "treasure-records.json");
		legacyTreasureRecordPath = Path.Combine(GetPluginConfigRoot(pluginConfigDirectory), "NorthIslandChestPlugin", "treasure-records.json");
		LoadTreasureRecords();
		combatJob = (CombatJobs.Contains(config.CombatJob, StringComparer.Ordinal) ? config.CombatJob : combatJob);
		discardPreset = config.DiscardPreset ?? "";
		NormalizePurchaseConfig();
		ApplySelectedProfile();
		currencyBuyer = new CurrencyBuyer(clientState, objects, condition, gameGui, addonLifecycle, log, OnCurrencyPurchaseFinished, data, new ShopEventBridge(sigScanner, log));
		mainWindow = new MainWindow(this);
		simplifiedWindow = new MainWindow(this, simplified: true);
		treasureHistoryWindow = new TreasureHistoryWindow(this);
		windows.AddWindow((IWindow)(object)mainWindow);
		windows.AddWindow((IWindow)(object)simplifiedWindow);
		windows.AddWindow((IWindow)(object)treasureHistoryWindow);
		commands.AddHandler("/ocbchest", new CommandInfo((IReadOnlyCommandInfo.HandlerDelegate)((string _, string _) =>
		{
			plugin.OpenMainUi();
		}))
		{
			HelpMessage = "開啟 OCBFR 設定 / Open OCBFR settings."
		});
		commands.AddHandler("/ocbstart", new CommandInfo((IReadOnlyCommandInfo.HandlerDelegate)((string _, string _) =>
		{
			plugin.Start();
		}))
		{
			HelpMessage = "啟動 OCBFR 自動流程 / Start the OCBFR workflow."
		});
		commands.AddHandler("/ocbstop", new CommandInfo((IReadOnlyCommandInfo.HandlerDelegate)((string _, string _) =>
		{
			plugin.StopFromUser();
		}))
		{
			HelpMessage = "停止 OCBFR 自動流程 / Stop the OCBFR workflow."
		});
		chat.ChatMessage += OnChatMessage;
		clientState.Logout += OnClientLogout;
		observedTerritory = clientState.TerritoryType;
		clientState.TerritoryChanged += OnTerritoryChanged;
		framework.Update += OnUpdate;
		pluginInterface.UiBuilder.Draw += DrawWindows;
		pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
		pluginInterface.UiBuilder.OpenConfigUi += OpenMainUi;
	}

	public void Dispose()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected Obj, but got Unknown
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Expected Obj, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected Obj, but got Unknown
		if (!disposed)
		{
			disposed = true;
			clientState.Logout -= OnClientLogout;
			clientState.TerritoryChanged -= OnTerritoryChanged;
			pluginInterface.UiBuilder.Draw -= DrawWindows;
			pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
			pluginInterface.UiBuilder.OpenConfigUi -= OpenMainUi;
			if (running || currencyBuyer.IsBusy)
			{
				EmergencyStop();
			}
			currencyBuyer.Dispose();
			Stop();
			FlushWindowLayouts(force: true);
			chat.ChatMessage -= OnChatMessage;
			framework.Update -= OnUpdate;
			commands.RemoveHandler("/ocbchest");
			commands.RemoveHandler("/ocbstart");
			commands.RemoveHandler("/ocbstop");
			windows.RemoveAllWindows();
		}
	}

	public void Start()
	{
		if (!clientState.IsLoggedIn || !playerState.IsLoaded || objects.LocalPlayer == null)
		{
			status = "等待角色登录";
		}
		else
		{
			if (running || currencyBuyer.IsBusy)
			{
				return;
			}
			runGeneration++;
		logoutHandled = false;
			ApplySelectedProfile();
			uint territoryType = clientState.TerritoryType;
			running = true;
			log.Information("插件已开始运行", Array.Empty<object>());
			silver = (copper = -1);
			initialScan = true;
			if ((territoryType == 1346 || territoryType == 1252) && territoryType != activeProfile.TerritoryId)
			{
				string value = ((territoryType == 1346) ? IslandProfile.North.ChapterName : IslandProfile.South.ChapterName);
				Send("/pdr leaveduty");
				islandSwitchPending = true;
				BeginMovementWait(islandSwitchMovementWait, requireTerritoryExit: true);
				status = "切换岛屿...";
				log.Debug($"目标副本为{activeProfile.ChapterName}，已从{value}执行退本，开始检测退本及可动状态，可动满 1 秒后发送进本指令", Array.Empty<object>());
			}
			else if (!IsIsland())
			{
				BeginEntryWait("进入副本...");
			}
			else
			{
				ScheduleInitialCurrencyCheck("副本内首次", TimeSpan.Zero);
			}
		}
	}

	public void Stop(string message = "已停止")
	{
		bool num = running || currencyBuyer.IsBusy;
		islandRecoveryPending = false;
		runGeneration++;
		freelancerJobChangeRequested = false;
		if (currencyBuyer.IsBusy)
		{
			currencyBuyer.Cancel();
		}
		if (currencyPurchaseMoveActive)
		{
			Send("/vnav stop");
		}
		if (bocchiEnabled)
		{
			Send("/bocchiillegal off");
		}
		bocchiEnabled = false;
		running = (currencyPurchaseMoveActive = (islandSwitchPending = (waitingForEntry = (entrySyncMessageSeen = (waitingForScan = (initialCurrencyCheckPending = false))))));
		pendingScanAt = (pendingBocchiAt = (pendingReturnScanAt = (pendingCurrencyCheckAt = (pendingPurchaseAt = (nextAllowedScanAt = DateTime.MinValue)))));
		purchaseRetryDeadline = (currencyPurchaseMoveDeadline = (nextCurrencyPurchaseMoveCheckAt = (currencyPurchaseMoveStartAt = DateTime.MinValue)));
		islandSwitchMovementWait.Reset();
		treasureMovementWait.Reset();
		treasurePlayerGuard.Reset();
		treasurePhase = TreasurePhase.None;
		treasurePhaseAt = DateTime.MinValue;
		crystalMoveDeadline = (nextCrystalMoveCheckAt = DateTime.MinValue);
		mountRetryDeadline = (nextMountRetryAt = DateTime.MinValue);
		xszLastPosition = default;
		xszLastPositionChangeAt = (nextXszPositionCheckAt = DateTime.MinValue);
		status = message;
		if (num)
		{
			log.Information("插件已停止：" + message, Array.Empty<object>());
		}
	}

	private void EmergencyStop(string message = "已紧急停止")
	{
		Stop(message);
		Send("/bocchiillegal off");
		Send((config.TreasureModeSelection == TreasureMode.XszRun) ? "/xsz-occult-treasure stop" : "/pdr ptreasure abort");
		Send("/vnav stop");
		bocchiEnabled = false;
	}

	private void LoadTreasureRecords()
	{
		try
		{
			string text = (File.Exists(treasureRecordPath) ? treasureRecordPath : legacyTreasureRecordPath);
			if (!File.Exists(text))
			{
				return;
			}
			List<TreasureRecord> list = JsonSerializer.Deserialize<List<TreasureRecord>>(File.ReadAllText(text));
			if (list == null)
			{
				return;
			}
			treasureRecords.Clear();
			treasureRecords.AddRange(from x in list.Where((TreasureRecord x) => x != null).Select((TreasureRecord x) =>
				{
					if (x.Loot == null)
					{
						Dictionary<string, int> dictionary = (x.Loot = new Dictionary<string, int>(StringComparer.Ordinal));
					}
					return x;
				})
				orderby x.CompletedAt descending
				select x);
			if (!string.Equals(text, treasureRecordPath, StringComparison.OrdinalIgnoreCase))
			{
				string directoryName = Path.GetDirectoryName(treasureRecordPath);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(text, treasureRecordPath, overwrite: false);
				log.Debug("已从旧插件配置目录迁移寻宝战利品记录", Array.Empty<object>());
			}
		}
		catch (Exception ex)
		{
			log.Error(ex, "读取寻宝战利品记录失败，将使用空记录", Array.Empty<object>());
			treasureRecords.Clear();
		}
	}

	private PluginConfig LoadPluginConfig(IDalamudPluginInterface pluginInterface)
	{
		if (pluginInterface.GetPluginConfig() is PluginConfig result)
		{
			return result;
		}
		string text = Path.Combine(GetPluginConfigRoot(pluginInterface.GetPluginConfigDirectory()), "NorthIslandChestPlugin.json");
		if (!File.Exists(text))
		{
			return new PluginConfig();
		}
		try
		{
			PluginConfig pluginConfig = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(text));
			if (pluginConfig == null)
			{
				return new PluginConfig();
			}
			pluginConfig.Initialize(pluginInterface);
			pluginInterface.SavePluginConfig((IPluginConfiguration)(object)pluginConfig);
			log.Debug("已从旧插件配置文件迁移用户设置：" + text, Array.Empty<object>());
			return pluginConfig;
		}
		catch (Exception ex)
		{
			log.Error(ex, "迁移旧插件配置失败：" + text + "，将使用默认设置", Array.Empty<object>());
			return new PluginConfig();
		}
	}

	private static string GetPluginConfigRoot(string pluginConfigDirectory)
	{
		string text = Directory.GetParent(pluginConfigDirectory)?.FullName;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return pluginConfigDirectory;
	}

	private void SaveTreasureRecord()
	{
		try
		{
			TreasureRecord item = new TreasureRecord
			{
				CompletedAt = DateTime.Now,
				Island = activeProfile.Target,
				Mode = config.TreasureModeSelection,
				Loot = new Dictionary<string, int>(treasureLoot, StringComparer.Ordinal)
			};
			treasureRecords.Insert(0, item);
			string directoryName = Path.GetDirectoryName(treasureRecordPath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string text = treasureRecordPath + ".tmp";
			string contents = JsonSerializer.Serialize(treasureRecords, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(text, contents, Encoding.UTF8);
			if (File.Exists(treasureRecordPath))
			{
				File.Replace(text, treasureRecordPath, null);
			}
			else
			{
				File.Move(text, treasureRecordPath);
			}
			log.Information("寻宝完成，战利品已记录", Array.Empty<object>());
		}
		catch (Exception ex)
		{
			log.Error(ex, "保存寻宝战利品记录失败", Array.Empty<object>());
		}
	}

	private void OnUpdate(IFramework framework)
	{
		if (disposed)
		{
			return;
		}
		// [GLOBAL] 進島計時 / 自動退本重入 —— 已按用戶要求移除。
		// 原因：此區塊曾放在 OnUpdate 內，引發 AccessViolationException 與
		//       InvalidProgramException（兩次遊戲 crash 都來自這裡）。
		// 欄位 autoLeaveAfterMinutes 與 islandEnteredAt 保留但不再使用。
		CheckForLogout();
		if (!clientState.IsLoggedIn || !playerState.IsLoaded || objects.LocalPlayer == null)
		{
			return;
		}
		if (currencyBuyer.IsBusy)
		{
			currencyBuyer.Update();
			if (currencyBuyer.IsBusy)
			{
				status = currencyBuyer.Status;
			}
		}
		else
		{
			if (!running)
			{
				return;
			}
			if (!IsIsland() && !waitingForEntry && !islandSwitchPending && treasurePhase != TreasurePhase.Reentry && !islandRecoveryPending)
			{
				PrepareUnexpectedIslandExit();
			}
			if (islandRecoveryPending)
			{
				if (!IsPlayerMovable())
				{
					return;
				}
				islandRecoveryPending = false;
				BeginEntryWait("重新进入副本...");
				return;
			}
			if (islandSwitchPending)
			{
				if (MovementWaitReady(islandSwitchMovementWait))
				{
					islandSwitchPending = false;
					islandSwitchMovementWait.Reset();
					BeginEntryWait("进入副本...");
				}
				return;
			}
			if (treasurePhase != TreasurePhase.None)
			{
				UpdateTreasureProcedure();
				return;
			}
			if (waitingForEntry)
			{
				TryCompleteEntryHandshake();
				return;
			}
			if (currencyPurchaseMoveActive)
			{
				UpdateCurrencyPurchaseMove();
				return;
			}
			if (!IsIsland())
			{
				if (bocchiEnabled)
				{
					Send("/bocchiillegal off");
				}
				bocchiEnabled = false;
				// [GLOBAL] 原本只設定 status 就 return，導致角色只要在副本外（一般世界／幻影村）
				// 就永遠卡住、唔會重新進本。改為直接呼叫 BeginEntryWait() 自動重新進本。
				BeginEntryWait("进入副本...");
				return;
			}
			if (pendingScanAt != DateTime.MinValue && DateTime.UtcNow >= pendingScanAt)
			{
				AdvanceFreelancerScan();
			}
			if (pendingCurrencyCheckAt != DateTime.MinValue && DateTime.UtcNow >= pendingCurrencyCheckAt)
			{
				pendingCurrencyCheckAt = DateTime.MinValue;
				UpdateCurrencyCounts();
				if (HasCurrencyPurchaseRequest())
				{
					BeginCurrencyPurchasePreparation();
					return;
				}
				ContinueAfterInitialCurrencyCheck();
			}
			if (pendingPurchaseAt != DateTime.MinValue && DateTime.UtcNow >= pendingPurchaseAt)
			{
				pendingPurchaseAt = DateTime.MinValue;
				if (!(DateTime.UtcNow >= purchaseFailureCooldownUntil) || !TryBeginCurrencyPurchase())
				{
					if (HasCurrencyPurchaseRequest() && DateTime.UtcNow < purchaseRetryDeadline)
					{
						pendingPurchaseAt = DateTime.UtcNow + CurrencyPurchaseRetryInterval;
						status = "准备购买...";
						log.Debug($"自动购买暂未就绪，{CurrencyPurchaseRetryInterval.TotalSeconds:0} 秒后重试：{currencyBuyer.Status}", Array.Empty<object>());
					}
					else
					{
						string text = "自动购买未能开始：" + currencyBuyer.Status;
						log.Error($"自动购买未能在 {CurrencyPurchaseRetryTimeout.TotalSeconds:0} 秒内开始：{currencyBuyer.Status}", Array.Empty<object>());
						currencyPurchaseStatus = text;
						status = text;
						purchaseFailureCooldownUntil = DateTime.UtcNow.AddSeconds(30.0);
						RequestFreelancerScan("自动购买未能开始后继续流程");
					}
				}
				return;
			}
			if (pendingReturnScanAt != DateTime.MinValue && DateTime.UtcNow >= pendingReturnScanAt)
			{
				pendingReturnScanAt = DateTime.MinValue;
				if (!waitingForScan)
				{
					ScanTreasures();
				}
			}
			if (pendingBocchiAt != DateTime.MinValue && DateTime.UtcNow >= pendingBocchiAt)
			{
				pendingBocchiAt = DateTime.MinValue;
				Send("/bocchiillegal on");
				if (!string.IsNullOrWhiteSpace(discardPreset))
				{
					Send("/pdrdiscard " + discardPreset.Trim());
				}
				bocchiEnabled = true;
				status = "自动战斗中";
			}
		}
	}

	private void ScanTreasures()
	{
		if (!(pendingScanAt != DateTime.MinValue) && !waitingForScan)
		{
			RequestFreelancerScan("亚返回后");
		}
	}

	private void RequestFreelancerScan(string source)
	{
		if (!(pendingScanAt != DateTime.MinValue) && !waitingForScan)
		{
			freelancerJobChangeRequested = false;
			pendingBocchiAt = DateTime.MinValue;
			Send("/bocchiillegal off");
			Send("/vnav stop");
			bocchiEnabled = false;
			log.Debug($"开始{source}宝箱检测：下坐骑并切换自由人，等待就绪后使用魔寻宝 41651", Array.Empty<object>());
			// [GLOBAL] 戰鬥中無法切換幻境職業。
			// 遊戲會回 "You are unable to change phantom jobs at this time."，角色會停留在
			// 戰鬥職業，動作 41651 不存在 → UseAction 失敗 → 本次掃描作廢。
			// 正常掃描由「亞返回」觸發（亞返回必須脫戰），只有「重新入島後立即掃描」會撞上，
			// 故改為戰鬥中先延後，由 OnUpdate 的 pendingScanAt 觸發點每 5 秒重試至脫戰。
			if (condition[(ConditionFlag)26])
			{
				pendingScanAt = DateTime.UtcNow + TimeSpan.FromSeconds(5L);
				status = "战斗中，等待脱战...";
			}
			// [GLOBAL] 坐騎上無法切換幻境職業（遊戲會回 unable to change phantom jobs），
			// 所以掃描前一定要先下坐騎，否則 /pdr pjob Phantom Freelancer 失敗、
			// 動作 41651 不存在 → 本次掃描作廢（症狀：坐在坐騎上不會自己掃寶箱）。
			else if (IsMounted() || IsMounting())
			{
				TryDismount("扫描宝箱前");
				pendingScanAt = DateTime.UtcNow + JobChangeDelay;
				status = "下坐骑...";
			}
			else if (!IsIsland() || !IsPlayerMovable())
			{
				pendingScanAt = DateTime.UtcNow.AddSeconds(1);
				status = "等待角色可动后切换自由人...";
			}
			else
			{
				Send("/pdr pjob " + freelancerJobName);
				freelancerJobChangeRequested = true;
				pendingScanAt = DateTime.UtcNow + JobChangeDelay;
				status = "检测宝箱...";
			}
		}
	}

	private void AdvanceFreelancerScan()
	{
		pendingScanAt = DateTime.MinValue;
		if (!freelancerJobChangeRequested || !IsIsland() || !IsPlayerMovable() ||
			condition[(ConditionFlag)26] || IsMountedOrMounting())
		{
			RequestFreelancerScan("扫描准备重试");
			return;
		}
		freelancerJobChangeRequested = false;
		BeginTreasureScan();
	}

	private void BeginTreasureScan()
	{
		int generation = runGeneration;
		waitingForScan = true;
		silver = (copper = -1);
		nextAllowedScanAt = DateTime.UtcNow + SubsequentScanInterval;
		log.Debug("自由人切换等待结束，首次和后续扫描均使用同一原生魔寻宝调用", Array.Empty<object>());
		treasureCastAttempts = 1;
		TryCastTreasureSight("首次调用");
		status = "检测宝箱...";
		Task.Run(async () =>
		{
			await Task.Delay(800);
			if (!disposed)
			{
				framework.RunOnFrameworkThread((Action)(() =>
				{
					if (generation == runGeneration && running && waitingForScan)
					{
						treasureCastAttempts++;
						TryCastTreasureSight($"重试 #{treasureCastAttempts}");
					}
				}));
			}
		});
		Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(5L));
			if (!disposed)
			{
				framework.RunOnFrameworkThread((Action)(() =>
				{
					if (generation == runGeneration && running && waitingForScan)
					{
						waitingForScan = false;
						if (silver < 0 || copper < 0)
						{
							RetryUnansweredTreasureScan();
						}
					}
				}));
			}
		});
	}

	private void RetryUnansweredTreasureScan()
	{
		waitingForScan = false;
		freelancerJobChangeRequested = false;
		pendingBocchiAt = DateTime.MinValue;
		// Keep combat paused and leave time for Treasuresight's ten-second cooldown.
		pendingScanAt = DateTime.UtcNow.AddSeconds(5);
		status = "宝箱检测未响应，暂停战斗并等待重试...";
		log.Warning("[Global] 未收到完整宝箱数量，保持暂停战斗，稍后重新切自由人扫描", Array.Empty<object>());
	}

	private unsafe bool TryCastTreasureSight(string reason)
	{
		try
		{
			// [GLOBAL] 魔尋寶（Occult Treasuresight）在國際服是 ActionType.Action = 1、動作 ID = 41651
			//（幻境職業「自由人」的技能欄 II）。原版用 GeneralAction(5) / 32 在國際服無效。
			log.Debug($"魔寻宝调用开始：{reason}，动作类型={(ActionType)1}，动作ID={41651u}，目标参数={3758096384uL}", Array.Empty<object>());
			ActionManager* ptr = ActionManager.Instance();
			if (ptr == null)
			{
				log.Error("魔寻宝调用失败：ActionManager.Instance() 返回空指针", Array.Empty<object>());
				return false;
			}
			bool flag = ((*ptr)).UseAction((ActionType)1, 41651u, 3758096384uL, 0u, (ActionManager.UseActionMode)0, 0u, (bool*)null);
			log.Debug($"魔寻宝调用完成：UseAction 返回 {flag}", Array.Empty<object>());
			if (!flag)
			{
				log.Debug("魔寻宝动作暂未被接受，等待重试", Array.Empty<object>());
			}
			return flag;
		}
		catch (InvalidOperationException ex)
		{
			log.Error((Exception)ex, "魔寻宝调用失败（" + reason + "）：ActionManager 地址未解析，稍后重试", Array.Empty<object>());
			return false;
		}
		catch (Exception ex2)
		{
			log.Error(ex2, "魔寻宝调用失败（" + reason + "）：原生调用抛出异常", Array.Empty<object>());
			return false;
		}
	}

	private bool IsMountedOrMounting()
	{
		try
		{
			return IsMounted() || IsMounting();
		}
		catch (Exception ex)
		{
			log.Error(ex, "读取骑乘状态失败", Array.Empty<object>());
			return false;
		}
	}

	private bool IsMounted()
	{
		return condition[(ConditionFlag)4];
	}

	private bool IsMounting()
	{
		if (!condition[(ConditionFlag)64])
		{
			return condition[(ConditionFlag)71];
		}
		return true;
	}

	private unsafe bool TryUseRandomMount(string reason)
	{
		try
		{
			log.Debug($"随机坐骑原生调用开始（{reason}）：通用动作类型={(ActionType)5}，槽位={9u}", Array.Empty<object>());
			ActionManager* ptr = ActionManager.Instance();
			if (ptr == null)
			{
				log.Error("随机坐骑原生调用失败：ActionManager.Instance() 返回空指针", Array.Empty<object>());
				return false;
			}
			bool flag = ((*ptr)).UseAction((ActionType)5, 9u, 3758096384uL, 0u, (ActionManager.UseActionMode)0, 0u, (bool*)null);
			log.Debug($"随机坐骑原生调用完成（{reason}）：UseAction 返回 {flag}", Array.Empty<object>());
			if (!flag)
			{
				log.Debug("随机坐骑动作暂未被接受，等待重试", Array.Empty<object>());
			}
			return flag;
		}
		catch (InvalidOperationException ex)
		{
			log.Error((Exception)ex, "随机坐骑原生调用失败（" + reason + "）：ActionManager 地址未解析", Array.Empty<object>());
			return false;
		}
		catch (Exception ex2)
		{
			log.Error(ex2, "随机坐骑原生调用失败（" + reason + "）：原生调用抛出异常", Array.Empty<object>());
			return false;
		}
	}

	private unsafe bool TryDismount(string reason)
	{
		if (!IsMounted())
		{
			return true;
		}
		try
		{
			log.Debug("下坐骑原生调用开始（" + reason + "）", Array.Empty<object>());
			ActionManager* ptr = ActionManager.Instance();
			if (ptr == null)
			{
				log.Error("下坐骑原生调用失败：ActionManager.Instance() 返回空指针", Array.Empty<object>());
				return false;
			}
			bool flag = ((*ptr)).UseAction((ActionType)13, 0u, 3758096384uL, 0u, (ActionManager.UseActionMode)0, 0u, (bool*)null);
			log.Debug($"下坐骑原生调用完成（{reason}）：UseAction 返回 {flag}", Array.Empty<object>());
			return flag;
		}
		catch (Exception ex)
		{
			log.Error(ex, "下坐骑原生调用失败（" + reason + "）", Array.Empty<object>());
			return false;
		}
	}

	private void OnChatMessage(IHandleableChatMessage message)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Invalid comparison between Unknown and I4
		string textValue = ((IMutableChatMessage)message).Message.TextValue;
		bool cofferCountsUpdated = false;
		// [GLOBAL] 被動寶箱計數同步。
		// 即使插件不在自己的掃描狀態（例如玩家手動、或由 BOCCHI 施放魔尋寶），
		// 也從系統訊息更新 silver/copper，令介面顯示與實際一致。
		// 注意：刻意不呼叫 CompleteTreasureScan()，避免影響插件的掃描流程。
		if ((int)((IChatMessage)message).LogKind == 57 && IsIsland())
		{
			if (GameText.TryGetCofferCounts(textValue, out int detectedSilver, out int detectedBronze))
			{
				silver = detectedSilver;
				copper = detectedBronze;
				cofferCountsUpdated = true;
			}
		}
		if (!running)
		{
			return;
		}
		// [GLOBAL] 轉職確認 → 立即施放魔尋寶（唔再盲等 JobChangeDelay）。
		// 遊戲在幻境職業切換成功時會發系統訊息：
		//     "K. T. changes to Phantom Freelancer."
		// 失敗時則發：
		//     "You are unable to change phantom jobs at this time."
		// pendingScanAt != MinValue 代表已送出轉職指令、正在等結果。
		// （原本的 JobChangeDelay 計時器保留作後備：萬一訊息沒到，仍然會施放。）
		if (pendingScanAt != DateTime.MinValue && freelancerJobChangeRequested)
		{
			string localName = objects.LocalPlayer?.Name.TextValue;
			if (GameText.IsFreelancerChange(textValue, localName, freelancerJobName))
			{
				AdvanceFreelancerScan();
			}
			else if (GameText.IsJobChangeDenied(textValue))
			{
				freelancerJobChangeRequested = false;
				pendingScanAt = DateTime.UtcNow + TimeSpan.FromSeconds(5L);
			}
		}
		// [GLOBAL] 寶箱滿 → 直接開始尋寶流程。
		// 被動同步（BOCCHI 或玩家自己施放魔尋寶）原本只更新 silver/copper，不會觸發流程；
		// 而插件自己的掃描是由「亞返回」觸發的，在 BOCCHI illegal 模式下實測不會觸發，
		// 結果寶箱已滿（8 銀 / 30 銅）卻永遠不開始跑刀。
		if (cofferCountsUpdated && !waitingForScan && !waitingForEntry &&
			(silver >= MaxSilver || copper >= MaxCopper) && IsIsland() &&
			treasurePhase == TreasurePhase.None && !currencyBuyer.IsBusy && !currencyPurchaseMoveActive)
		{
			BeginTreasureProcedure();
		}
		CaptureTreasureChatLoot(message);
		// [GLOBAL] 進副本握手關鍵字（國際服實測）。
		// 進島（territory 1346/1252）後約 7 秒，系統會發出：
		//     "Your item level has been synced to 700."
		// 舊有的英文字串猜測 "You are now subject to item level restrictions" 永遠配對不到，
		// 導致 entrySyncMessageSeen 永遠為 false → TryCompleteEntryHandshake() 永遠不通過
		// → 進島後不會動作、出島後在幻影村（territory 1278）發呆。
		if (waitingForEntry && (int)((IChatMessage)message).LogKind == 57 && GameText.IsEntrySync(textValue))
		{
			entrySyncMessageSeen = true;
			status = "加载副本...";
			log.Debug("检测到" + activeProfile.ChapterName + "品级同步系统消息，等待目标区域加载完成", Array.Empty<object>());
		}
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		string text = ((localPlayer != null) ? ((IGameObject)localPlayer).Name.TextValue : null);
		// [GLOBAL] 中英兼容：亞返回完成（中文「发动了亚返回」；英文「uses Occult Return」，日誌先出 readies 再出 uses，取 uses）
		bool flag = GameText.IsOwnOccultReturn(textValue, text);
		if (flag && TryCompleteTreasureReturn("本角色亚返回消息及位置"))
		{
			return;
		}
		if (!initialScan & flag)
		{
			log.Debug("检测到本角色 " + text + " 的亚返回完成消息，将按间隔检测钱币和宝箱", Array.Empty<object>());
			if (pendingCurrencyCheckAt == DateTime.MinValue)
			{
				pendingCurrencyCheckAt = DateTime.UtcNow + ReturnScanDelay;
			}
			int num = copper;
			bool flag2 = (uint)(num - 28) <= 1u;
			bool flag3 = flag2;
			if (!waitingForScan && pendingReturnScanAt == DateTime.MinValue && (flag3 || DateTime.UtcNow >= nextAllowedScanAt))
			{
				pendingReturnScanAt = DateTime.UtcNow + ReturnScanDelay;
				if (flag3)
				{
					log.Debug($"当前铜宝箱为 {copper}/30，绕过 10 分钟间隔，将在本次亚返回后复检宝箱", Array.Empty<object>());
				}
			}
			else if (DateTime.UtcNow < nextAllowedScanAt)
			{
				TimeSpan timeSpan = nextAllowedScanAt - DateTime.UtcNow;
				log.Debug($"忽略本次亚返回：距离下次宝箱检测还需 {timeSpan.TotalMinutes:0.0} 分钟", Array.Empty<object>());
			}
		}
		if (!waitingForScan || (int)((IChatMessage)message).LogKind != 57)
		{
			return;
		}
		// Use the same parser for passive updates and our own active scan.
		if (GameText.TryGetCofferCounts(textValue, out int scanSilver, out int scanBronze))
		{
			silver = scanSilver;
			copper = scanBronze;
			waitingForScan = false;
			CompleteTreasureScan();
		}
	}

	private bool IsCapturingTreasureLoot() =>
		treasurePhase == TreasurePhase.XszRunning || treasurePhase == TreasurePhase.InnerReturn || treasurePhase == TreasurePhase.OuterReturn;

	private void CaptureTreasureChatLoot(IHandleableChatMessage message)
	{
		if (!IsCapturingTreasureLoot()) return;
		string bareItemName = null;
		// Japanese LogMessage 1053/1054 can contain only a linked item and quantity.
		// Verify both the loot chat type and the item identity before accepting it.
		if ((int)((IChatMessage)message).LogKind == 62)
		{
			ItemPayload item = ((IMutableChatMessage)message).Message.Payloads.OfType<ItemPayload>().FirstOrDefault();
			if (item != null)
			{
				try { bareItemName = data.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.Japanese).GetRow(item.ItemId).Name.ToString(); }
				catch { /* The normal text parser remains available if the sheet is unavailable. */ }
			}
		}
		RecordTreasureLoot(((IMutableChatMessage)message).Message.TextValue, bareItemName);
	}

	private void CaptureTreasureLoot(string text) => RecordTreasureLoot(text, null);

	private void RecordTreasureLoot(string text, string bareItemName)
	{
		if (!IsCapturingTreasureLoot() || !GameText.TryParseLoot(text, objects.LocalPlayer?.Name.TextValue, out string name, out int quantity, bareItemName)) return;
		treasureLoot[name] = treasureLoot.TryGetValue(name, out int current) ? current + quantity : quantity;
		log.Debug($"记录寻宝战利品：{name} ×{quantity}", Array.Empty<object>());
	}

	private void CompleteTreasureScan()
	{
		log.Information($"[Global] 宝箱扫描完成：银 {silver}，铜 {copper}，测试模拟满箱={debugForceFull}", Array.Empty<object>());
		// [DEBUG] debugForceFull：強制視為寶箱已滿，用來一鍵驗證完整循環
		//（寶箱滿 → 內環 → 外環 → 退本 → 重入 → 再掃描）。
		if (debugForceFull || silver >= 8 || copper >= 30)
		{
			BeginTreasureProcedure();
			return;
		}
		ChangeToCombatJob();
		// [GLOBAL] 原本用 initialScan 做閘門，但 initialScan 只在 Start() 與 TreasurePhase.Reentry
		// 設為 true。走「手動出島 → 自動重新入島」這條路徑時它仍是 false，
		// 於是只設狀態、pendingBocchiAt 永不排程 → /bocchiillegal on 永不送出 → 角色站著不動。
		// 改為看 BOCCHI 實際有冇開（bocchiEnabled）：未開就開，已開就只更新狀態。
		if (!bocchiEnabled)
		{
			initialScan = false;
			pendingBocchiAt = DateTime.UtcNow + JobChangeDelay;
			status = "准备战斗...";
		}
		else
		{
			status = "自动战斗中";
		}
	}

	private void BeginTreasureProcedure()
	{
		// Cancel delayed scan/combat work before handing control to the route.
		runGeneration++;
		freelancerJobChangeRequested = false;
		waitingForScan = false;
		pendingScanAt = pendingReturnScanAt = pendingBocchiAt = DateTime.MinValue;
		treasureMovementWait.Reset();
		treasurePlayerGuard.Reset();
		treasureLoot.Clear();
		xszLastPosition = default;
		xszLastPositionChangeAt = (nextXszPositionCheckAt = DateTime.MinValue);
		treasureError = "";
		treasurePhase = TreasurePhase.FirstMove;
		treasurePhaseAt = DateTime.UtcNow + TreasureCommandDelay;
		status = "准备寻宝...";
		log.Debug("宝箱达到上限，0.5 秒后关闭 BOCCHI 并移动至小水晶区域", Array.Empty<object>());
	}

	private void UpdateTreasureProcedure()
	{
		if (treasureMovementWait.IsActive)
		{
			if (!MovementWaitReady(treasureMovementWait))
			{
				return;
			}
			treasureMovementWait.Reset();
		}
		if (treasurePhaseAt != DateTime.MinValue && DateTime.UtcNow < treasurePhaseAt)
		{
			return;
		}
		treasurePhaseAt = DateTime.MinValue;
		switch (treasurePhase)
		{
		case TreasurePhase.FirstMove:
			Send("/bocchiillegal off");
			bocchiEnabled = false;
			treasurePhase = TreasurePhase.FirstMoveDelay;
			BeginMovementWait(treasureMovementWait);
			status = "准备寻宝...";
			break;
		case TreasurePhase.FirstMoveDelay:
			StartCrystalMove(TreasurePhase.FirstCrystal);
			break;
		case TreasurePhase.FirstCrystal:
			UpdateCrystalMove(firstLeg: true);
			break;
		case TreasurePhase.SecondMove:
			treasurePhase = TreasurePhase.SecondMoveDelay;
			BeginMovementWait(treasureMovementWait);
			status = "准备外环寻宝...";
			break;
		case TreasurePhase.SecondMoveDelay:
			StartCrystalMove(TreasurePhase.SecondCrystal);
			break;
		case TreasurePhase.SecondCrystal:
			UpdateCrystalMove(firstLeg: false);
			break;
		case TreasurePhase.FirstWaitPlayers:
		case TreasurePhase.SecondWaitPlayers:
			CheckCrystalPlayers();
			break;
		case TreasurePhase.XszRunning:
			UpdateXszTreasure();
			break;
		case TreasurePhase.LeaveDuty:
			Send("/pdr leaveduty");
			treasurePhase = TreasurePhase.Reentry;
			BeginMovementWait(treasureMovementWait, requireTerritoryExit: true);
			status = "退出副本...";
			break;
		case TreasurePhase.Reentry:
			treasurePhase = TreasurePhase.None;
			treasurePhaseAt = DateTime.MinValue;
			silver = (copper = -1);
			initialScan = true;
			if (!IsIsland())
			{
				BeginEntryWait("重新进入副本...");
				break;
			}
			RequestFreelancerScan("新循环");
			break;
		case TreasurePhase.InnerReturn:
		case TreasurePhase.OuterReturn:
			// Global Action chat is not required: the base position completes either leg.
			TryCompleteTreasureReturn("位置检测，基地营60y内");
			break;
		}
	}

	private bool TryCompleteTreasureReturn(string source)
	{
		if (treasurePhase != TreasurePhase.InnerReturn && treasurePhase != TreasurePhase.OuterReturn)
		{
			return false;
		}
		IPlayerCharacter player = objects.LocalPlayer;
		if (!IsIsland() || player == null || !IsPlayerMovable() ||
			Vector3.DistanceSquared(player.Position, activeProfile.CrystalMoveTarget) >= 60f * 60f)
		{
			return false;
		}
		if (treasurePhase == TreasurePhase.InnerReturn)
		{
			treasurePhase = TreasurePhase.SecondMove;
			treasurePhaseAt = DateTime.MinValue;
			BeginMovementWait(treasureMovementWait);
			status = "准备外环寻宝...";
			log.Information("[Global] 内环亚返回完成（" + source + "），准备外环", Array.Empty<object>());
		}
		else
		{
			SaveTreasureRecord();
			treasurePhase = debugNoLeaveDuty ? TreasurePhase.None : TreasurePhase.LeaveDuty;
			treasurePhaseAt = debugNoLeaveDuty ? DateTime.MinValue : DateTime.UtcNow + ReturnScanDelay;
			status = debugNoLeaveDuty ? "寻宝完成（测试模式：不退本）" : "寻宝完成，准备重进";
			log.Information("[Global] 外环亚返回完成（" + source + "）：" + status, Array.Empty<object>());
		}
		return true;
	}

	private void StartCrystalMove(TreasurePhase arrivalPhase)
	{
		Vector3 crystalMoveTarget = activeProfile.CrystalMoveTarget;
		Send($"/vnav moveto {crystalMoveTarget.X.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Y.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Z.ToString("0.###", CultureInfo.InvariantCulture)}");
		treasurePhase = arrivalPhase;
		crystalMoveDeadline = DateTime.UtcNow + CrystalMoveTimeout;
		nextCrystalMoveCheckAt = DateTime.UtcNow;
		status = "前往大水晶...";
		log.Debug($"已执行前往小水晶区域导航，目标坐标 {crystalMoveTarget}，开始严格坐标轮询", Array.Empty<object>());
	}

	private void UpdateCrystalMove(bool firstLeg)
	{
		if (DateTime.UtcNow < nextCrystalMoveCheckAt)
		{
			return;
		}
		nextCrystalMoveCheckAt = DateTime.UtcNow.AddSeconds(1.0);
		if (!IsAtCrystalMoveTarget())
		{
			if (!(DateTime.UtcNow < crystalMoveDeadline))
			{
				Stop("未到达小水晶区域，请检查导航功能");
			}
		}
		else
		{
			crystalMoveDeadline = (nextCrystalMoveCheckAt = DateTime.MinValue);
			BeginCrystalWait(firstLeg);
		}
	}

	private bool IsAtCrystalMoveTarget()
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		return Vector3.DistanceSquared(((IGameObject)localPlayer).Position, activeProfile.CrystalMoveTarget) <= 0.25f;
	}

	private void BeginXszTreasure()
	{
		Send("/xsz-occult-treasure start");
		DateTime utcNow = DateTime.UtcNow;
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		xszLastPosition = ((localPlayer != null) ? ((IGameObject)localPlayer).Position : default(Vector3));
		xszLastPositionChangeAt = utcNow;
		nextXszPositionCheckAt = utcNow;
		treasurePhase = TreasurePhase.XszRunning;
		treasurePhaseAt = DateTime.MinValue;
		status = "XSZ 寻宝中";
		log.Debug($"已启动 XSZ 跑刀，每 {XszPositionPollInterval.TotalSeconds:0} 秒检测坐标，连续 {XszNoMovementTimeout.TotalSeconds:0} 秒无变化视为完成", Array.Empty<object>());
	}

	private void UpdateXszTreasure()
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow < nextXszPositionCheckAt)
		{
			return;
		}
		nextXszPositionCheckAt = utcNow + XszPositionPollInterval;
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return;
		}
		Vector3 position = ((IGameObject)localPlayer).Position;
		if (Vector3.DistanceSquared(position, xszLastPosition) > 0.01f)
		{
			xszLastPosition = position;
			xszLastPositionChangeAt = utcNow;
		}
		else if (!(utcNow - xszLastPositionChangeAt < XszNoMovementTimeout))
		{
			log.Debug($"XSZ 跑刀连续 {XszNoMovementTimeout.TotalSeconds:0} 秒未检测到坐标变化，视为寻宝完成", Array.Empty<object>());
			SaveTreasureRecord();
			Send("/pdr leaveduty");
			treasurePhase = TreasurePhase.Reentry;
			treasurePhaseAt = DateTime.MinValue;
			BeginMovementWait(treasureMovementWait, requireTerritoryExit: true);
			xszLastPositionChangeAt = (nextXszPositionCheckAt = DateTime.MinValue);
			status = "寻宝完成，准备重进";
		}
	}

	private bool HasNearbyPlayer(float radius)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return true;
		}
		foreach (IGameObject item in (IEnumerable<IGameObject>)objects)
		{
			if ((int)item.ObjectKind == 1 && item.Address != ((IGameObject)localPlayer).Address && Vector3.Distance(item.Position, ((IGameObject)localPlayer).Position) <= radius)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsIsland()
	{
		return clientState.TerritoryType == activeProfile.TerritoryId;
	}

	private IslandProfile ResolveSelectedProfile()
	{
		return IslandProfile.Resolve(config.IslandTarget);
	}

	private void ApplySelectedProfile()
	{
		activeProfile = ResolveSelectedProfile();
	}

	private bool IsProfileSelectionLocked()
	{
		if (!running && !waitingForEntry)
		{
			return currencyBuyer.IsBusy;
		}
		return true;
	}

	private void SelectIslandTarget(IslandTarget target)
	{
		if (!IsProfileSelectionLocked() && config.IslandTarget != target)
		{
			config.IslandTarget = target;
			ApplySelectedProfile();
			silver = (copper = (silverCurrency = (goldCurrency = -1)));
			currencyPurchaseStatus = string.Empty;
			config.Save();
		}
	}

	private void ResetIslandCycle()
	{
		runGeneration++;
		freelancerJobChangeRequested = false;
		if (currencyBuyer.IsBusy)
		{
			currencyBuyer.Cancel();
		}
		waitingForScan = waitingForEntry = entrySyncMessageSeen = initialCurrencyCheckPending = false;
		bocchiEnabled = currencyPurchaseMoveActive = islandSwitchPending = false;
		pendingScanAt = pendingBocchiAt = pendingReturnScanAt = pendingCurrencyCheckAt = pendingPurchaseAt = nextAllowedScanAt = DateTime.MinValue;
		purchaseRetryDeadline = currencyPurchaseMoveDeadline = nextCurrencyPurchaseMoveCheckAt = currencyPurchaseMoveStartAt = DateTime.MinValue;
		islandSwitchMovementWait.Reset();
		treasureMovementWait.Reset();
		treasurePlayerGuard.Reset();
		treasurePhase = TreasurePhase.None;
		treasurePhaseAt = DateTime.MinValue;
		treasureLoot.Clear();
		currentCrystal = treasureError = "";
		innerLeg = false;
		crystalMoveDeadline = nextCrystalMoveCheckAt = mountRetryDeadline = nextMountRetryAt = DateTime.MinValue;
		xszLastPosition = default;
		xszLastPositionChangeAt = nextXszPositionCheckAt = DateTime.MinValue;
		silver = copper = silverCurrency = goldCurrency = -1;
		initialScan = true;
		islandEnteredAt = DateTime.MinValue;
	}

	private void PrepareUnexpectedIslandExit()
	{
		Send("/bocchiillegal off");
		Send("/pdr ptreasure abort");
		Send("/vnav stop");
		ResetIslandCycle();
		islandRecoveryPending = true;
		status = "已离岛，等待角色可动后重新进岛...";
		log.Information("[Global] 离岛重置：已取消旧路线、扫描及战斗计时", Array.Empty<object>());
	}

	private void OnTerritoryChanged(uint territory)
	{
		uint previous = observedTerritory;
		observedTerritory = territory;
		if (!running || disposed || territory == previous)
		{
			return;
		}
		if (previous == activeProfile.TerritoryId && territory != activeProfile.TerritoryId)
		{
			PrepareUnexpectedIslandExit();
		}
		else if (territory == activeProfile.TerritoryId && islandRecoveryPending)
		{
			// Also handle a rapid manual reentry before the next update outside.
			islandRecoveryPending = false;
			waitingForEntry = true;
			entrySyncMessageSeen = false;
			status = "加载副本...";
		}
	}

	private void BeginEntryWait(string nextStatus)
	{
		ResetIslandCycle();
		islandRecoveryPending = false;
		log.Debug("[重入重置] 已清空上一次的寻宝状态，进本后重新扫描", Array.Empty<object>());
		// Wait for the known Global item-level sync message as well as zone readiness.
		entrySyncMessageSeen = false;
		waitingForEntry = true;
		Send(activeProfile.EntryCommand);
		status = nextStatus;
	}

	private bool TryCompleteEntryHandshake()
	{
		if (!waitingForEntry || !entrySyncMessageSeen || !IsIsland() || condition[(ConditionFlag)45] || condition[(ConditionFlag)51] || objects.LocalPlayer == null)
		{
			return false;
		}
		waitingForEntry = false;
		entrySyncMessageSeen = false;
		// [GLOBAL] 記錄進島時間（供 autoLeaveAfterMinutes 計時）
		islandEnteredAt = DateTime.UtcNow;
		log.Debug("已确认进入" + activeProfile.ChapterName + "，开始首次流程", Array.Empty<object>());
		ScheduleInitialCurrencyCheck("首次进岛", JobChangeDelay);
		return true;
	}

	private bool IsNearPosition(Vector3 target, float radius)
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		float num = ((IGameObject)localPlayer).Position.X - target.X;
		float num2 = ((IGameObject)localPlayer).Position.Z - target.Z;
		return num * num + num2 * num2 <= radius * radius;
	}

	private bool NearBase()
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		Vector3 position = ((IGameObject)localPlayer).Position;
		float num = position.X - 39f;
		float num2 = position.Z - 39f;
		return num * num + num2 * num2 <= 324f;
	}

	private void Send(string command)
	{
		try
		{
			log.Debug("执行命令：" + command, Array.Empty<object>());
			commands.ProcessCommand(command);
		}
		catch (Exception ex)
		{
			log.Error(ex, "执行命令失败：" + command, Array.Empty<object>());
		}
	}

	private void ChangeToCombatJob()
	{
		Send("/pdr pjob " + combatJob);
		log.Debug("切换战斗辅助职业：" + combatJob, Array.Empty<object>());
	}

	private void UpdateCurrencyCounts()
	{
		silverCurrency = GetInventoryCount(activeProfile.SilverCurrencyItemId);
		goldCurrency = GetInventoryCount(activeProfile.GoldCurrencyItemId);
		log.Debug($"钱币检测：{activeProfile.SilverCurrencyName} {silverCurrency}/{9999}，{activeProfile.GoldCurrencyName} {goldCurrency}/{9999}", Array.Empty<object>());
	}


	private void ScheduleInitialCurrencyCheck(string source, TimeSpan delay)
	{
		initialCurrencyCheckPending = true;
		initialCurrencyCheckSource = source;
		pendingCurrencyCheckAt = DateTime.UtcNow + delay;
		status = "检查钱币...";
		log.Debug(source + "流程：先检测钱币并决定是否购买，未触发购买后再进行魔寻宝", Array.Empty<object>());
	}

	private void ContinueAfterInitialCurrencyCheck()
	{
		if (initialCurrencyCheckPending)
		{
			string source = initialCurrencyCheckSource;
			initialCurrencyCheckPending = false;
			RequestFreelancerScan(source);
		}
	}

	private unsafe int GetInventoryCount(uint itemId)
	{
		try
		{
			InventoryManager* ptr = InventoryManager.Instance();
			if (ptr == null)
			{
				return 0;
			}
			return Math.Max(0, ((*ptr)).GetInventoryItemCount(itemId, false, true, true, (short)0));
		}
		catch (Exception ex)
		{
			log.Error(ex, $"读取钱币数量失败：物品 ID {itemId}", Array.Empty<object>());
			return 0;
		}
	}

	private bool TryBeginCurrencyPurchase()
	{
		List<CurrencyPurchaseRequest> list = CreateCurrencyPurchaseRequests();
		if (list.Count == 0)
		{
			return false;
		}
		if (!currencyBuyer.Begin(list, activeProfile))
		{
			return false;
		}
		if (bocchiEnabled)
		{
			Send("/bocchiillegal off");
		}
		bocchiEnabled = false;
		Send("/vnav stop");
		initialCurrencyCheckPending = false;
		waitingForEntry = (entrySyncMessageSeen = (waitingForScan = false));
		pendingScanAt = (pendingBocchiAt = (pendingReturnScanAt = (pendingCurrencyCheckAt = (pendingPurchaseAt = DateTime.MinValue))));
		purchaseRetryDeadline = DateTime.MinValue;
		treasurePhase = TreasurePhase.None;
		treasurePhaseAt = DateTime.MinValue;
		treasureMovementWait.Reset();
		treasurePlayerGuard.Reset();
		status = currencyBuyer.Status;
		currencyPurchaseStatus = currencyBuyer.Status;
		log.Debug("自动购买已接管流程，其他插件行为已暂停", Array.Empty<object>());
		return true;
	}

	private bool HasCurrencyPurchaseRequest()
	{
		return CreateCurrencyPurchaseRequests().Count > 0;
	}

	private void BeginCurrencyPurchasePreparation()
	{
		if (bocchiEnabled)
		{
			Send("/bocchiillegal off");
		}
		bocchiEnabled = false;
		waitingForScan = false;
		pendingScanAt = (pendingBocchiAt = (pendingReturnScanAt = DateTime.MinValue));
		treasurePhase = TreasurePhase.None;
		treasurePhaseAt = DateTime.MinValue;
		treasureMovementWait.Reset();
		treasurePlayerGuard.Reset();
		status = "准备购买...";
		if (!IsAtCrystalMoveTarget())
		{
			Vector3 crystalMoveTarget = activeProfile.CrystalMoveTarget;
			Send($"/vnav moveto {crystalMoveTarget.X.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Y.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Z.ToString("0.###", CultureInfo.InvariantCulture)}");
			currencyPurchaseMoveActive = true;
			DateTime utcNow = DateTime.UtcNow;
			currencyPurchaseMoveStartAt = utcNow + CurrencyPurchaseMoveStartDelay;
			currencyPurchaseMoveDeadline = utcNow + CurrencyPurchaseMoveStartDelay + CrystalMoveTimeout;
			nextCurrencyPurchaseMoveCheckAt = DateTime.UtcNow;
			status = "前往购买地点...";
			log.Debug($"{activeProfile.ChapterName}自动购买：已停止 BOCCHI，{CurrencyPurchaseMoveStartDelay.TotalSeconds:0.#} 秒后开始前往大水晶，目标坐标 {crystalMoveTarget}", Array.Empty<object>());
		}
		else
		{
			ScheduleCurrencyPurchaseStart();
		}
	}

	private void UpdateCurrencyPurchaseMove()
	{
		if (!IsIsland())
		{
			HandleCurrencyPurchaseFailure("已离开" + activeProfile.ChapterName + "，跳过本轮购买");
		}
		else
		{
			if (DateTime.UtcNow < currencyPurchaseMoveStartAt)
			{
				return;
			}
			if (currencyPurchaseMoveStartAt != DateTime.MinValue)
			{
				Vector3 crystalMoveTarget = activeProfile.CrystalMoveTarget;
				Send($"/vnav moveto {crystalMoveTarget.X.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Y.ToString("0.###", CultureInfo.InvariantCulture)} {crystalMoveTarget.Z.ToString("0.###", CultureInfo.InvariantCulture)}");
				currencyPurchaseMoveStartAt = DateTime.MinValue;
				nextCurrencyPurchaseMoveCheckAt = DateTime.UtcNow;
				log.Debug($"{activeProfile.ChapterName}自动购买：已开始前往大水晶，目标坐标 {crystalMoveTarget}", Array.Empty<object>());
			}
			if (!(DateTime.UtcNow < nextCurrencyPurchaseMoveCheckAt))
			{
				nextCurrencyPurchaseMoveCheckAt = DateTime.UtcNow + CurrencyPurchaseMovePollInterval;
				if (IsAtCrystalMoveTarget())
				{
					Send("/vnav stop");
					currencyPurchaseMoveActive = false;
					currencyPurchaseMoveDeadline = (nextCurrencyPurchaseMoveCheckAt = (currencyPurchaseMoveStartAt = DateTime.MinValue));
					log.Debug(activeProfile.ChapterName + "自动购买：已到达大水晶，准备开始购买", Array.Empty<object>());
					ScheduleCurrencyPurchaseStart();
				}
				else if (!(DateTime.UtcNow < currencyPurchaseMoveDeadline))
				{
					HandleCurrencyPurchaseFailure("未能到达" + activeProfile.ChapterName + "大水晶，跳过本轮购买");
				}
			}
		}
	}

	private void ScheduleCurrencyPurchaseStart()
	{
		pendingPurchaseAt = DateTime.UtcNow + CurrencyPurchaseDelay;
		purchaseRetryDeadline = DateTime.UtcNow + CurrencyPurchaseRetryTimeout;
	}

	private List<CurrencyPurchaseRequest> CreateCurrencyPurchaseRequests()
	{
		try
		{
			PurchaseBag purchaseBag = CurrencyBuyer.ReadBag();
			if (purchaseBag == null)
			{
				return new List<CurrencyPurchaseRequest>();
			}
			return CurrencyPurchasePlan.Build(activeProfile, GetPurchaseSettings(activeProfile), (CurrencyKind kind) => purchaseCatalog.Get(activeProfile, kind), CurrencyBuyer.GetItemCount, purchaseBag);
		}
		catch (Exception ex)
		{
			currencyPurchaseStatus = "无法读取商店数据，自动购买暂不可用";
			log.Error(ex, currencyPurchaseStatus, Array.Empty<object>());
			return new List<CurrencyPurchaseRequest>();
		}
	}

	private void OnCurrencyPurchaseFinished(bool success, string message)
	{
		pendingPurchaseAt = (purchaseRetryDeadline = DateTime.MinValue);
		currencyPurchaseMoveActive = false;
		currencyPurchaseMoveDeadline = (nextCurrencyPurchaseMoveCheckAt = (currencyPurchaseMoveStartAt = DateTime.MinValue));
		currencyPurchaseStatus = (success ? message : ("自动购买失败：" + message));
		if (!running)
		{
			return;
		}
		if (!success)
		{
			purchaseFailureCooldownUntil = DateTime.UtcNow.AddSeconds(30.0);
			status = currencyPurchaseStatus;
			if (IsIsland())
			{
				RequestFreelancerScan("自动购买失败后继续流程");
			}
			else
			{
				BeginEntryWait("购买失败，恢复寻宝");
			}
			return;
		}
		silverCurrency = (goldCurrency = -1);
		silver = (copper = -1);
		initialScan = true;
		nextAllowedScanAt = DateTime.MinValue;
		if (!IsIsland())
		{
			BeginEntryWait("购买完成，重新进岛...");
		}
		else
		{
			RequestFreelancerScan("自动购买完成后");
		}
	}

	private void HandleCurrencyPurchaseFailure(string message)
	{
		pendingPurchaseAt = (purchaseRetryDeadline = DateTime.MinValue);
		currencyPurchaseMoveActive = false;
		currencyPurchaseMoveDeadline = (nextCurrencyPurchaseMoveCheckAt = (currencyPurchaseMoveStartAt = DateTime.MinValue));
		purchaseFailureCooldownUntil = DateTime.UtcNow.AddSeconds(30.0);
		currencyPurchaseStatus = "自动购买失败：" + message;
		status = currencyPurchaseStatus;
		if (IsIsland())
		{
			RequestFreelancerScan("自动购买失败后继续流程");
		}
		else
		{
			BeginEntryWait("购买失败，恢复寻宝");
		}
	}

	private static int GetPurchaseCost(CurrencyKind kind, CurrencyPurchaseMode mode)
	{
		switch (kind)
		{
		case CurrencyKind.Silver:
			switch (mode)
			{
			case CurrencyPurchaseMode.OldCoffer:
				return 40;
			case CurrencyPurchaseMode.UltimateFixative:
				return 1200;
			}
			break;
		case CurrencyKind.Gold:
			switch (mode)
			{
			case CurrencyPurchaseMode.OldCoffer:
				return 50;
			case CurrencyPurchaseMode.UltimateFixative:
				return 1920;
			}
			break;
		}
		return 1;
	}

	private PurchaseSettings GetPurchaseSettings(IslandProfile profile)
	{
		if (profile.Target != IslandTarget.SouthHorn)
		{
			return config.NorthPurchase;
		}
		return config.SouthPurchase;
	}

	private static int GetConfiguredQuantity(PurchaseSettings settings, CurrencyKind kind, CurrencyPurchaseMode mode)
	{
		switch (kind)
		{
		case CurrencyKind.Silver:
			switch (mode)
			{
			case CurrencyPurchaseMode.OldCoffer:
				return settings.SilverCofferQuantity;
			case CurrencyPurchaseMode.UltimateFixative:
				return settings.SilverFixativeQuantity;
			}
			break;
		case CurrencyKind.Gold:
			switch (mode)
			{
			case CurrencyPurchaseMode.OldCoffer:
				return settings.GoldCofferQuantity;
			case CurrencyPurchaseMode.UltimateFixative:
				return settings.GoldFixativeQuantity;
			}
			break;
		}
		return 0;
	}

	private void NormalizePurchaseConfig()
	{
		bool flag = false;
		PluginConfig pluginConfig = config;
		if (pluginConfig.NorthPurchase == null)
		{
			PurchaseSettings purchaseSettings = (pluginConfig.NorthPurchase = new PurchaseSettings());
		}
		pluginConfig = config;
		if (pluginConfig.SouthPurchase == null)
		{
			PurchaseSettings purchaseSettings = (pluginConfig.SouthPurchase = new PurchaseSettings());
		}
		if (config.Version < 3)
		{
			config.NorthPurchase = new PurchaseSettings
			{
				SilverMode = config.SilverPurchaseMode,
				GoldMode = config.GoldPurchaseMode,
				SilverTriggerAmount = config.SilverTriggerAmount,
				GoldTriggerAmount = config.GoldTriggerAmount,
				SilverCofferQuantity = config.SilverCofferQuantity,
				GoldCofferQuantity = config.GoldCofferQuantity,
				SilverFixativeQuantity = config.SilverFixativeQuantity,
				GoldFixativeQuantity = config.GoldFixativeQuantity
			};
			config.SouthPurchase = new PurchaseSettings();
			config.Version = 3;
			flag = true;
		}
		if (config.Version < 4)
		{
			// Saving the new schema drops removed notification settings and their URL.
			config.Version = 4;
			flag = true;
		}
		if (config.Version < 5)
		{
			// Saving schema 5 drops the removed tower options.
			config.Version = 5;
			flag = true;
		}
		IslandTarget islandTarget = config.IslandTarget;
		if (islandTarget != IslandTarget.NorthHorn && islandTarget != IslandTarget.SouthHorn)
		{
			config.IslandTarget = IslandTarget.NorthHorn;
			flag = true;
		}
		flag |= NormalizePurchaseSettings(config.NorthPurchase, supportsFixative: true);
		flag |= NormalizePurchaseSettings(config.SouthPurchase, supportsFixative: false);
		flag |= MigratePurchaseItems(config.NorthPurchase, IslandProfile.North);
		flag |= MigratePurchaseItems(config.SouthPurchase, IslandProfile.South);
		IslandProfile[] array = new IslandProfile[2]
		{
			IslandProfile.North,
			IslandProfile.South
		};
		foreach (IslandProfile profile in array)
		{
			PurchaseSettings purchaseSettings4 = GetPurchaseSettings(profile);
			flag |= purchaseSettings4.SilverTriggerAmount != Math.Clamp(purchaseSettings4.SilverTriggerAmount, 0, 9999) || purchaseSettings4.GoldTriggerAmount != Math.Clamp(purchaseSettings4.GoldTriggerAmount, 0, 9999);
			purchaseSettings4.SilverTriggerAmount = Math.Clamp(purchaseSettings4.SilverTriggerAmount, 0, 9999);
			purchaseSettings4.GoldTriggerAmount = Math.Clamp(purchaseSettings4.GoldTriggerAmount, 0, 9999);
			try
			{
				flag |= CurrencyPurchasePlan.NormalizeOptions(purchaseCatalog.Get(profile, CurrencyKind.Silver), purchaseSettings4.SilverItems, purchaseSettings4.SilverTriggerAmount);
				flag |= CurrencyPurchasePlan.NormalizeOptions(purchaseCatalog.Get(profile, CurrencyKind.Gold), purchaseSettings4.GoldItems, purchaseSettings4.GoldTriggerAmount);
			}
			catch (Exception ex)
			{
				log.Error(ex, "购买数量限制初始化失败，将在读取商店目录时重试", Array.Empty<object>());
			}
		}
		if (flag)
		{
			config.Save();
		}
	}

	internal static bool MigratePurchaseItems(PurchaseSettings settings, IslandProfile profile)
	{
		PurchaseSettings purchaseSettings = settings;
		if (purchaseSettings.SilverItems == null)
		{
			Dictionary<string, CurrencyItemOption> dictionary = (purchaseSettings.SilverItems = new Dictionary<string, CurrencyItemOption>());
		}
		purchaseSettings = settings;
		if (purchaseSettings.GoldItems == null)
		{
			Dictionary<string, CurrencyItemOption> dictionary = (purchaseSettings.GoldItems = new Dictionary<string, CurrencyItemOption>());
		}
		if (settings.ItemsVersion >= 1)
		{
			return false;
		}
		CurrencyKind[] values = Enum.GetValues<CurrencyKind>();
		foreach (CurrencyKind currencyKind in values)
		{
			bool flag = currencyKind == CurrencyKind.Silver;
			CurrencyPurchaseMode currencyPurchaseMode = (flag ? settings.SilverMode : settings.GoldMode);
			if (currencyPurchaseMode != CurrencyPurchaseMode.None && (currencyPurchaseMode != CurrencyPurchaseMode.UltimateFixative || profile.SupportsFixative))
			{
				uint value = (flag ? profile.SilverEventId : profile.GoldEventId);
				uint value2 = ((currencyPurchaseMode == CurrencyPurchaseMode.OldCoffer) ? 47740u : 51978u);
				(flag ? settings.SilverItems : settings.GoldItems).TryAdd($"{value}:{value2}:0", new CurrencyItemOption
				{
					Enabled = true,
					Quantity = GetConfiguredQuantity(settings, currencyKind, currencyPurchaseMode)
				});
			}
		}
		settings.ItemsVersion = 1;
		return true;
	}

	private static bool NormalizePurchaseSettings(PurchaseSettings settings, bool supportsFixative)
	{
		if (settings.ItemsVersion >= 1)
		{
			return false;
		}
		bool flag = false;
		CurrencyPurchaseMode currencyPurchaseMode = (Enum.IsDefined(typeof(CurrencyPurchaseMode), settings.SilverMode) ? settings.SilverMode : CurrencyPurchaseMode.None);
		CurrencyPurchaseMode currencyPurchaseMode2 = (Enum.IsDefined(typeof(CurrencyPurchaseMode), settings.GoldMode) ? settings.GoldMode : CurrencyPurchaseMode.None);
		if (!supportsFixative && currencyPurchaseMode == CurrencyPurchaseMode.UltimateFixative)
		{
			currencyPurchaseMode = CurrencyPurchaseMode.None;
		}
		if (!supportsFixative && currencyPurchaseMode2 == CurrencyPurchaseMode.UltimateFixative)
		{
			currencyPurchaseMode2 = CurrencyPurchaseMode.None;
		}
		flag |= SetPurchaseMode(settings, CurrencyKind.Silver, currencyPurchaseMode);
		flag |= SetPurchaseMode(settings, CurrencyKind.Gold, currencyPurchaseMode2);
		int num = Math.Clamp(settings.SilverTriggerAmount, 0, 9999);
		int num2 = Math.Clamp(settings.GoldTriggerAmount, 0, 9999);
		if (settings.SilverTriggerAmount != num)
		{
			settings.SilverTriggerAmount = num;
			flag = true;
		}
		if (settings.GoldTriggerAmount != num2)
		{
			settings.GoldTriggerAmount = num2;
			flag = true;
		}
		flag |= SetConfiguredQuantity(settings, CurrencyKind.Silver, CurrencyPurchaseMode.OldCoffer, Math.Clamp(settings.SilverCofferQuantity, 1, 249));
		flag |= SetConfiguredQuantity(settings, CurrencyKind.Gold, CurrencyPurchaseMode.OldCoffer, Math.Clamp(settings.GoldCofferQuantity, 1, 199));
		flag |= SetConfiguredQuantity(settings, CurrencyKind.Silver, CurrencyPurchaseMode.UltimateFixative, Math.Clamp(settings.SilverFixativeQuantity, 1, Math.Max(1, 8)));
		flag |= SetConfiguredQuantity(settings, CurrencyKind.Gold, CurrencyPurchaseMode.UltimateFixative, Math.Clamp(settings.GoldFixativeQuantity, 1, Math.Max(1, 5)));
		flag |= ClampSelectedPurchaseQuantity(settings, CurrencyKind.Silver, settings.SilverMode);
		return flag | ClampSelectedPurchaseQuantity(settings, CurrencyKind.Gold, settings.GoldMode);
	}

	private static bool ClampSelectedPurchaseQuantity(PurchaseSettings settings, CurrencyKind kind, CurrencyPurchaseMode mode)
	{
		if (mode == CurrencyPurchaseMode.None)
		{
			return false;
		}
		bool flag = false;
		int value = ((kind == CurrencyKind.Silver) ? settings.SilverTriggerAmount : settings.GoldTriggerAmount);
		int purchaseCost = GetPurchaseCost(kind, mode);
		value = Math.Clamp(value, purchaseCost, 9999);
		if (kind == CurrencyKind.Silver && settings.SilverTriggerAmount != value)
		{
			settings.SilverTriggerAmount = value;
			flag = true;
		}
		else if (kind == CurrencyKind.Gold && settings.GoldTriggerAmount != value)
		{
			settings.GoldTriggerAmount = value;
			flag = true;
		}
		int quantity = Math.Clamp(GetConfiguredQuantity(settings, kind, mode), 1, Math.Max(1, value / purchaseCost));
		return SetConfiguredQuantity(settings, kind, mode, quantity) | flag;
	}

	private static bool SetPurchaseMode(PurchaseSettings settings, CurrencyKind kind, CurrencyPurchaseMode mode)
	{
		if (kind == CurrencyKind.Silver)
		{
			if (settings.SilverMode == mode)
			{
				return false;
			}
			settings.SilverMode = mode;
			return true;
		}
		if (settings.GoldMode == mode)
		{
			return false;
		}
		settings.GoldMode = mode;
		return true;
	}

	private static bool SetConfiguredQuantity(PurchaseSettings settings, CurrencyKind kind, CurrencyPurchaseMode mode, int quantity)
	{
		if (GetConfiguredQuantity(settings, kind, mode) == quantity)
		{
			return false;
		}
		if (kind != CurrencyKind.Silver)
		{
			if (kind != CurrencyKind.Gold)
			{
				goto IL_004e;
			}
			if (mode != CurrencyPurchaseMode.OldCoffer)
			{
				if (mode != CurrencyPurchaseMode.UltimateFixative)
				{
					goto IL_004e;
				}
				settings.GoldFixativeQuantity = quantity;
			}
			else
			{
				settings.GoldCofferQuantity = quantity;
			}
		}
		else if (mode != CurrencyPurchaseMode.OldCoffer)
		{
			if (mode != CurrencyPurchaseMode.UltimateFixative)
			{
				goto IL_004e;
			}
			settings.SilverFixativeQuantity = quantity;
		}
		else
		{
			settings.SilverCofferQuantity = quantity;
		}
		return true;
		IL_004e:
		return false;
	}

	private void BeginMovementWait(MovableWait wait, bool requireTerritoryExit = false)
	{
		wait.Begin(DateTime.UtcNow, IsPlayerMovable(), clientState.TerritoryType, requireTerritoryExit);
	}

	private bool MovementWaitReady(MovableWait wait)
	{
		return wait.IsReady(DateTime.UtcNow, IsPlayerMovable(), clientState.TerritoryType);
	}

	private bool IsPlayerMovable()
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (!clientState.IsLoggedIn || localPlayer == null || ((IGameObject)localPlayer).IsDead || ((IBattleChara)localPlayer).IsCasting)
		{
			return false;
		}
		ICondition val = condition;
		ConditionFlag[] array = new ConditionFlag[20] { (ConditionFlag)45, (ConditionFlag)51, (ConditionFlag)25, (ConditionFlag)30, (ConditionFlag)33, (ConditionFlag)38, (ConditionFlag)39, (ConditionFlag)31, (ConditionFlag)32, (ConditionFlag)35, (ConditionFlag)50, (ConditionFlag)58, (ConditionFlag)78, (ConditionFlag)2, (ConditionFlag)27, (ConditionFlag)87, (ConditionFlag)64, (ConditionFlag)71, (ConditionFlag)70, (ConditionFlag)11 };
		if (val.Any(array))
		{
			return false;
		}
		if (!IsMovementScreenBlocked("NowLoading") && !IsMovementScreenBlocked("FadeMiddle"))
		{
			return !IsMovementScreenBlocked("FadeBack");
		}
		return false;
	}

	private unsafe bool IsMovementScreenBlocked(string name)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBase* address = (AtkUnitBase*)gameGui.GetAddonByName(name, 1).Address;
		if (address != null)
		{
			return ((*address)).IsVisible;
		}
		return false;
	}



	private void OnClientLogout(int type, int code)
	{
		StopForLogout();
	}

	private void StopForLogout()
	{
		if (disposed || logoutHandled || (!running && !currencyBuyer.IsBusy)) return;
		logoutHandled = true;
		EmergencyStop("角色已离线，插件已停止");
	}

	private void BeginCrystalWait(bool firstLeg)
	{
		innerLeg = firstLeg;
		currentCrystal = activeProfile.ShardKeywords[Random.Shared.Next(activeProfile.ShardKeywords.Length)];
		Send("/vnav stop");
		BeginShardTeleport(activeProfile.CrystalMoveTarget, config.TreasureModeSelection != TreasureMode.XszRun);
	}

	private void BeginShardTeleport(Vector3 origin, bool requiresMount)
	{
		treasurePhase = (innerLeg ? TreasurePhase.FirstWaitPlayers : TreasurePhase.SecondWaitPlayers);
		treasurePhaseAt = DateTime.MinValue;
		treasurePlayerGuard.Begin(origin, DateTime.UtcNow, requiresMount);
		Send("/pdr ptp " + currentCrystal);
		status = "传送至小水晶...";
	}

	private void CheckCrystalPlayers()
	{
		if (!IsIsland())
		{
			// [GLOBAL] 原本直接 Stop() 停掉整個插件。情境：副本時限到、被踢出、斷線重連
			// ——角色離開北島/南島，但插件正在跑刀，結果完全停止、唔會重新進本。
			// 改為重設尋寶階段並重新進本（必須重設 treasurePhase，否則本方法會每個 tick 重複觸發）。
			treasurePhase = TreasurePhase.None;
			treasurePhaseAt = DateTime.MinValue;
			BeginEntryWait("重新进入副本...");
			return;
		}
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		Vector3? position = ((localPlayer != null) ? new Vector3?(((IGameObject)localPlayer).Position) : ((Vector3?)null));
		bool nearbyPlayer = treasurePlayerGuard.ShouldCheckPlayers(position) && HasNearbyPlayer(50f);
		TreasurePlayerSnapshot player = new TreasurePlayerSnapshot(position, IsPlayerMovable(), nearbyPlayer, IsMounted(), IsMounting());
		switch (treasurePlayerGuard.Update(DateTime.UtcNow, player))
		{
		case TreasureGuardAction.WaitForTeleport:
			status = "传送至小水晶...";
			break;
		case TreasureGuardAction.WaitForPlayers:
			status = "附近有人，等待寻宝";
			break;
		case TreasureGuardAction.WaitForMovement:
			status = "等待人物可动...";
			break;
		case TreasureGuardAction.Mount:
			if (IsPlayerMovable() && !HasNearbyPlayer(50f) && !IsMountedOrMounting())
			{
				TryUseRandomMount(innerLeg ? "内环" : "外环");
			}
			status = "上坐骑...";
			break;
		case TreasureGuardAction.WaitForMount:
			status = "上坐骑...";
			break;
		case TreasureGuardAction.StartTreasure:
			if (IsPlayerMovable() && !IsMounting() && !HasNearbyPlayer(50f) && (!treasurePlayerGuard.RequiresMount || IsMounted()))
			{
				if (!treasurePlayerGuard.RequiresMount)
				{
					treasurePlayerGuard.Reset();
					BeginXszTreasure();
					break;
				}
				treasurePlayerGuard.Reset();
				Send(innerLeg ? "/pdr ptreasure 内环" : "/pdr ptreasure 外环");
				treasurePhase = (innerLeg ? TreasurePhase.InnerReturn : TreasurePhase.OuterReturn);
				status = (innerLeg ? "内环寻宝中" : "外环寻宝中");
			}
			break;
		case TreasureGuardAction.Dismount:
			TryDismount("周围玩家检测超过 15 秒，换传小水晶前");
			status = "下坐骑，准备换点...";
			break;
		case TreasureGuardAction.WaitForDismount:
			status = "下坐骑，准备换点...";
			break;
		case TreasureGuardAction.SwitchCrystal:
		{
			if (IsMountedOrMounting() || !IsPlayerMovable())
			{
				break;
			}
			IPlayerCharacter localPlayer2 = objects.LocalPlayer;
			Vector3? vector = ((localPlayer2 != null) ? new Vector3?(((IGameObject)localPlayer2).Position) : ((Vector3?)null));
			if (vector.HasValue)
			{
				string[] array = activeProfile.ShardKeywords.Where((string x) => x != currentCrystal).ToArray();
				if (array.Length == 0)
				{
					Stop("无可用小水晶，已停止");
					break;
				}
				currentCrystal = array[Random.Shared.Next(array.Length)];
				BeginShardTeleport(vector.Value, treasurePlayerGuard.RequiresMount);
			}
			break;
		}
		case TreasureGuardAction.TeleportTimedOut:
			Stop("小水晶传送失败，请检查传送功能");
			break;
		case TreasureGuardAction.MountTimedOut:
			Stop("未能召唤随机坐骑，请检查坐骑可用性");
			break;
		case TreasureGuardAction.DismountTimedOut:
			Stop("下坐骑失败，已停止");
			break;
		}
	}

	private void DrawAutomaticPurchaseConfig()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		ImGui.SetNextItemOpen(config.AutoPurchaseExpanded, (ImGuiCond)2);
		bool flag = ImGui.CollapsingHeader(UiText.Label("自动购买配置"), (ImGuiTreeNodeFlags)0);
		if (flag != config.AutoPurchaseExpanded)
		{
			config.AutoPurchaseExpanded = flag;
			config.Save();
		}
		if (!flag)
		{
			return;
		}
		ImGui.SetNextItemWidth(MathF.Min(280f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
		string purchaseSearch = config.PurchaseSearch ?? string.Empty;
		if (ImGui.InputTextWithHint(UiText.Label("##PurchaseSearch"), UiText.Render("搜索商品"), ref purchaseSearch, 128, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
		{
			config.PurchaseSearch = purchaseSearch;
			config.Save();
		}
		bool purchaseEnabledOnly = config.PurchaseEnabledOnly;
		if (ImGui.Checkbox(UiText.Label("仅显示已启用商品"), ref purchaseEnabledOnly))
		{
			config.PurchaseEnabledOnly = purchaseEnabledOnly;
			config.Save();
		}
		if (ImGui.BeginTabBar("PurchaseCurrencies", (ImGuiTabBarFlags)0))
		{
			CurrencyKind[] values = Enum.GetValues<CurrencyKind>();
			foreach (CurrencyKind currencyKind in values)
			{
				if (ImGui.BeginTabItem(UiText.Label((currencyKind == CurrencyKind.Silver) ? activeProfile.SilverCurrencyName : activeProfile.GoldCurrencyName), (ImGuiTabItemFlags)0))
				{
					DrawCurrencyPurchaseConfig(currencyKind);
					ImGui.EndTabItem();
				}
			}
			ImGui.EndTabBar();
		}
		if (!string.IsNullOrWhiteSpace(currencyPurchaseStatus))
		{
			ImU8String val = new ImU8String(5, 1);
			val.AppendLiteral(UiText.Render("购买状态："));
			val.AppendFormatted<string>(UiText.Render(currencyPurchaseStatus));
			ImGui.TextWrapped(val);
		}
	}


	private void DrawCurrencyPurchaseConfig(CurrencyKind kind)
	{
		//IL_060f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0614: Unknown result type (might be due to invalid IL or missing references)
		//IL_0630: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0601: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0422: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_048c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_0402: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0527: Unknown result type (might be due to invalid IL or missing references)
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_055a: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
		PurchaseSettings purchaseSettings = GetPurchaseSettings(activeProfile);
		bool flag = kind == CurrencyKind.Silver;
		string text = (flag ? activeProfile.SilverCurrencyName : activeProfile.GoldCurrencyName);
		string text2 = $"{activeProfile.Target}{kind}";
		int num = (flag ? silverCurrency : goldCurrency);
		int num2 = (flag ? purchaseSettings.SilverTriggerAmount : purchaseSettings.GoldTriggerAmount);
		Dictionary<string, CurrencyItemOption> options = (flag ? purchaseSettings.SilverItems : purchaseSettings.GoldItems);
		ImU8String val = new ImU8String(2, 3);
		val.AppendFormatted<string>(UiText.Render(text));
		val.AppendLiteral(UiText.Render("："));
		val.AppendFormatted<string>(UiText.Render((num >= 0) ? num.ToString() : "未检测"));
		val.AppendLiteral(UiText.Render("/"));
		val.AppendFormatted<int>(9999);
		ImGui.TextWrapped(val);
		ImGui.BeginDisabled(currencyBuyer.IsBusy || currencyPurchaseMoveActive);
		ImGui.SetNextItemWidth(120f);
		ImU8String val2 = UiText.Label($"触发钱币数量##{text2}Trigger");
		ImU8String val3 = val2;
		ImU8String val4 = default;
		if (ImGui.InputInt(val3, ref num2, 0, 0, val4, (ImGuiInputTextFlags)0))
		{
			num2 = Math.Clamp(num2, 0, 9999);
			if (flag)
			{
				purchaseSettings.SilverTriggerAmount = num2;
			}
			else
			{
				purchaseSettings.GoldTriggerAmount = num2;
			}
			config.Save();
		}
		try
		{
			IReadOnlyList<CurrencyShopItem> readOnlyList = purchaseCatalog.Get(activeProfile, kind);
			if (CurrencyPurchasePlan.NormalizeOptions(readOnlyList, options, num2))
			{
				config.Save();
			}
			float globalScale = ImGuiHelpers.GlobalScale;
			ImGuiTableFlags val5 = (ImGuiTableFlags)50340032;
			val4 = new ImU8String(13, 1);
			val4.AppendLiteral(UiText.Render("PurchaseItems"));
			val4.AppendFormatted<string>(UiText.Render(text2));
			ImU8String val7;
			if (ImGui.BeginTable(val4, 5, val5, new Vector2(0f, 300f * globalScale), 820f * globalScale))
			{
				ImGui.TableSetupScrollFreeze(0, 1);
				ImGui.TableSetupColumn(UiText.Label("启用"), (ImGuiTableColumnFlags)8, 42f * globalScale, 0u);
				ImGui.TableSetupColumn(UiText.Label("商品"), (ImGuiTableColumnFlags)8, 275f * globalScale, 0u);
				ImGui.TableSetupColumn(UiText.Label("单价"), (ImGuiTableColumnFlags)8, 65f * globalScale, 0u);
				ImGui.TableSetupColumn(UiText.Label("数量"), (ImGuiTableColumnFlags)8, 130f * globalScale, 0u);
				ImGui.TableSetupColumn(UiText.Label("优先级"), (ImGuiTableColumnFlags)8, 105f * globalScale, 0u);
				ImGui.TableHeadersRow();
				foreach (CurrencyShopItem item in readOnlyList)
				{
					options.TryGetValue(item.Key, out CurrencyItemOption value);
					if (value == null)
					{
						value = new CurrencyItemOption();
					}
					if ((!config.PurchaseEnabledOnly || value.Enabled) && (string.IsNullOrWhiteSpace(config.PurchaseSearch) || item.Name.Contains(config.PurchaseSearch, StringComparison.OrdinalIgnoreCase)))
					{
						ImGui.PushID(item.Key);
						ImGui.TableNextRow();
						ImGui.TableNextColumn();
						bool enabled = value.Enabled;
						bool flag2 = ImGui.Checkbox(UiText.Label("##Enabled"), ref enabled);
						value.Enabled = enabled;
						ImGui.TableNextColumn();
						ImGui.TextWrapped(item.Name + (item.HighQuality ? " HQ" : string.Empty));
						if (ImGui.IsItemHovered())
						{
							ImGui.SetTooltip(UiText.Render($"持有：{CurrencyBuyer.GetItemCount(item.ItemId)}\n每次兑换：{item.ReceiveCount} 个" + (item.Unique ? "\n唯一物品" : string.Empty)));
						}
						ImGui.TableNextColumn();
						ImGui.Text(UiText.Render(item.Cost.ToString()));
						ImGui.TableNextColumn();
						int num3 = CurrencyPurchasePlan.ConfiguredMaximum(item, num2, PurchaseQuantityMode.PerBatch);
						int num4 = CurrencyPurchasePlan.ClampConfiguredQuantity(item, num2, PurchaseQuantityMode.PerBatch, value.Quantity);
						if (num4 != value.Quantity)
						{
							value.Quantity = num4;
							flag2 = true;
						}
						ImGui.SetNextItemWidth(-1f);
						ImGui.BeginDisabled(num3 == 0);
						ImU8String val6 = "##Quantity";
						val7 = default;
						if (ImGui.InputInt(val6, ref num4, 0, 0, val7, (ImGuiInputTextFlags)0))
						{
							value.Quantity = CurrencyPurchasePlan.ClampConfiguredQuantity(item, num2, PurchaseQuantityMode.PerBatch, num4);
							flag2 = true;
						}
						ImGui.EndDisabled();
						if (ImGui.IsItemHovered((ImGuiHoveredFlags)128))
						{
							ImGui.SetTooltip(UiText.Render((num3 == 0) ? "触发钱币数量不足以兑换此商品" : $"当前触发值下上限：{num3}"));
						}
						ImGui.TableNextColumn();
						int priority = value.Priority;
						ImGui.SetNextItemWidth(-1f);
						ImU8String val8 = "##Priority";
						val7 = default;
						if (ImGui.InputInt(val8, ref priority, 0, 0, val7, (ImGuiInputTextFlags)0))
						{
							value.Priority = Math.Clamp(priority, 0, 9999);
							flag2 = true;
						}
						if (ImGui.IsItemHovered())
						{
							ImGui.SetTooltip(UiText.Render("数值越大，越先购买"));
						}
						if (flag2)
						{
							options[item.Key] = value;
							config.Save();
						}
						ImGui.PopID();
					}
				}
				ImGui.EndTable();
			}
			val7 = new ImU8String(9, 2);
			val7.AppendLiteral(UiText.Render("已启用 "));
			val7.AppendFormatted<int>(readOnlyList.Count((CurrencyShopItem x) => options.TryGetValue(x.Key, out CurrencyItemOption value2) && (value2?.Enabled ?? false)));
			val7.AppendLiteral(UiText.Render(" / "));
			val7.AppendFormatted<int>(readOnlyList.Count);
			val7.AppendLiteral(UiText.Render(" 项"));
			ImGui.TextDisabled(val7);
		}
		catch (Exception ex)
		{
			ImU8String val9 = new ImU8String(9, 1);
			val9.AppendLiteral(UiText.Render("无法读取商店目录："));
			val9.AppendFormatted<string>(UiText.Render(ex.Message));
			ImGui.TextWrapped(val9);
		}
		finally
		{
			ImGui.EndDisabled();
		}
	}

	private void DrawIslandTargetConfig()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		ImGui.Text(UiText.Render("目标副本"));
		ImGui.BeginDisabled(IsProfileSelectionLocked());
		if (ImGui.RadioButton(UiText.Label("北征之章（北岛）##IslandNorth"), config.IslandTarget == IslandTarget.NorthHorn))
		{
			SelectIslandTarget(IslandTarget.NorthHorn);
		}
		ImGui.SameLine();
		if (ImGui.RadioButton(UiText.Label("南征之章（南岛）##IslandSouth"), config.IslandTarget == IslandTarget.SouthHorn))
		{
			SelectIslandTarget(IslandTarget.SouthHorn);
		}
		ImGui.EndDisabled();
	}

	private void DrawTitleBarControls()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
		Vector2 windowPos = ImGui.GetWindowPos();
		Vector2 windowSize = ImGui.GetWindowSize();
		ImGuiStylePtr style = ImGui.GetStyle();
		float num = MathF.Max(2f, style.ItemSpacing.X - 3f);
		float num2 = 2f * (ImGui.GetFontSize() + style.ItemInnerSpacing.X) + num;
		bool flag = !config.SimplifiedUi;
		float num3 = (flag ? 4f : 3f);
		float num4 = 24f * num3 + num * (num3 - 1f);
		float x = MathF.Max(y: windowPos.X + windowSize.X - num4 - num2, x: windowPos.X + 8f);
		float y = MathF.Max(18f, ImGui.GetFrameHeight() - 2f);
		float y2 = windowPos.Y + 1f;
		ImGui.PushClipRect(windowPos, windowPos + windowSize, false);
		ImGui.SetCursorScreenPos(new Vector2(x, y2));
		ImGui.PushStyleColor((ImGuiCol)21, Vector4.Zero);
		ImGui.PushStyleColor((ImGuiCol)22, new Vector4(1f, 1f, 1f, 0.12f));
		ImGui.PushStyleColor((ImGuiCol)23, new Vector4(1f, 1f, 1f, 0.2f));
		Vector2 size = new Vector2(24f, y);
		if (flag)
		{
			if (DrawTitleBarIconButton("TitleSponsor", TitleBarIcon.Sponsor, size, "支持 OCBFR"))
			{
				OpenSponsorPage();
			}
			ImGui.SameLine(0f, num);
		}
		ImGui.BeginDisabled(running || currencyBuyer.IsBusy);
		if (DrawTitleBarIconButton("TitleStart", TitleBarIcon.Play, size, "启动插件"))
		{
			Start();
		}
		ImGui.EndDisabled();
		ImGui.SameLine(0f, num);
		if (DrawTitleBarIconButton("TitleEmergencyStop", TitleBarIcon.Stop, size, "紧急停止"))
		{
			StopFromUser(emergency: true);
		}
		ImGui.SameLine(0f, num);
		TitleBarIcon icon = (config.SimplifiedUi ? TitleBarIcon.FullView : TitleBarIcon.CompactView);
		string tooltip = (config.SimplifiedUi ? "切换至完整界面" : "切换至简化界面");
		if (DrawTitleBarIconButton("TitleUiMode", icon, size, tooltip))
		{
			ToggleMainUi();
		}
		ImGui.PopStyleColor(3);
		ImGui.PopClipRect();
		ImGui.SetCursorScreenPos(cursorScreenPos);
	}

	private static bool DrawTitleBarIconButton(string id, TitleBarIcon icon, Vector2 size, string tooltip)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		ImU8String val = new ImU8String(2, 1);
		val.AppendLiteral(UiText.Render("##"));
		val.AppendFormatted<string>(UiText.Render(id));
		bool result = ImGui.Button(val, size);
		Vector2 itemRectMin = ImGui.GetItemRectMin();
		Vector2 itemRectMax = ImGui.GetItemRectMax();
		Vector2 vector = (itemRectMin + itemRectMax) * 0.5f;
		uint colorU = ImGui.GetColorU32((ImGuiCol)0);
		ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
		switch (icon)
		{
		case TitleBarIcon.Play:
			windowDrawList.AddTriangleFilled(vector + new Vector2(-3.5f, -5f), vector + new Vector2(-3.5f, 5f), vector + new Vector2(5f, 0f), colorU);
			break;
		case TitleBarIcon.Stop:
			windowDrawList.AddRectFilled(vector + new Vector2(-4.5f, -4.5f), vector + new Vector2(4.5f, 4.5f), colorU, 1f);
			break;
		case TitleBarIcon.FullView:
			DrawRectangleOutline(windowDrawList, vector + new Vector2(-5.5f, -4f), vector + new Vector2(5.5f, 4f), colorU);
			break;
		case TitleBarIcon.CompactView:
			DrawRectangleOutline(windowDrawList, vector + new Vector2(-5.5f, -2.5f), vector + new Vector2(3f, 3.5f), colorU);
			DrawRectangleOutline(windowDrawList, vector + new Vector2(-2.5f, -5f), vector + new Vector2(5.5f, 1f), colorU);
			break;
		case TitleBarIcon.Sponsor:
		{
			string text = "♥";
			Vector2 vector2 = ImGui.CalcTextSize(UiText.Render(text), false, -1f);
			windowDrawList.AddText(vector - vector2 * 0.5f, colorU, text);
			break;
		}
		}
		DrawTitleBarTooltip(tooltip);
		return result;
	}

	private static void DrawRectangleOutline(ImDrawListPtr drawList, Vector2 min, Vector2 max, uint color)
	{
		drawList.AddLine(min, new Vector2(max.X, min.Y), color, 1.4f);
		drawList.AddLine(new Vector2(max.X, min.Y), max, color, 1.4f);
		drawList.AddLine(max, new Vector2(min.X, max.Y), color, 1.4f);
		drawList.AddLine(new Vector2(min.X, max.Y), min, color, 1.4f);
	}


	private static void DrawTitleBarTooltip(string text)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (ImGui.IsItemHovered((ImGuiHoveredFlags)128))
		{
			ImGui.BeginTooltip();
			ImGui.TextUnformatted(UiText.Render(text));
			ImGui.EndTooltip();
		}
	}

	private void DrawSponsorButton(string id)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Vector2 vector = new Vector2(26f * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());
		ImU8String val = new ImU8String(3, 1);
		val.AppendLiteral(UiText.Render("♥##"));
		val.AppendFormatted<string>(UiText.Render(id));
		if (ImGui.Button(val, vector))
		{
			OpenSponsorPage();
		}
		DrawTitleBarTooltip("支持 OCBFR");
	}

	private void DrawSimplifiedHeader()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		Vector4 vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
		ImGui.TextColored(ref vector, UiText.Render("// OCBFR"));
		ImGui.SameLine();
		ImU8String val = new ImU8String(13, 4);
		val.AppendLiteral(UiText.Render("银箱 "));
		val.AppendFormatted<int>(Math.Max(0, silver), "00");
		val.AppendLiteral(UiText.Render("/"));
		val.AppendFormatted<int>(8, "00");
		val.AppendLiteral(UiText.Render("  ·  铜箱 "));
		val.AppendFormatted<int>(Math.Max(0, copper), "00");
		val.AppendLiteral(UiText.Render("/"));
		val.AppendFormatted<int>(30, "00");
		ImGui.Text(val);
		ImGui.Spacing();
		vector = (running ? new Vector4(0.28f, 0.9f, 0.72f, 1f) : new Vector4(0.65f, 0.67f, 0.68f, 1f));
		ImGui.TextColored(ref vector, UiText.Render(running ? "● 运行中" : "○ 已停止"));
		ImGui.SameLine();
		ImU8String val2 = new ImU8String(7, 1);
		val2.AppendLiteral(UiText.Render("当前选择模式："));
		val2.AppendFormatted<string>(UiText.Render((activeProfile.Target == IslandTarget.SouthHorn) ? "南征之章" : "北征之章"));
		ImGui.Text(val2);
		ImGui.SameLine();
		DrawSponsorButton("SimplifiedSponsor");
	}

	private static void DrawPureBlurBackground(float strength, bool border)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		ImGuiViewportPtr val = ImGui.GetWindowViewport();
		uint iD = val.ID;
		val = ImGui.GetMainViewport();
		if (iD == val.ID)
		{
			ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
			Vector2 windowPos = ImGui.GetWindowPos();
			Vector2 vector = windowPos + ImGui.GetWindowSize();
			float num = 4f * ImGuiHelpers.GlobalScale;
			ImGuiHelpers.PrependBlurBehind(windowDrawList, windowPos, vector, strength, num, new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0f, 0f, 0f), 0f);
			if (border)
			{
				windowDrawList.AddRect(windowPos, vector, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.24f)), num, (ImDrawFlags)240, 1.1f * ImGuiHelpers.GlobalScale);
			}
		}
	}

	private void DrawBHeader()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		float x = ImGui.GetContentRegionAvail().X;
		if (ImGui.BeginTable("BHeaderRows", 1, (ImGuiTableFlags)12607488, new Vector2(x, 0f), 0f))
		{
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			Vector4 vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
			ImGui.TextColored(ref vector, UiText.Render("// OCBFR"));
			ImGui.SameLine();
			vector = (running ? new Vector4(0.28f, 0.9f, 0.72f, 1f) : new Vector4(0.65f, 0.67f, 0.68f, 1f));
			ImGui.TextColored(ref vector, UiText.Render(running ? "● 运行中" : "○ 已停止"));
			if (ImGui.GetContentRegionAvail().X > 750f * ImGuiHelpers.GlobalScale)
			{
				ImGui.SameLine();
			}
			ImU8String val = new ImU8String(7, 1);
			val.AppendLiteral(UiText.Render("当前选择模式 "));
			val.AppendFormatted<string>(UiText.Render(activeProfile.ChapterName));
			ImGui.TextWrapped(val);
			ImGui.Dummy(new Vector2(0f, 2f * ImGuiHelpers.GlobalScale));
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
			ImGui.TextColored(ref vector, UiText.Render("OCBFR"));
			ImGui.SameLine();
			vector = new Vector4(0.28f, 0.9f, 0.72f, 1f);
			ImU8String val2 = new ImU8String(6, 1);
			val2.AppendLiteral(UiText.Render("  /  v"));
			val2.AppendFormatted<string>(UiText.Render(PluginVersion));
			ImGui.TextColored(ref vector, val2);
			ImGui.Dummy(new Vector2(0f, 2f * ImGuiHelpers.GlobalScale));
			ImGui.EndTable();
		}
		ImGui.Separator();
	}

	private void DrawBStatusSummary()
	{
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		if (ImGui.GetContentRegionAvail().X < 760f * ImGuiHelpers.GlobalScale)
		{
			ImU8String val = new ImU8String(5, 1);
			val.AppendLiteral(UiText.Render("当前任务："));
			val.AppendFormatted<string>(UiText.Render(status));
			ImGui.TextWrapped(val);
			ImU8String val2 = new ImU8String(10, 4);
			val2.AppendLiteral(UiText.Render("银箱 "));
			val2.AppendFormatted<int>(Math.Max(0, silver));
			val2.AppendLiteral(UiText.Render("/"));
			val2.AppendFormatted<int>(8);
			val2.AppendLiteral(UiText.Render("  铜箱 "));
			val2.AppendFormatted<int>(Math.Max(0, copper));
			val2.AppendLiteral(UiText.Render("/"));
			val2.AppendFormatted<int>(30);
			ImGui.TextWrapped(val2);
			DrawIslandTargetConfig();
			return;
		}
		ImGui.Indent(8f);
		if (!ImGui.BeginTable("BStatusSummary", 4, (ImGuiTableFlags)8704, default(Vector2), 0f))
		{
			ImGui.Unindent(8f);
			return;
		}
		ImGui.TableSetupColumn(UiText.Label("BStatusMain"), (ImGuiTableColumnFlags)8, 280f, 0u);
		ImGui.TableSetupColumn(UiText.Label("BMode"), (ImGuiTableColumnFlags)8, 220f, 0u);
		ImGui.TableSetupColumn(UiText.Label("BSilver"), (ImGuiTableColumnFlags)8, 105f, 0u);
		ImGui.TableSetupColumn(UiText.Label("BCopper"), (ImGuiTableColumnFlags)8, 105f, 0u);
		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.TextDisabled(UiText.Render("当前任务"));
		Vector4 vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
		ImGui.TextColored(ref vector, UiText.Render(status));
		ImGui.TableNextColumn();
		ImGui.Text(UiText.Render("寻宝副本选择"));
		ImGui.BeginDisabled(IsProfileSelectionLocked());
		ImGui.PushStyleColor((ImGuiCol)9, new Vector4(0.25f, 0.25f, 0.25f, 1f));
		ImGui.PushStyleColor((ImGuiCol)8, new Vector4(0.16f, 0.16f, 0.16f, 1f));
		if (ImGui.RadioButton(UiText.Label("南征之章（南岛）##SummarySouth"), config.IslandTarget == IslandTarget.SouthHorn))
		{
			SelectIslandTarget(IslandTarget.SouthHorn);
		}
		if (ImGui.RadioButton(UiText.Label("北征之章（北岛）##SummaryNorth"), config.IslandTarget == IslandTarget.NorthHorn))
		{
			SelectIslandTarget(IslandTarget.NorthHorn);
		}
		ImGui.PopStyleColor(2);
		ImGui.EndDisabled();
		ImGui.TableNextColumn();
		ImGui.TextDisabled(UiText.Render("银箱"));
		ImU8String val3 = new ImU8String(3, 2);
		val3.AppendFormatted<int>((silver >= 0) ? silver : 0, "00");
		val3.AppendLiteral(UiText.Render(" / "));
		val3.AppendFormatted<int>(8, "00");
		ImGui.Text(val3);
		ImGui.TextDisabled(UiText.Render("容量"));
		ImGui.TableNextColumn();
		ImGui.TextDisabled(UiText.Render("铜箱"));
		ImU8String val4 = new ImU8String(3, 2);
		val4.AppendFormatted<int>((copper >= 0) ? copper : 0, "00");
		val4.AppendLiteral(UiText.Render(" / "));
		val4.AppendFormatted<int>(30, "00");
		ImGui.Text(val4);
		ImGui.TextDisabled(UiText.Render("容量"));
		ImGui.EndTable();
		ImGui.Unindent(8f);
	}

	private void DrawBProgressRail()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		Vector4 vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
		ImGui.TextColored(ref vector, UiText.Render("自动流程"));
		ImGui.TextDisabled(UiText.Render("自动流程路线"));
		ImGui.Spacing();
		int num = ResolveBProgressStep();
		DrawBProgressStep("01 / 进入", "区域同步完成", num >= 1, num == 1);
		DrawBProgressStep("02 / 检测", "钱币与宝箱", num >= 2, num == 2);
		DrawBProgressStep("03 / 战斗", "BOCCHI 运行中", num >= 3, num == 3);
		DrawBProgressStep("04 / 寻宝", "等待宝箱上限", num >= 4, num == 4);
		DrawBProgressStep("05 / 重进", "自动循环", num >= 5, num == 5);
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.TextDisabled(UiText.Render("当前区域 ID"));
		ImU8String val = new ImU8String(0, 1);
		val.AppendFormatted<uint>(clientState.TerritoryType);
		ImGui.Text(val);
		ImGui.TextDisabled(UiText.Render((activeProfile.TerritoryId == clientState.TerritoryType) ? "区域匹配" : "区域不匹配"));
		ImGui.Spacing();
		ImGui.TextDisabled(UiText.Render("战斗辅助职业"));
		ImGui.Text(UiText.Render(combatJob));
		if (silver >= 0 && copper >= 0)
		{
			ImGui.Spacing();
			ImGui.TextDisabled(UiText.Render("宝箱负载"));
			ImU8String val2 = new ImU8String(11, 4);
			val2.AppendLiteral(UiText.Render("银 "));
			val2.AppendFormatted<int>(silver);
			val2.AppendLiteral(UiText.Render("/"));
			val2.AppendFormatted<int>(8);
			val2.AppendLiteral(UiText.Render("  ·  铜 "));
			val2.AppendFormatted<int>(copper);
			val2.AppendLiteral(UiText.Render("/"));
			val2.AppendFormatted<int>(30);
			ImGui.Text(val2);
		}
	}

	private static void DrawBProgressStep(string title, string detail, bool complete, bool active)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		Vector4 vector;
		if (active)
		{
			vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
		}
		else
		{
			vector = (complete ? new Vector4(0.28f, 0.9f, 0.72f, 1f) : new Vector4(0.47f, 0.49f, 0.5f, 1f));
		}
		string text;
		if (active)
		{
			text = "◆";
		}
		else
		{
			text = (complete ? "■" : "□");
		}
		ImGui.TextColored(ref vector, UiText.Render(text));
		ImGui.SameLine();
		ImGui.TextColored(ref vector, UiText.Render(title));
		ImU8String val = new ImU8String(4, 1);
		val.AppendLiteral(UiText.Render("    "));
		val.AppendFormatted<string>(UiText.Render(detail));
		ImGui.TextDisabled(val);
		ImGui.Spacing();
	}

	private int ResolveBProgressStep()
	{
		if (!running)
		{
			return 0;
		}
		if (treasurePhase != TreasurePhase.None)
		{
			return 4;
		}
		if (currencyBuyer.IsBusy || currencyPurchaseMoveActive)
		{
			return 2;
		}
		if (waitingForEntry || islandSwitchPending)
		{
			return 1;
		}
		if (initialScan || waitingForScan || pendingScanAt != DateTime.MinValue)
		{
			return 2;
		}
		return 3;
	}

	private void DrawBConfigurationPanel()
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		ImGui.Dummy(new Vector2(0f, 6f));
		DrawBSectionTitle("副本与战斗配置", "副本设置");
		DrawBProfileConfig();
		ImGui.Spacing();
		DrawRequiredModulesButton();
		ImGui.Spacing();
		DrawAutomaticPurchaseConfig();
		ImGui.Spacing();
		ImGui.Spacing();
		ImGui.Spacing();
		DrawBDebug();
		ImGui.Spacing();
		if (!string.IsNullOrEmpty(treasureError))
		{
			Vector4 vector = new Vector4(1f, 0.3f, 0.3f, 1f);
			ImU8String val = new ImU8String(3, 1);
			val.AppendLiteral(UiText.Render("错误："));
			val.AppendFormatted<string>(UiText.Render(treasureError));
			ImGui.TextColored(ref vector, val);
		}
		DrawBFooter();
	}

	private static void DrawBSectionTitle(string title, string code)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		Vector4 vector = new Vector4(0.94f, 0.94f, 0.94f, 1f);
		ImGui.TextColored(ref vector, UiText.Render("◇"));
		ImGui.SameLine();
		ImGui.Text(UiText.Render(title));
		ImGui.SameLine();
		ImGui.TextDisabled(UiText.Render(code));
		ImGui.Separator();
	}

	private void DrawBProfileConfig()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		float num = MathF.Min(250f * ImGuiHelpers.GlobalScale, MathF.Max(30f, ImGui.GetContentRegionAvail().X - 200f * ImGuiHelpers.GlobalScale));
		if (!ImGui.BeginTable("BProfileConfig", 2, (ImGuiTableFlags)25088, default(Vector2), 0f))
		{
			return;
		}
		ImGui.TableSetupColumn(UiText.Label("BProfileLabel"), (ImGuiTableColumnFlags)8, 150f * ImGuiHelpers.GlobalScale, 0u);
		ImGui.TableSetupColumn(UiText.Label("BProfileValue"), (ImGuiTableColumnFlags)4, 0f, 0u);
		ImGui.TableNextRow((ImGuiTableRowFlags)0, ImGui.GetFrameHeight() + 8f);
		ImGui.TableNextColumn();
		ImGui.Text(UiText.Render("战斗辅助职业"));
		ImGui.TableNextColumn();
		ImGui.SetNextItemWidth(num);
		if (ImGui.BeginCombo(UiText.Label("##BCombatJob"), UiText.Render(combatJob), (ImGuiComboFlags)0))
		{
			string[] combatJobs = CombatJobs;
			foreach (string text in combatJobs)
			{
				bool flag = text == combatJob;
				if (ImGui.Selectable(UiText.Label(text), flag, (ImGuiSelectableFlags)0, default(Vector2)))
				{
					combatJob = text;
					config.CombatJob = combatJob;
					config.Save();
				}
				if (flag)
				{
					ImGui.SetItemDefaultFocus();
				}
			}
			ImGui.EndCombo();
		}
		ImGui.SameLine(0f, 6f);
		DrawInfoIcon("BCombatJobInfo", "注意：有些辅助职业的辅助技能可能与魔寻宝 CD 存在冲突，不接受因此所产生问题的反馈。默认选择的辅助白魔法师无此问题");
		ImGui.TableNextRow((ImGuiTableRowFlags)0, ImGui.GetFrameHeight() + 8f);
		ImGui.TableNextColumn();
		ImGui.Text(UiText.Render("DR 自动丢弃预设"));
		ImGui.TableNextColumn();
		ImGui.SetNextItemWidth(num);
		if (ImGui.InputText(UiText.Label("##BDiscardPreset"), ref discardPreset, 128, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
		{
			config.DiscardPreset = discardPreset;
			config.Save();
		}
		ImGui.SameLine(0f, 6f);
		DrawInfoIcon("BDiscardPresetInfo", "如需自动丢弃跑刀垃圾，请在此处填写 DR 自动丢弃物品模块的预设名称，留空则不启用");
		ImGui.TableNextRow((ImGuiTableRowFlags)0, ImGui.GetFrameHeight() + 8f);
		ImGui.TableNextColumn();
		ImGui.Text(UiText.Render("寻宝模式选择"));
		ImGui.TableNextColumn();
		string text2 = ((config.TreasureModeSelection == TreasureMode.XszRun) ? "XSZ 跑刀" : "DR 跑刀");
		ImGui.SetNextItemWidth(num);
		if (ImGui.BeginCombo(UiText.Label("##BTreasureMode"), UiText.Render(text2), (ImGuiComboFlags)0))
		{
			if (ImGui.Selectable(UiText.Label("DR 跑刀"), config.TreasureModeSelection == TreasureMode.DrRun, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				config.TreasureModeSelection = TreasureMode.DrRun;
				config.Save();
			}
			if (ImGui.Selectable(UiText.Label("XSZ 跑刀"), config.TreasureModeSelection == TreasureMode.XszRun, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				config.TreasureModeSelection = TreasureMode.XszRun;
				config.Save();
			}
			ImGui.EndCombo();
		}
		ImGui.SameLine(0f, 6f);
		DrawInfoIcon("BTreasureModeInfo", "XSZ 跑刀为 XSZToolbox 测试码功能，如果你没有权限则不要选择这个模式。");
		ImGui.TableNextRow((ImGuiTableRowFlags)0, ImGui.GetFrameHeight() + 6f);
		ImGui.TableNextColumn();
		ImGui.Text(UiText.Render("寻宝记录"));
		ImGui.TableNextColumn();
		if (ImGui.Button(UiText.Label("查看寻宝战利品记录"), new Vector2(num, 0f)))
		{
			((Window)treasureHistoryWindow).IsOpen = true;
		}
		ImGui.EndTable();

		// [GLOBAL] 進島後 N 分鐘自動退本重入 —— 已按用戶要求移除。
		// 原因：計時區塊放在 OnUpdate 內會引發 AccessViolationException / InvalidProgramException，
		//       兩次遊戲 crash 都來自這裡。欄位 autoLeaveAfterMinutes 保留但不再使用。
	}

	private static void DrawInfoIcon(string id, string tooltip)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		Vector2 vector = new Vector2(20f, 20f);
		ImU8String val = new ImU8String(2, 1);
		val.AppendLiteral(UiText.Render("##"));
		val.AppendFormatted<string>(UiText.Render(id));
		ImGui.InvisibleButton(val, vector, (ImGuiButtonFlags)0);
		Vector2 itemRectMin = ImGui.GetItemRectMin();
		Vector2 itemRectMax = ImGui.GetItemRectMax();
		Vector2 vector2 = (itemRectMin + itemRectMax) * 0.5f;
		ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
		uint colorU = ImGui.GetColorU32(new Vector4(0.94f, 0.94f, 0.94f, 1f));
		windowDrawList.AddCircle(vector2, 8f, colorU, 24, 1.4f);
		Vector2 vector3 = ImGui.CalcTextSize(UiText.Render("!"), false, -1f);
		windowDrawList.AddText(vector2 - vector3 * 0.5f, colorU, "!");
		if (ImGui.IsItemHovered((ImGuiHoveredFlags)128))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 360f);
			ImGui.TextWrapped(UiText.Render(tooltip));
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	private void DrawRequiredModulesButton()
	{
		if (ImGui.Button(UiText.Label("启用 DR 必要模块"), default(Vector2)))
		{
			Send("/pdr load OccultCrescentHelper");
			Send("/pdr load BetterMKDSupportJobList");
			Send("/pdr load PhantomJobSwitchCommand");
			Send("/pdr load AutoCommenceDuty");
			Send("/pdr load InstantLeaveDuty");
			Send("/pdr load FieldEntryCommand");
			status = "已发送一键开启 Daily Routines 模块指令";
		}
	}

	private void DrawBDebug()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (!ImGui.CollapsingHeader(UiText.Label("Debug"), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (ImGui.Button(UiText.Label("直接开始寻宝流程（测试用）"), default(Vector2)))
		{
			if (!running)
			{
				running = true;
			}
			silver = 8;
			copper = 0;
			BeginTreasureProcedure();
		}
		// [DEBUG] 完整循環測試開關（見 debugForceFull 的說明）
		ImGui.Checkbox(UiText.Label("Debug: 强制视为宝箱已满（测试完整流程）"), ref debugForceFull);
		// [DEBUG] 只測內環+外環，唔退本
		ImGui.Checkbox(UiText.Label("Debug: 不退本（只测内环+外环）"), ref debugNoLeaveDuty);
		// [DEBUG] 測試「時間到 → 自動退本 + 重新進島」。
		// 直接跳到 TreasurePhase.LeaveDuty，等同於跑完一次完整尋寶後的流程：
		//   LeaveDuty  → /pdr leaveduty → 等區域變更
		//   Reentry    → 若已離開島嶼 → BeginEntryWait() → /pdrfe ocn
		// 用來驗證「即刻退本」模組與進島握手關鍵字是否正常，不必等完整跑刀。
		if (ImGui.Button(UiText.Label("测试：立即退本并重新进岛"), default(Vector2)))
		{
			if (!running)
			{
				running = true;
			}
			treasurePhase = TreasurePhase.LeaveDuty;
			treasurePhaseAt = DateTime.MinValue;
			status = "寻宝完成，准备重进";
			log.Debug("[Debug] 手动触发「退本 + 重新进岛」测试", Array.Empty<object>());
		}
	}

	private void DrawBFooter()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		ImGui.Separator();
		ImGui.TextDisabled(UiText.Render(running ? "运行中" : "已停止"));
		ImGui.SameLine();
		if (running)
		{
			if (ImGui.Button(UiText.Label("停止运行"), default(Vector2)))
			{
				StopFromUser();
			}
		}
		else if (ImGui.Button(UiText.Label("开始运行"), default(Vector2)))
		{
			Start();
		}
		ImGui.SameLine();
		if (ImGui.Button(UiText.Label("关闭窗口"), default(Vector2)))
		{
			((Window)SelectedMainWindow).IsOpen = false;
		}
	}



	private void CheckForLogout()
	{
		// Stop active work when the character logs out.
		if (disposed)
		{
			return;
		}
		if (!clientState.IsLoggedIn)
		{
			StopForLogout();
		}
	}





	private void OpenSponsorPage()
	{
		try
		{
			Process.Start(CreateSponsorStartInfo());
		}
		catch (Exception ex)
		{
			log.Warning(ex, "无法打开 OCBFR 赞助页面", Array.Empty<object>());
		}
	}

	private void OpenMainUi()
	{
		if (!disposed) ((Window)SelectedMainWindow).IsOpen = true;
	}

	private void StopFromUser(bool emergency = false)
	{
		if (!disposed)
		{
			if (emergency)
			{
				EmergencyStop();
			}
			else
			{
				Stop("已通过手动操作停止");
			}
		}
	}

	private void ToggleMainUi()
	{
		if (!disposed) toggleMainUiPending = true;
	}


	private void MarkWindowLayoutsDirty()
	{
		if (!windowLayoutsDirty)
		{
			nextWindowLayoutSave = DateTime.UtcNow.AddMilliseconds(500.0);
		}
		windowLayoutsDirty = true;
	}

	private void FlushWindowLayouts(bool force = false)
	{
		if (windowLayoutsDirty && (force || !(DateTime.UtcNow < nextWindowLayoutSave)))
		{
			config.Save();
			windowLayoutsDirty = false;
		}
	}

	private void DrawWindows()
	{
		if (disposed) return;
		UiText.Language = config.UiLanguage;
		treasureHistoryWindow.WindowName = UiText.Label("寻宝战利品###OCNFarmerTreasureHistory");
		CheckForLogout();
		windows.Draw();
		if (toggleMainUiPending)
		{
			toggleMainUiPending = false;
			((Window)SelectedMainWindow).IsOpen = false;
			config.SimplifiedUi = !config.SimplifiedUi;
			((Window)SelectedMainWindow).IsOpen = true;
			config.Save();
		}
		FlushWindowLayouts();
	}

	private void DrawLanguageSelector()
	{
		ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
		string preview = config.UiLanguage == UiLanguage.English ? "English" : "繁體中文";
		if (!ImGui.BeginCombo("##OCBFRUiLanguage", preview, (ImGuiComboFlags)0)) return;
		foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
		{
			string label = language == UiLanguage.English ? "English" : "繁體中文";
			if (ImGui.Selectable(label, config.UiLanguage == language, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				config.UiLanguage = language;
				UiText.Language = language;
				config.Save();
			}
		}
		ImGui.EndCombo();
	}
}
