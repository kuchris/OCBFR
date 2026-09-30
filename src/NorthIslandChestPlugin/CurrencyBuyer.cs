using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Info.Game.Packets.Upstream;
using OmenTools.Interop.Game.AddonEvent;

namespace NorthIslandChestPlugin;

internal sealed class CurrencyBuyer : IDisposable
{
	private enum Phase
	{
		Idle,
		StartSession,
		SendPurchase,
		WaitForConfirmation,
		Verify,
		BetweenItems,
		BetweenRequests,
		Closing
	}

	private static readonly TimeSpan ProgressTimeout = TimeSpan.FromSeconds(90L);

	private static readonly TimeSpan ShopTimeout = TimeSpan.FromSeconds(12L);

	private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(6L);

	private const float CurrencyExchangeRadius = 15f;

	private static readonly TimeSpan EventStartDelay = TimeSpan.FromMilliseconds(2500L);

	private static readonly TimeSpan AgentReadyDelay = TimeSpan.FromMilliseconds(800L);

	private static readonly TimeSpan EventStartRetryDelay = TimeSpan.FromMilliseconds(800L);

	private static readonly TimeSpan WindowCleanupDuration = TimeSpan.FromMilliseconds(1500L);

	private static readonly TimeSpan BetweenRequestDelay = TimeSpan.FromMilliseconds(750L);

	private const int MaxEventStartAttempts = 3;

	private readonly IClientState clientState;

	private readonly IObjectTable objects;

	private readonly ICondition condition;

	private readonly IGameGui gameGui;

	private readonly IAddonLifecycle addonLifecycle;

	private readonly IPluginLog log;

	private readonly Action<bool, string> finished;


	private readonly Queue<CurrencyPurchaseRequest> queue = new Queue<CurrencyPurchaseRequest>();

	private readonly CurrencyPurchaseCatalog catalog = new CurrencyPurchaseCatalog();

	private Phase phase;

	private CurrencyPurchaseRequest current;

	private DateTime startedAt;

	private DateTime phaseDeadline;

	private DateTime nextActionAt;

	private DateTime windowCleanupUntil;

	private int currencyBefore;

	private int rewardBefore;

	private bool confirmationSent;

	private bool quantitySent;

	private bool purchaseSent;

	private int selectedItemIndex = -1;

	private CurrencyPurchaseRequest? remainder;

	private readonly Dictionary<string, int> purchased = new Dictionary<string, int>();

	private bool closeForNextRequest;

	private bool completionSuccess;

	private bool eventCompleted;

	private bool eventStartSent;

	private int eventStartAttempts;

	private uint activeEventId;

	private uint activeTerritoryId = 1346u;

	private Vector3 activeCurrencyExchangeAnchor = IslandProfile.North.CurrencyExchangeAnchor;

	private string activeChapterName = IslandProfile.North.ChapterName;

	private string completionMessage = string.Empty;

	internal bool IsBusy => phase != Phase.Idle;

	internal string Status { get; private set; } = "空闲";

	internal CurrencyBuyer(IClientState clientState, IObjectTable objects, ICondition condition, IGameGui gameGui, IAddonLifecycle addonLifecycle, IPluginLog log, Action<bool, string> finished)
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected Obj, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Expected Obj, but got Unknown
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Expected Obj, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected Obj, but got Unknown
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Expected Obj, but got Unknown
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Expected Obj, but got Unknown
		this.clientState = clientState;
		this.objects = objects;
		this.condition = condition;
		this.gameGui = gameGui;
		this.addonLifecycle = addonLifecycle;
		this.log = log;
		this.finished = finished;
		addonLifecycle.RegisterListener((AddonEvent)1, "ShopExchangeCurrency", (IAddonLifecycle.AddonEventDelegate)OnShopAddon);
		addonLifecycle.RegisterListener((AddonEvent)4, "ShopExchangeCurrency", (IAddonLifecycle.AddonEventDelegate)OnShopAddon);
		addonLifecycle.RegisterListener((AddonEvent)1, "ShopExchangeCurrencyDialog", (IAddonLifecycle.AddonEventDelegate)OnShopDialogAddon);
		addonLifecycle.RegisterListener((AddonEvent)4, "ShopExchangeCurrencyDialog", (IAddonLifecycle.AddonEventDelegate)OnShopDialogAddon);
		addonLifecycle.RegisterListener((AddonEvent)1, "SelectYesno", (IAddonLifecycle.AddonEventDelegate)OnConfirmAddon);
		addonLifecycle.RegisterListener((AddonEvent)4, "SelectYesno", (IAddonLifecycle.AddonEventDelegate)OnConfirmAddon);
	}

	public void Dispose()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected Obj, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected Obj, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected Obj, but got Unknown
		addonLifecycle.UnregisterListener(new IAddonLifecycle.AddonEventDelegate[1] { OnShopAddon });
		addonLifecycle.UnregisterListener(new IAddonLifecycle.AddonEventDelegate[1] { OnShopDialogAddon });
		addonLifecycle.UnregisterListener(new IAddonLifecycle.AddonEventDelegate[1] { OnConfirmAddon });
		if (IsBusy)
		{
			Cancel();
		}
	}

	internal unsafe bool Begin(IEnumerable<CurrencyPurchaseRequest> requests, IslandProfile profile)
	{
		if (IsBusy)
		{
			return false;
		}
		activeTerritoryId = profile.TerritoryId;
		activeCurrencyExchangeAnchor = profile.CurrencyExchangeAnchor;
		activeChapterName = profile.ChapterName;
		if (clientState.TerritoryType != activeTerritoryId || condition[(ConditionFlag)45] || condition[(ConditionFlag)51] || condition[(ConditionFlag)26] || IsOccupiedForShopEvent())
		{
			return false;
		}
		AtkUnitBase* addon = GetAddon("SelectYesno");
		if (addon != null && ((*addon)).IsVisible)
		{
			Status = "请先完成当前确认操作";
			return false;
		}
		if (!DService.IsInitialized)
		{
			Status = "自动购买功能暂未就绪";
			return false;
		}
		if (GetAddon("ShopExchangeCurrency") != null || GetAddon("ShopExchangeCurrencyDialog") != null)
		{
			Status = "当前无法开始自动购买";
			return false;
		}
		if (!IsNearCurrencyExchange())
		{
			Status = "请靠近钱币商人（15 码内）";
			return false;
		}
		foreach (CurrencyPurchaseRequest request in requests)
		{
			if (request.Cost <= 0 || request.ReceiveCount <= 0 || request.Quantity <= 0 || request.StackSize <= 0 || request.CurrencyItemId != ((request.Currency == CurrencyKind.Silver) ? profile.SilverCurrencyItemId : profile.GoldCurrencyItemId) || !catalog.Get(profile, request.Currency).Any((CurrencyShopItem x) => x.Matches(request)))
			{
				queue.Clear();
				Status = "购买配置无效";
				return false;
			}
			queue.Enqueue(request);
		}
		if (queue.Count == 0)
		{
			return false;
		}
		purchased.Clear();
		log.Debug("自动购买队列：" + string.Join("；", queue.Select((CurrencyPurchaseRequest x) => $"{x.CurrencyName}->{x.RewardName} ×{x.Quantity}")), Array.Empty<object>());
		startedAt = DateTime.UtcNow;
		StartNextRequest();
		return true;
	}

	internal void Cancel()
	{
		if (IsBusy)
		{
			queue.Clear();
			phase = Phase.Idle;
			remainder = null;
			windowCleanupUntil = DateTime.MinValue;
			CompleteEventSession();
			CloseShopWindows();
			purchaseSent = (confirmationSent = false);
			Status = "已取消自动购买";
		}
	}

	internal void Update()
	{
		if (!IsBusy)
		{
			return;
		}
		MaintainWindowCleanup();
		if (!clientState.IsLoggedIn || objects.LocalPlayer == null || clientState.TerritoryType != activeTerritoryId)
		{
			queue.Clear();
			eventCompleted = true;
			eventStartSent = false;
			activeEventId = 0u;
			FinishNow(success: false, "已离开" + activeChapterName);
			return;
		}
		if (phase == Phase.Closing)
		{
			DriveClosing();
			return;
		}
		if (DateTime.UtcNow - startedAt > ProgressTimeout)
		{
			Fail("自动购买超过 90 秒未取得进展");
			return;
		}
		if (condition[(ConditionFlag)45] || condition[(ConditionFlag)51])
		{
			Status = "购买中，等待过图...";
			return;
		}
		if (condition[(ConditionFlag)26])
		{
			Status = "购买中，等待脱战...";
			return;
		}
		switch (phase)
		{
		case Phase.StartSession:
			DriveStartSession();
			break;
		case Phase.SendPurchase:
			DriveSendPurchase();
			break;
		case Phase.WaitForConfirmation:
			DriveWaitForConfirmation();
			break;
		case Phase.Verify:
			VerifyPurchase();
			break;
		case Phase.BetweenItems:
			DriveBetweenItems();
			break;
		case Phase.BetweenRequests:
			DriveBetweenRequests();
			break;
		case Phase.Closing:
			DriveClosing();
			break;
		}
	}

	private void StartNextRequest(bool reuseShop = false)
	{
		confirmationSent = false;
		quantitySent = false;
		purchaseSent = false;
		selectedItemIndex = -1;
		remainder = null;
		if (queue.Count == 0)
		{
			BeginClosing(success: true, PurchaseSummary(), continueWithNextRequest: false);
			return;
		}
		current = queue.Dequeue();
		if (objects.LocalPlayer == null)
		{
			Fail("玩家不可用");
			return;
		}
		int num = RevalidateQuantity(current);
		if (num <= 0)
		{
			log.Debug("跳过" + current.RewardName + "：预算、目标持有量或背包空间已变化", Array.Empty<object>());
			StartNextRequest(reuseShop);
			return;
		}
		current = current with
		{
			Quantity = num
		};
		currencyBefore = GetItemCount(current.CurrencyItemId);
		rewardBefore = GetRewardCount(current);
		if (reuseShop && activeEventId == current.EventId)
		{
			phase = Phase.SendPurchase;
			phaseDeadline = DateTime.UtcNow + ShopTimeout;
			nextActionAt = DateTime.UtcNow + AgentReadyDelay;
		}
		else if (reuseShop)
		{
			CurrencyPurchaseRequest[] array = queue.ToArray();
			queue.Clear();
			queue.Enqueue(current);
			CurrencyPurchaseRequest[] array2 = array;
			foreach (CurrencyPurchaseRequest item in array2)
			{
				queue.Enqueue(item);
			}
			BeginClosing(success: true, PurchaseSummary(), continueWithNextRequest: true);
		}
		else
		{
			eventCompleted = false;
			eventStartSent = false;
			eventStartAttempts = 0;
			activeEventId = 0u;
			windowCleanupUntil = DateTime.MinValue;
			phase = Phase.StartSession;
			phaseDeadline = DateTime.UtcNow + ShopTimeout;
			nextActionAt = DateTime.UtcNow + EventStartDelay;
			Status = "准备购买...";
			log.Debug($"自动购买排队 EventStart player={GetLocalEntityId():X} event={current.EventId:X}", Array.Empty<object>());
		}
	}

	private void DriveStartSession()
	{
		SuppressShopWindow();
		if (DateTime.UtcNow < nextActionAt)
		{
			return;
		}
		if (!eventStartSent)
		{
			if (!CanSendEventStart())
			{
				if (DateTime.UtcNow >= phaseDeadline)
				{
					if (GetLocalEntityId() == 0)
					{
						Fail("玩家实体未就绪，已中止自动购买", "当前无法开始自动购买");
					}
					else
					{
						Fail("当前状态不允许发送 EventStart，已中止自动购买", "当前无法开始自动购买");
					}
				}
				else
				{
					Status = "准备购买...";
				}
			}
			else if (!TrySendEventStart())
			{
				eventStartAttempts++;
				if (eventStartAttempts >= 3)
				{
					Fail("EventStart 发包失败，已中止自动购买", "未能开始自动购买");
					return;
				}
				nextActionAt = DateTime.UtcNow + EventStartRetryDelay;
				Status = "准备购买...";
			}
			else
			{
				eventStartSent = true;
				activeEventId = current.EventId;
				nextActionAt = DateTime.UtcNow + AgentReadyDelay;
				Status = "准备购买...";
			}
		}
		else if (!IsShopAgentReady())
		{
			if (DateTime.UtcNow >= phaseDeadline)
			{
				Fail($"商店 Agent 未在 {ShopTimeout.TotalSeconds:0} 秒内就绪，已中止自动购买", "未能打开购买界面");
			}
		}
		else
		{
			phase = Phase.SendPurchase;
			phaseDeadline = DateTime.UtcNow + ShopTimeout;
			nextActionAt = DateTime.MinValue;
			Status = $"购买：{current.RewardName} ×{current.Quantity}";
		}
	}

	private void DriveSendPurchase()
	{
		SuppressShopWindow();
		if (DateTime.UtcNow < nextActionAt)
		{
			return;
		}
		if (HasTransactionDialog())
		{
			if (DateTime.UtcNow >= phaseDeadline)
			{
				Fail("上一笔购买确认未关闭");
			}
			return;
		}
		int num = RevalidateQuantity(current);
		if (num <= 0)
		{
			StartNextRequest(reuseShop: true);
			return;
		}
		current = current with
		{
			Quantity = num
		};
		currencyBefore = GetItemCount(current.CurrencyItemId);
		rewardBefore = GetRewardCount(current);
		if (!TrySendShopBuy(out var _))
		{
			if (DateTime.UtcNow >= phaseDeadline)
			{
				LogShopLookupFailure();
				Fail("商店中未找到" + current.RewardName + "，已停止以避免误购");
			}
		}
		else
		{
			Status = $"购买：{current.RewardName} ×{current.Quantity}";
		}
	}

	private void DriveWaitForConfirmation()
	{
		SuppressShopWindow();
		TryConfirmSelectYesno();
		if (PurchaseApplied())
		{
			PurchaseSucceeded();
		}
		else if (DateTime.UtcNow >= phaseDeadline)
		{
			Fail("购买确认超时或库存未变化");
		}
	}

	private void VerifyPurchase()
	{
		if (PurchaseApplied())
		{
			PurchaseSucceeded();
		}
		else if (DateTime.UtcNow >= phaseDeadline)
		{
			int itemCount = GetItemCount(current.CurrencyItemId);
			int itemCount2 = GetItemCount(current.RewardItemId);
			Fail($"购买库存验证失败：{current.CurrencyName} {currencyBefore}->{itemCount}，{current.RewardName} {rewardBefore}->{itemCount2}", "未能确认购买结果");
		}
	}

	private bool PurchaseApplied()
	{
		int itemCount = GetItemCount(current.CurrencyItemId);
		int rewardCount = GetRewardCount(current);
		if (purchaseSent && itemCount >= 0 && rewardCount >= 0 && itemCount <= currencyBefore - (long)current.Cost * (long)current.Quantity)
		{
			return rewardCount >= rewardBefore + (long)current.ReceiveCount * (long)current.Quantity;
		}
		return false;
	}

	private void PurchaseSucceeded()
	{
		startedAt = DateTime.UtcNow;
		log.Debug($"自动购买成功：{current.CurrencyName} -> {current.RewardName} ×{current.Quantity}", Array.Empty<object>());
		purchased[current.RewardName] = purchased.GetValueOrDefault(current.RewardName) + current.Quantity * current.ReceiveCount;
		CurrencyPurchaseRequest? currencyPurchaseRequest = remainder;
		if (currencyPurchaseRequest.HasValue)
		{
			CurrencyPurchaseRequest valueOrDefault = currencyPurchaseRequest.GetValueOrDefault();
			CurrencyPurchaseRequest[] array = queue.ToArray();
			queue.Clear();
			queue.Enqueue(valueOrDefault);
			CurrencyPurchaseRequest[] array2 = array;
			foreach (CurrencyPurchaseRequest item in array2)
			{
				queue.Enqueue(item);
			}
			remainder = null;
		}
		purchaseSent = false;
		if (queue.TryPeek(out var result) && result.EventId == activeEventId)
		{
			phase = Phase.BetweenItems;
			nextActionAt = DateTime.UtcNow + BetweenRequestDelay;
			phaseDeadline = DateTime.UtcNow + ShopTimeout;
		}
		else
		{
			BeginClosing(success: true, PurchaseSummary(), queue.Count > 0);
		}
	}

	private string PurchaseSummary()
	{
		if (purchased.Count != 0)
		{
			return "已购买：" + string.Join("；", purchased.Select((KeyValuePair<string, int> x) => $"{x.Key} ×{x.Value}"));
		}
		return "自动购买结束，无需兑换";
	}

	private bool HasTransactionDialog()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBasePtr addonByName = gameGui.GetAddonByName("ShopExchangeCurrencyDialog", 1);
		if (addonByName.IsNull)
		{
			addonByName = gameGui.GetAddonByName("SelectYesno", 1);
			return !addonByName.IsNull;
		}
		return true;
	}

	private void DriveBetweenItems()
	{
		if (DateTime.UtcNow >= phaseDeadline)
		{
			Fail("上一笔购买确认未关闭");
		}
		else if (!(DateTime.UtcNow < nextActionAt) && !HasTransactionDialog())
		{
			if (!IsShopAgentReady())
			{
				BeginClosing(success: true, PurchaseSummary(), continueWithNextRequest: true);
			}
			else
			{
				StartNextRequest(reuseShop: true);
			}
		}
	}

	private void Fail(string diagnosticMessage, string? userMessage = null)
	{
		if (phase != Phase.Closing)
		{
			log.Error("自动购买失败：" + (userMessage ?? diagnosticMessage), Array.Empty<object>());
			if (userMessage != null)
			{
				log.Debug("自动购买诊断：" + diagnosticMessage, Array.Empty<object>());
			}
			queue.Clear();
			BeginClosing(success: false, (userMessage ?? diagnosticMessage) + ((purchased.Count > 0) ? ("；" + PurchaseSummary()) : string.Empty), continueWithNextRequest: false);
		}
	}

	private void BeginClosing(bool success, string message, bool continueWithNextRequest)
	{
		completionSuccess = success;
		completionMessage = message;
		closeForNextRequest = continueWithNextRequest;
		phase = Phase.Closing;
		phaseDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(3L);
		nextActionAt = DateTime.MinValue;
		ScheduleWindowCleanup();
		Status = (success ? "购买收尾中..." : message);
	}

	private void DriveClosing()
	{
		CompleteEventSession();
		CloseShopWindows();
		if (!(DateTime.UtcNow < phaseDeadline))
		{
			if (closeForNextRequest && queue.Count > 0)
			{
				phase = Phase.BetweenRequests;
				nextActionAt = DateTime.UtcNow + BetweenRequestDelay;
				phaseDeadline = DateTime.UtcNow + ShopTimeout;
				Status = "继续购买...";
			}
			else
			{
				FinishNow(completionSuccess, completionMessage);
			}
		}
	}

	private unsafe void DriveBetweenRequests()
	{
		if (DateTime.UtcNow < nextActionAt)
		{
			return;
		}
		if (IsShopAgentReady() || IsOccupiedForShopEvent() || HasTransactionDialog() || GetAddon("ShopExchangeCurrency") != null)
		{
			if (DateTime.UtcNow >= phaseDeadline)
			{
				Fail("上一笔商店事件未完全退出，已停止以避免复用错误的钱币商店", "上一笔购买未能正常结束");
			}
			else
			{
				Status = "继续购买...";
			}
		}
		else
		{
			log.Debug($"上一钱币商店已退出，开始下一笔自动购买，剩余 {queue.Count} 笔", Array.Empty<object>());
			StartNextRequest();
		}
	}

	private void FinishNow(bool success, string message)
	{
		phase = Phase.Idle;
		CompleteEventSession();
		CloseShopWindows();
		queue.Clear();
		phase = Phase.Idle;
		activeEventId = 0u;
		Status = message;
		if (success)
		{
			log.Information("自动购买完成", Array.Empty<object>());
		}
		finished(success, message);
	}

	private void CompleteEventSession()
	{
		if (eventCompleted)
		{
			return;
		}
		eventCompleted = true;
		if (eventStartSent && activeEventId != 0)
		{
			try
			{
				new EventCompletePackt(activeEventId, 0u).Send();
				log.Debug($"自动购买 EventComplete event={activeEventId:X}", Array.Empty<object>());
			}
			catch (Exception ex)
			{
				log.Error(ex, $"自动购买 EventComplete 失败 event={activeEventId:X}", Array.Empty<object>());
			}
		}
		eventStartSent = false;
		activeEventId = 0u;
	}

	private bool TrySendEventStart()
	{
		uint localEntityId = GetLocalEntityId();
		if (!DService.IsInitialized || localEntityId == 0)
		{
			return false;
		}
		try
		{
			new EventStartPackt(localEntityId, current.EventId).Send();
			log.Debug($"自动购买 EventStart player={localEntityId:X} event={current.EventId:X} attempt={eventStartAttempts + 1}", Array.Empty<object>());
			return true;
		}
		catch (Exception ex)
		{
			log.Error(ex, $"自动购买 EventStart 失败 event={current.EventId:X}", Array.Empty<object>());
			return false;
		}
	}

	private unsafe bool CanSendEventStart()
	{
		if (clientState.TerritoryType != activeTerritoryId)
		{
			return false;
		}
		if (!clientState.IsLoggedIn || objects.LocalPlayer == null)
		{
			return false;
		}
		if (!IsNearCurrencyExchange())
		{
			return false;
		}
		if (condition[(ConditionFlag)45] || condition[(ConditionFlag)51])
		{
			return false;
		}
		if (condition[(ConditionFlag)26])
		{
			return false;
		}
		if (IsOccupiedForShopEvent())
		{
			return false;
		}
		if (!DService.IsInitialized || GetLocalEntityId() == 0)
		{
			return false;
		}
		AtkUnitBase* addon = GetAddon("SelectYesno");
		if (addon != null && ((*addon)).IsVisible)
		{
			return false;
		}
		return true;
	}

	private bool IsNearCurrencyExchange()
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		return IsNearCurrencyExchange((localPlayer != null) ? new Vector3?(((IGameObject)localPlayer).Position) : ((Vector3?)null));
	}

	private bool IsNearCurrencyExchange(Vector3? position)
	{
		if (!position.HasValue)
		{
			return false;
		}
		Vector3 value = position.Value;
		float num = value.X - activeCurrencyExchangeAnchor.X;
		float num2 = value.Z - activeCurrencyExchangeAnchor.Z;
		return num * num + num2 * num2 <= 225f;
	}

	private bool IsOccupiedForShopEvent()
	{
		if (!condition[(ConditionFlag)31] && !condition[(ConditionFlag)32] && !condition[(ConditionFlag)33])
		{
			return condition[(ConditionFlag)30];
		}
		return true;
	}

	private unsafe static bool IsShopAgentReady()
	{
		try
		{
			AgentShop* ptr = AgentShop.Instance();
			return ptr != null && ((*ptr)).IsAgentActive() && ((*ptr)).ItemReceive != null;
		}
		catch
		{
			return false;
		}
	}

	private bool TrySendShopBuy(out int itemIndex)
	{
		itemIndex = -1;
		if (purchaseSent || !TryGetAgentItemIndex(current, out itemIndex))
		{
			return false;
		}
		int num = Math.Min(current.Quantity, Math.Max(1, Math.Min(99, current.StackSize / current.ReceiveCount)));
		remainder = ((num < current.Quantity) ? new CurrencyPurchaseRequest?(current with
		{
			Quantity = current.Quantity - num
		}) : ((CurrencyPurchaseRequest?)null));
		current = current with
		{
			Quantity = num
		};
		selectedItemIndex = itemIndex;
		purchaseSent = true;
		confirmationSent = (quantitySent = false);
		phase = Phase.WaitForConfirmation;
		phaseDeadline = DateTime.UtcNow + ConfirmationTimeout;
		if (SendShopAgentEvent(itemIndex, current.Quantity))
		{
			return true;
		}
		Fail("未能发送购买请求，已停止以避免重复兑换");
		return false;
	}

	private unsafe bool TryGetAgentItemIndex(CurrencyPurchaseRequest request, out int itemIndex)
	{
		itemIndex = -1;
		AgentShop* ptr = AgentShop.Instance();
		if (!eventStartSent || activeEventId != request.EventId || ptr == null || !((*ptr)).IsAgentActive() || ((*ptr)).ItemReceive == null)
		{
			return false;
		}
		Span<AgentShop.ShopItem> itemReceiveSpan = ((*ptr)).ItemReceiveSpan;
		for (int i = 0; i < itemReceiveSpan.Length; i++)
		{
			if (MatchesShopEntry(request, itemReceiveSpan, i))
			{
				itemIndex = i;
				return true;
			}
		}
		return false;
	}

	internal static bool MatchesShopEntry(CurrencyPurchaseRequest request, ReadOnlySpan<AgentShop.ShopItem> items, int index)
	{
		if (index >= 0 && index < items.Length)
		{
			return items[index].ItemId == (uint)((int)request.RewardItemId + (request.HighQuality ? 1000000 : 0));
		}
		return false;
	}

	private unsafe void LogShopLookupFailure()
	{
		AgentShop* ptr = AgentShop.Instance();
		if (ptr == null)
		{
			log.Debug("商店诊断：AgentShop 不可用", Array.Empty<object>());
			return;
		}
		IEnumerable<string> values = ((*ptr)).ItemReceiveSpan.ToArray().Take(80).Select((AgentShop.ShopItem x, int i) =>
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			return $"{i}:{x.ItemId}/count={x.ItemCount}";
		});
		log.Debug($"商店诊断：requestedEvent={current.EventId}, activeEvent={activeEventId}, reward={current.RewardItemId}, active={((*ptr)).IsAgentActive()}, receiveRows={((*ptr)).ItemReceiveCount}, costRows={((*ptr)).ItemCostCount}; {string.Join(";", values)}", Array.Empty<object>());
	}

	private unsafe static bool SendShopAgentEvent(int itemIndex, int quantity)
	{
		if (itemIndex < 0 || quantity <= 0)
		{
			return false;
		}
		if (!IsShopAgentReady())
		{
			return false;
		}
		try
		{
			((AgentId)118).SendEvent(1uL, 0, itemIndex, quantity, 0);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void TryConfirmSelectYesno()
	{
		if (!CanConfirmPurchase() || !HasPurchaseConfirmation())
		{
			return;
		}
		confirmationSent = true;
		if (!AddonSelectYesnoEvent.ClickYes())
		{
			confirmationSent = false;
			return;
		}
		if (phase == Phase.WaitForConfirmation)
		{
			phase = Phase.Verify;
			phaseDeadline = DateTime.UtcNow + ConfirmationTimeout;
		}
		Status = $"购买：{current.RewardName} ×{current.Quantity}";
	}

	private bool CanConfirmPurchase()
	{
		bool flag = purchaseSent && !confirmationSent;
		if (flag)
		{
			Phase phase = this.phase;
			bool flag2 = (uint)(phase - 3) <= 1u;
			flag = flag2;
		}
		return flag;
	}

	private unsafe bool HasPurchaseConfirmation()
	{
		AtkUnitBase* addon = GetAddon("SelectYesno");
		if ((purchaseSent || confirmationSent) && addon != null)
		{
			return ((*addon)).IsVisible;
		}
		return false;
	}

	private unsafe bool IsCurrentSelection()
	{
		bool flag = !purchaseSent || activeEventId != current.EventId;
		if (!flag)
		{
			Phase phase = this.phase;
			bool flag2 = (uint)(phase - 3) <= 1u;
			flag = !flag2;
		}
		if (flag || !clientState.IsLoggedIn || clientState.TerritoryType != activeTerritoryId || condition[(ConditionFlag)26] || condition[(ConditionFlag)45] || condition[(ConditionFlag)51])
		{
			return false;
		}
		AgentShop* ptr = AgentShop.Instance();
		if (ptr != null && ((*ptr)).IsAgentActive() && ((*ptr)).SelectedItemIndex == selectedItemIndex)
		{
			return MatchesShopEntry(current, ((*ptr)).ItemReceiveSpan, selectedItemIndex);
		}
		return false;
	}

	private unsafe void OnShopAddon(AddonEvent type, AddonArgs args)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if ((phase == Phase.Idle && DateTime.UtcNow >= windowCleanupUntil))
		{
			return;
		}
		AtkUnitBasePtr addon = args.Addon;
		if (addon.IsNull)
		{
			return;
		}
		try
		{
			AtkUnitBase* address = (AtkUnitBase*)args.Addon.Address;
			if (address != null)
			{
				((*address)).IsVisible = false;
			}
		}
		catch
		{
		}
	}

	private unsafe void OnShopDialogAddon(AddonEvent type, AddonArgs args)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBasePtr addon = args.Addon;
		if (addon.IsNull)
		{
			return;
		}
		Phase phase = this.phase;
		if ((phase == Phase.Idle || phase == Phase.Closing) ? true : false)
		{
			if (DateTime.UtcNow < windowCleanupUntil)
			{
				HideAddon((AtkUnitBase*)args.Addon.Address);
			}
		}
		else
		{
			if (quantitySent || !IsCurrentSelection())
			{
				return;
			}
			try
			{
				AtkUnitBase* address = (AtkUnitBase*)args.Addon.Address;
				if (address == null || !((*address)).IsReady || !((*address)).IsVisible)
				{
					return;
				}
				quantitySent = true;
				((*address)).IsVisible = false;
				if (!FireCallback(address, 0, current.Quantity))
				{
					Fail("未能确认购买数量");
					return;
				}
				if (this.phase == Phase.WaitForConfirmation)
				{
					this.phase = Phase.Verify;
					phaseDeadline = DateTime.UtcNow + ConfirmationTimeout;
				}
				Status = $"购买：{current.RewardName} ×{current.Quantity}";
			}
			catch
			{
			}
		}
	}

	private void OnConfirmAddon(AddonEvent type, AddonArgs args)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		Phase phase = this.phase;
		bool flag = (uint)(phase - 2) <= 2u;
		if (flag && !confirmationSent)
		{
			AtkUnitBasePtr addon = args.Addon;
			if (!addon.IsNull)
			{
				TryConfirmSelectYesno();
			}
		}
	}


	private void ScheduleWindowCleanup()
	{
		windowCleanupUntil = DateTime.UtcNow + WindowCleanupDuration;
	}

	private void MaintainWindowCleanup()
	{
		if (!(DateTime.UtcNow >= windowCleanupUntil))
		{
			CloseShopWindows();
		}
	}

	private unsafe static void SuppressShopWindow()
	{
		try
		{
			AtkUnitBase* addonByName = (*RaptureAtkUnitManager.Instance()).GetAddonByName("ShopExchangeCurrency", 1);
			if (addonByName != null)
			{
				((*addonByName)).IsVisible = false;
			}
		}
		catch
		{
		}
	}

	private void CloseShopWindows()
	{
		Phase phase = this.phase;
		if ((phase == Phase.Idle || phase == Phase.Closing) ? true : false)
		{
			CloseWindow("ShopExchangeCurrencyDialog");
			CloseWindow("ShopExchangeCurrency");
			if (HasPurchaseConfirmation())
			{
				CloseWindow("SelectYesno");
			}
		}
		else
		{
			HideWindow("ShopExchangeCurrencyDialog");
			HideWindow("ShopExchangeCurrency");
			if (HasPurchaseConfirmation())
			{
				HideWindow("SelectYesno");
			}
		}
	}

	private unsafe void CloseWindow(string name)
	{
		try
		{
			AtkUnitBase* addon = GetAddon(name);
			if (addon != null)
			{
				((*addon)).IsVisible = false;
				if (phase == Phase.Closing || phase == Phase.Idle)
				{
					((*addon)).Close(true);
				}
			}
		}
		catch
		{
		}
	}

	private unsafe static void HideWindow(string name)
	{
		try
		{
			AtkUnitBase* addonByName = (*RaptureAtkUnitManager.Instance()).GetAddonByName(name, 1);
			if (addonByName != null)
			{
				((*addonByName)).IsVisible = false;
			}
		}
		catch
		{
		}
	}

	private unsafe static void HideAddon(AtkUnitBase* addon)
	{
		try
		{
			if (addon != null)
			{
				((*addon)).IsVisible = false;
			}
		}
		catch
		{
		}
	}

	private unsafe AtkUnitBase* GetAddon(string name)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			AtkUnitBasePtr addonByName = gameGui.GetAddonByName(name, 1);
			if (addonByName.IsNull)
			{
				return null;
			}
			AtkUnitBase* address = (AtkUnitBase*)addonByName.Address;
			return (address != null && ((*address)).IsReady) ? address : null;
		}
		catch
		{
			return null;
		}
	}

	private unsafe static bool FireCallback(AtkUnitBase* addon, params int[] args)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (addon == null || !((*addon)).IsReady)
			{
				return false;
			}
			if (args.Length == 1)
			{
				((*addon)).FireCallbackInt(args[0]);
				return true;
			}
			AtkValue* ptr = stackalloc AtkValue[args.Length];
			for (int i = 0; i < args.Length; i++)
			{
				Unsafe.Write(ptr + i, default(AtkValue));
				Unsafe.Write(&(*(ptr + i)).Type, (FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType)3);
				(*(ptr + i)).Int = args[i];
			}
			((*addon)).FireCallback((uint)args.Length, ptr, true);
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static int GetItemCount(uint itemId)
	{
		int num = ReadItemCount(itemId, highQuality: false);
		int num2 = ReadItemCount(itemId, highQuality: true);
		if (num >= 0 && num2 >= 0)
		{
			return num + num2;
		}
		return -1;
	}

	private static int GetRewardCount(CurrencyPurchaseRequest request)
	{
		return ReadItemCount(request.RewardItemId, request.HighQuality);
	}

	private unsafe static int ReadItemCount(uint itemId, bool highQuality)
	{
		try
		{
			InventoryManager* ptr = InventoryManager.Instance();
			if (ptr == null)
			{
				return -1;
			}
			return ((*ptr)).GetInventoryItemCount(itemId, highQuality, true, true, (short)0);
		}
		catch
		{
			return -1;
		}
	}

	private unsafe static uint GetLocalEntityId()
	{
		try
		{
			PlayerState* ptr = PlayerState.Instance();
			return (ptr != null) ? ((*ptr)).EntityId : 0u;
		}
		catch
		{
			return 0u;
		}
	}

	private static int RevalidateQuantity(CurrencyPurchaseRequest request)
	{
		return Math.Min(request.Quantity, CurrencyPurchasePlan.Quantity((request.TargetOwned > 0) ? request.TargetOwned : request.Quantity, (request.TargetOwned > 0) ? PurchaseQuantityMode.TargetOwned : PurchaseQuantityMode.PerBatch, GetItemCount(request.RewardItemId), GetItemCount(request.CurrencyItemId), request.Cost, request.ReceiveCount, request.Unique, ReadBag()?.Capacity(request.RewardItemId, request.HighQuality, request.StackSize) ?? 0));
	}

	internal unsafe static PurchaseBag? ReadBag()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		InventoryManager* ptr = InventoryManager.Instance();
		if (ptr == null)
		{
			return null;
		}
		int num = 0;
		List<(uint, bool, int)> list = new List<(uint, bool, int)>();
		InventoryType[] array = new InventoryType[4] { (InventoryType)0, (InventoryType)1, (InventoryType)2, (InventoryType)3 };
		InventoryType[] array2 = array;
		foreach (InventoryType val in array2)
		{
			InventoryContainer* inventoryContainer = ((*ptr)).GetInventoryContainer(val);
			if (inventoryContainer == null || !((*inventoryContainer)).IsLoaded)
			{
				return null;
			}
			for (int j = 0; j < ((*inventoryContainer)).Size; j++)
			{
				InventoryItem* inventorySlot = ((*inventoryContainer)).GetInventorySlot(j);
				if (inventorySlot == null)
				{
					return null;
				}
				if (((*inventorySlot)).ItemId == 0)
				{
					num++;
				}
				else
				{
					list.Add((((*inventorySlot)).ItemId, ((*inventorySlot)).IsHighQuality(), ((*inventorySlot)).Quantity));
				}
			}
		}
		return new PurchaseBag(num, list);
	}
}
