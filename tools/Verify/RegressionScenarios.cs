using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

// Runs managed plugin methods with fake Dalamud services. Never invokes native
// game actions; this validates state transitions, not live game behaviour.
internal static class RegressionScenarios
{
    public static int Run(Assembly assembly)
    {
        int failures = 0;
        if (assembly.GetManifestResourceNames().Contains("OCNFarmer.OmniConnection")) {
            failures++; Console.WriteLine("FAIL sharing build still embeds verification connection resource");
        } else Console.WriteLine("PASS sharing build excludes verification connection resource");
        try {
            var mainWindow = assembly.GetType("NorthIslandChestPlugin.Plugin+MainWindow", true)!;
            var postDraw = mainWindow.GetMethod("PostDraw")!;
            var il = postDraw.GetMethodBody()!.GetILAsByteArray()!;
            var parentToken = mainWindow.BaseType!.GetMethod("PostDraw")!.MetadataToken;
            // The failing source casts this to Window and makes a virtual call,
            // re-entering this override forever. Verify the emitted direct call.
            bool directParentCall = false;
            for (int i = 0; i + 4 < il.Length; i++) {
                if (il[i] != 0x28) continue;
                try {
                    var target = postDraw.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                    if (target?.Name == "PostDraw" && target.DeclaringType != mainWindow) directParentCall = true;
                } catch (ArgumentException) { }
            }
            if (!directParentCall) throw new Exception("PostDraw lacks direct base call; virtual call on this recurses (confirmed by live crash dump)");
            postDraw.Invoke(RuntimeHelpers.GetUninitializedObject(mainWindow), null);
            Console.WriteLine("PASS window PostDraw returns without recursion");
        } catch (Exception error) { failures++; Console.WriteLine("FAIL window PostDraw: " + error.GetBaseException().Message); }
        if (assembly.GetType("NorthIslandChestPlugin.VerificationSession") != null ||
            assembly.GetType("NorthIslandChestPlugin.Plugin+VerificationWindow") != null ||
            assembly.GetReferencedAssemblies().Any(a => a.Name == "Omni.Verification")) {
            failures++; Console.WriteLine("FAIL removed verification types or dependency remain");
        } else Console.WriteLine("PASS verification UI/session and assembly dependency removed");
        var configType = assembly.GetType("NorthIslandChestPlugin.Plugin+PluginConfig", true)!;
        if (configType.GetProperties().Any(p => p.Name.StartsWith("ServerChan") || p.Name.StartsWith("Notify"))) {
            failures++; Console.WriteLine("FAIL unattended notification settings remain");
        } else Console.WriteLine("PASS unattended notification configuration removed");
        void Test(string name, Action<Fixture> action) {
            var fixture = new Fixture(assembly);
            try { action(fixture); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.GetBaseException().Message); }
            finally { fixture.Cleanup(); }
        }
        var ui = assembly.GetType("NorthIslandChestPlugin.UiText", true)!;
        var uiLanguage = assembly.GetType("NorthIslandChestPlugin.UiLanguage", true)!;
        var languageProperty = ui.GetProperty("Language", BindingFlags.NonPublic | BindingFlags.Static)!;
        string Render(string value) => (string)ui.GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { value })!;
        string Label(string value) => (string)ui.GetMethod("Label", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { value })!;
        int OpenedCount(Fixture f) => (int)f.Get("cofferOpeningCounter")!.GetType().GetProperty("Count", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Get("cofferOpeningCounter"))!;
        Test("failed shard teleport retries without stopping the workflow", f => {
            f.Set("innerLeg", true); f.Set("currentCrystal", "3"); f.Set("shardTeleportAttempts", 1); f.Phase = "FirstWaitPlayers";
            var guard = f.Get("treasurePlayerGuard")!;
            guard.GetType().GetMethod("Begin")!.Invoke(guard, new object[] { f.Position, DateTime.UtcNow.AddMinutes(-4), true });
            f.Call("CheckCrystalPlayers");
            f.Assert((bool)f.Get("running")! && f.Phase == "FirstWaitPlayers" && f.Commands.Contains("/pdr ptp 3"), "teleport timeout stopped instead of retrying");
            f.Call("CheckCrystalPlayers");
            f.Assert(f.Commands.Count(c => c == "/pdr ptp 3") == 1, "retry command repeated every frame");
        });
        Test("expired crystal navigation restarts the cycle instead of leaving the character idle", f => {
            f.Position += new Vector3(100, 0, 0); f.Phase = "FirstCrystal";
            f.Set("crystalMoveDeadline", DateTime.UtcNow.AddMinutes(-1));
            f.Call("UpdateCrystalMove", true);
            f.Assert((bool)f.Get("running")! && f.Phase == "LeaveDuty", "navigation timeout disabled automatic recovery");
        });
        Test("three failed shard attempts recover once, retain loot and opening counts, and clear old timers", f => {
            var chest = f.AddCoffer(501, 1, true);
            f.Call("UpdateOpenedCofferCount"); chest(true); f.Call("UpdateOpenedCofferCount");
            f.Loot["test item"] = 2;
            f.Set("innerLeg", true); f.Set("currentCrystal", "3"); f.Set("shardTeleportAttempts", 3); f.Phase = "FirstWaitPlayers";
            f.Set("pendingScanAt", DateTime.UtcNow); f.Set("pendingBocchiAt", DateTime.UtcNow);
            var guard = f.Get("treasurePlayerGuard")!;
            guard.GetType().GetMethod("Begin")!.Invoke(guard, new object[] { f.Position, DateTime.UtcNow.AddMinutes(-4), true });
            f.Call("CheckCrystalPlayers");
            f.Assert((bool)f.Get("running")! && f.Phase == "LeaveDuty", "exhausted teleport did not enter recovery");
            f.Assert(f.Records.Count == 1 && OpenedCount(f) == 1, "recovery lost collected loot or reset run count");
            f.Assert((DateTime)f.Get("pendingScanAt")! == DateTime.MinValue && (DateTime)f.Get("pendingBocchiAt")! == DateTime.MinValue, "recovery kept delayed work");
            f.Call("RecoverIslandWorkflow", "repeat");
            f.Assert(f.Records.Count == 1 && f.Commands.Count(c => c == "/pdr ptreasure abort") == 1, "recovery duplicated each frame");
        });
        Test("teleport retry waits for combat and casting to finish", f => {
            f.Set("innerLeg", true); f.Set("currentCrystal", "3"); f.Set("shardTeleportAttempts", 1); f.Phase = "FirstWaitPlayers";
            var guard = f.Get("treasurePlayerGuard")!;
            guard.GetType().GetMethod("Begin")!.Invoke(guard, new object[] { f.Position, DateTime.UtcNow.AddMinutes(-4), true });
            f.Flags.Add(26); f.Call("CheckCrystalPlayers");
            f.Assert((bool)f.Get("running")! && !f.Commands.Contains("/pdr ptp 3") && (int)f.Get("shardTeleportAttempts")! == 1, "retried in combat");
            f.Flags.Clear(); f.Flags.Add(27); f.Call("CheckCrystalPlayers");
            f.Assert(!f.Commands.Contains("/pdr ptp 3"), "retried while casting");
            f.Flags.Clear(); f.Call("CheckCrystalPlayers");
            f.Assert(f.Commands.Count(c => c == "/pdr ptp 3") == 1 && (int)f.Get("shardTeleportAttempts")! == 2, "retry did not resume when ready");
        });
        Test("30-minute stationary watchdog recovers while ordinary same-map movement does not", f => {
            var now = DateTime.UtcNow;
            f.Phase = "InnerReturn"; f.Position += new Vector3(1000, 0, 0);
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now)!, "initial observation triggered recovery");
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(29))!, "watchdog triggered early");
            f.Position += new Vector3(5, 0, 0);
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(31))!, "same map with movement falsely recovered");
            f.Assert((bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(61))! && f.Phase == "LeaveDuty", "continuous stationary workflow did not recover");
        });
        Test("phase changes and openings reset the no-progress timer", f => {
            var now = DateTime.UtcNow;
            f.Phase = "FirstWaitPlayers"; f.Call("UpdateRecoveryWatchdog", now);
            f.Phase = "InnerReturn";
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(31))!, "new phase falsely recovered");
            var chest = f.AddCoffer(601, 1, true); f.Call("UpdateOpenedCofferCount"); chest(true); f.Call("UpdateOpenedCofferCount");
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(62))!, "new opening falsely recovered");
        });
        Test("watchdog cannot restart stopped work or interrupt combat and loading", f => {
            var now = DateTime.UtcNow;
            f.Phase = "FirstWaitPlayers"; f.Call("UpdateRecoveryWatchdog", now);
            f.Flags.Add(26);
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(31))! && f.Phase == "FirstWaitPlayers", "recovered in combat");
            f.Flags.Clear(); f.Call("UpdateRecoveryWatchdog", now.AddMinutes(32)); f.Flags.Add(45);
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(63))!, "recovered during loading");
            f.Flags.Clear(); f.Set("running", false);
            f.Assert(!(bool)f.Call("UpdateRecoveryWatchdog", now.AddMinutes(94))! && !(bool)f.Get("running")!, "watchdog restarted stopped plugin");
        });
        Test("framework watchdog completes the leave and reentry command sequence", f => {
            f.Phase = "FirstWaitPlayers";
            f.Call("UpdateRecoveryWatchdog", DateTime.UtcNow.AddMinutes(-31));
            f.Call("OnUpdate", f.Get("framework"));
            f.Assert(f.Phase == "LeaveDuty" && (bool)f.Get("running")!, "framework did not run watchdog before the waiting phase");
            var wait = f.Get("treasureMovementWait")!;
            wait.GetType().GetMethod("Begin")!.Invoke(wait, new object[] { DateTime.UtcNow.AddSeconds(-2), true, f.Territory, false });
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Commands.Contains("/pdr leaveduty") && f.Phase == "Reentry", "recovery did not send leave command");
            f.Set("observedTerritory", f.Territory); f.Territory = 1278;
            f.Call("OnTerritoryChanged", f.Territory); f.Call("OnUpdate", f.Get("framework"));
            f.Assert(f.Commands.Contains("/pdrfe ocn") && (bool)f.Get("waitingForEntry")! && (bool)f.Get("running")!, "recovery did not request fresh entry after leaving");
        });
        Test("live bronze and silver openings count once; loot and scans do not add openings", f => {
            var bronze = f.AddCoffer(101, 1, true);
            var silver = f.AddCoffer(102, 2, true);
            var unrelated = f.AddCoffer(103, 3, false);
            f.Call("UpdateOpenedCofferCount");
            bronze(true); silver(true); unrelated(true);
            f.Call("UpdateOpenedCofferCount");
            f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 2, "wrong combined count or repeated frame counted twice");
            f.Phase = "OuterReturn";
            f.Call("CaptureTreasureLoot", "You obtain 16 Enlightenment gold obols.");
            f.Call("CaptureTreasureLoot", "You obtain a test item.");
            f.Chat("You sense the presence of 0 silver coffers and 0 bronze coffers in the area!");
            f.Assert(OpenedCount(f) == 2, "loot quantity or scan counted as chest opening");
        });
        Test("already-open coffers and disappearance are not openings; reentry keeps the total", f => {
            var old = f.AddCoffer(201, 1, true);
            old(true);
            f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 0, "already-open coffer counted at baseline");
            var fresh = f.AddCoffer(202, 2, true);
            f.Call("UpdateOpenedCofferCount"); fresh(true); f.Call("UpdateOpenedCofferCount");
            f.WorldObjects.Clear(); f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 1, "despawn counted as opening");
            f.Territory = 1278; f.Call("UpdateOpenedCofferCount");
            f.Territory = 1346;
            var next = f.AddCoffer(202, 2, true);
            f.Call("UpdateOpenedCofferCount"); next(true); f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 2, "reentry cleared count or stale instance suppressed opening");
        });
        Test("stopped plugin preserves results; next accepted Start resets, rejected Start does not", f => {
            var chest = f.AddCoffer(301, 1, true);
            f.Call("UpdateOpenedCofferCount"); chest(true); f.Call("UpdateOpenedCofferCount");
            f.Call("Start");
            f.Assert(OpenedCount(f) == 1, "duplicate Start reset running statistics");
            f.Set("running", false); f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 1, "stop lost result");
            f.LoggedIn = false; f.Call("Start");
            f.Assert(OpenedCount(f) == 1, "rejected Start lost result");
            f.LoggedIn = true; f.Territory = 1278; f.Call("Start");
            f.Assert(OpenedCount(f) == 0, "accepted Start failed to reset counter");
        });
        Test("statistics reset clears saved history and in-progress loot, and cannot recount open coffers", f => {
            var chest = f.AddCoffer(401, 1, true);
            f.Call("UpdateOpenedCofferCount"); chest(true); f.Call("UpdateOpenedCofferCount");
            f.Loot["test item"] = 2; f.Call("SaveTreasureRecord");
            f.Assert((bool)f.Call("ResetTreasureStatistics")!, "reset failed");
            f.Assert(OpenedCount(f) == 0 && f.Records.Count == 0 && f.Loot.Count == 0, "statistics remain after reset");
            f.Call("LoadTreasureRecords"); f.Call("UpdateOpenedCofferCount");
            f.Assert(f.Records.Count == 0 && OpenedCount(f) == 0, "reset did not persist or re-counted opened coffer");
            var next = f.AddCoffer(402, 2, true); f.Call("UpdateOpenedCofferCount"); next(true); f.Call("UpdateOpenedCofferCount");
            f.Assert(OpenedCount(f) == 1, "reset stopped subsequent counting");
        });
        Test("failed reset preserves all visible statistics", f => {
            f.Loot["test item"] = 2; f.Call("SaveTreasureRecord");
            f.Set("treasureRecordPath", Path.Combine(f.RecordPath, "invalid.json"));
            f.Assert(!(bool)f.Call("ResetTreasureStatistics")!, "reset unexpectedly succeeded with a file as the parent directory");
            f.Assert(f.Records.Count == 1 && f.Loot.Count == 1, "failed write discarded statistics");
        });
        Test("tower options and weather navigation are absent from the plugin", f => {
            f.Assert(!configType.GetProperties().Any(p => p.Name.Contains("Tower")), "tower options remain");
            f.Assert(!assembly.GetTypes().Any(t => t.Name.Contains("Tower")), "tower phase type remains");
            var pluginType = assembly.GetType("NorthIslandChestPlugin.Plugin", true)!;
            f.Assert(!pluginType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Any(m => m.Name.Contains("Tower") || m.Name == "GetCurrentWeatherId"), "tower/weather methods remain");
        });
        Test("old enabled tower options are dropped without losing other settings", f => {
            var jsonConvert = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert", true)!;
            var deserialize = jsonConvert.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!;
            const string legacy = "{\"Version\":4,\"AutoGoTower\":true,\"AutoGoTowerExpanded\":true,\"UiLanguage\":1,\"CombatJob\":\"Phantom Samurai\",\"DiscardPreset\":\"MyPreset\",\"NorthPurchase\":{\"SilverTriggerAmount\":7777}}";
            var migrated = deserialize.Invoke(null, new object[] { legacy, configType })!;
            f.Set("config", migrated);
            f.Call("NormalizePurchaseConfig");
            f.Assert((int)configType.GetProperty("Version")!.GetValue(migrated)! == 5, "schema migration did not run");
            f.Assert((string)configType.GetProperty("CombatJob")!.GetValue(migrated)! == "Phantom Samurai", "combat job lost");
            f.Assert((string)configType.GetProperty("DiscardPreset")!.GetValue(migrated)! == "MyPreset", "discard preset lost");
            f.Assert(configType.GetProperty("UiLanguage")!.GetValue(migrated)!.ToString() == "English", "UI language lost");
            var purchase = configType.GetProperty("NorthPurchase")!.GetValue(migrated)!;
            f.Assert((int)purchase.GetType().GetProperty("SilverTriggerAmount")!.GetValue(purchase)! == 7777, "purchase threshold lost");
            string saved = (string)jsonConvert.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new[] { migrated })!;
            f.Assert(!saved.Contains("AutoGoTower"), "removed options survive serialization");
        });
        Test("North start directly schedules currency check before scanning", f => {
            f.Set("running", false);
            f.Call("Start");
            f.Assert((bool)f.Get("running")! && (bool)f.Get("initialCurrencyCheckPending")!, "start did not continue to initial checks");
            f.Assert((DateTime)f.Get("pendingCurrencyCheckAt")! != DateTime.MinValue, "initial currency check missing");
            f.Assert(f.Phase == "None" && !f.Commands.Any(c => c.StartsWith("/vnav moveto") || c == "/pdr ptp 3"), "start unexpectedly navigates away");
        });
        Test("embedded UI catalog has no leftover Simplified characters in Traditional Chinese", f => {
            using var stream = assembly.GetManifestResourceStream("OCBFR.UiTranslations.json")!;
            using var catalog = System.Text.Json.JsonDocument.Parse(stream);
            foreach (var entry in catalog.RootElement.EnumerateArray()) {
                string text = entry.GetProperty("TraditionalChinese").GetString()!;
                f.Assert(!text.Any(c => "战斗后并范默认复闲么".Contains(c)), "Incomplete Traditional Chinese entry: " + text);
                string english = entry.GetProperty("English").GetString()!;
                f.Assert(!english.Any(c => c >= '\u4e00' && c <= '\u9fff'), "Incomplete English entry: " + english);
            }
        });
        Test("UI language switches without altering stored state or job commands", f => {
            f.Set("combatJob", "Phantom White Mage");
            languageProperty.SetValue(null, Enum.Parse(uiLanguage, "TraditionalChinese"));
            f.Assert(Render("开始运行") == "開始運行", "Traditional Chinese start label missing");
            f.Assert(Render("Phantom White Mage") == "幻境白魔法師", "Traditional job display missing");
            f.Call("RequestFreelancerScan", "test");
            f.Assert(f.Commands.Contains("/pdr pjob Phantom Freelancer"), "translated UI changed command name");
            f.Assert((string)f.Get("combatJob")! == "Phantom White Mage", "translated UI changed stored job");
            languageProperty.SetValue(null, Enum.Parse(uiLanguage, "English"));
            f.Assert(Render("开始运行") == "Start" && Render("检测宝箱...") == "Scanning coffers...", "English labels/status missing or stale cache");
            f.Assert(Render("Phantom White Mage") == "Phantom White Mage", "English job display changed");
        });
        Test("translated controls retain legacy ImGui identity", f => {
            languageProperty.SetValue(null, Enum.Parse(uiLanguage, "English"));
            f.Assert(Label("开始运行") == "Start###开始运行", "plain legacy button ID lost");
            f.Assert(Label("仅展示稀有物品##TreasureTotalsRare") == "Rare items only###仅展示稀有物品##TreasureTotalsRare", "legacy ## ID lost");
            f.Assert(Label("寻宝战利品###OCNFarmerTreasureHistory") == "Treasure loot###OCNFarmerTreasureHistory", "persistent window ID lost");
            f.Assert(Label("##BDiscardPreset") == "##BDiscardPreset", "hidden input ID changed");
        });
        Test("translation preserves user presets, unknown item names and numbers", f => {
            languageProperty.SetValue(null, Enum.Parse(uiLanguage, "English"));
            f.Assert(Render("银箱 08/08  ·  铜箱 30/30") == "Silver coffers 08/08  ·  Bronze coffers 30/30", "translated counts changed");
            f.Assert(Render("Enlightenment gold obols ×16") == "Enlightenment gold obols ×16", "game item name or quantity changed");
            f.Assert(Label("##PurchaseSearch") == "##PurchaseSearch", "purchase input ID changed");
            f.Assert(configType.GetProperty("UiLanguage") != null && configType.GetProperty("CombatJob")!.PropertyType == typeof(string), "existing config schema changed");
        });
        languageProperty.SetValue(null, Enum.Parse(uiLanguage, "TraditionalChinese"));
        var gameText = assembly.GetType("NorthIslandChestPlugin.GameText", true)!;
        object? GameCall(string method, params object?[] arguments) => gameText.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments);
        foreach (var example in new[] {
            ("Japanese active scan", "このエリアから、銀の宝箱0個、銅の宝箱1個の気配を感じる……！", 0, 1),
            ("Simplified patch active scan", "当前区域内似乎有7个银宝箱和29个铜宝箱！", 7, 29),
            ("Traditional patch active scan", "當前區域內似乎有7個銀寶箱和29個銅寶箱！", 7, 29),
            ("Japanese full-width count scan", "このエリアから、銀の宝箱０個、銅の宝箱１個の気配を感じる……！", 0, 1),
            ("Japanese empty scan", "このエリアには、今は宝箱はなさそうだ……", 0, 0),
            ("Simplified patch empty scan", "当前区域现在似乎没有宝箱……", 0, 0),
            ("Traditional patch empty scan", "當前區域現在似乎沒有寶箱……", 0, 0)
        }) Test(example.Item1, f => {
            f.Set("waitingForScan", true); f.Set("combatJob", "Phantom White Mage");
            f.Chat(example.Item2);
            f.Assert((int)f.Get("silver")! == example.Item3 && (int)f.Get("copper")! == example.Item4, "wrong localized count");
            f.Assert(!(bool)f.Get("waitingForScan")! && f.Phase == "None", "localized scan did not complete normally");
            f.Assert(f.Commands.Contains("/pdr pjob Phantom White Mage"), "localized scan changed combat command");
        });
        foreach (string language in new[] { "TraditionalChinese", "English" }) {
            languageProperty.SetValue(null, Enum.Parse(uiLanguage, language));
            foreach (string message in new[] {
                "このエリアから、銀の宝箱8個、銅の宝箱29個の気配を感じる……！",
                "当前区域内似乎有7个银宝箱和30个铜宝箱！",
                "當前區域內似乎有8個銀寶箱和29個銅寶箱！"
            }) Test(language + " UI preserves localized threshold transition: " + message, f => {
                f.Chat(message);
                f.Assert(f.Phase == "FirstMove", "localized full count did not start route");
            });
        }
        languageProperty.SetValue(null, Enum.Parse(uiLanguage, "TraditionalChinese"));
        Test("partial, overflowing or wrong chat type cannot complete localized scan", f => {
            f.Set("waitingForScan", true);
            f.Chat("銀の宝箱8個");
            f.Chat("銀の宝箱999999999999999個、銅の宝箱1個");
            f.Chat("このエリアから、銀の宝箱8個、銅の宝箱30個の気配を感じる……！", 10);
            f.Assert((bool)f.Get("waitingForScan")! && f.Phase == "None", "accepted incomplete or wrong-channel count");
        });
        foreach (string message in new[] {
            "今はサポートジョブを変更できません。",
            "战斗中无法切换辅助职业。",
            "戰鬥中無法切換輔助職業。"
        }) Test("localized job rejection retries before scanning: " + message, f => {
            f.Call("RequestFreelancerScan", "test"); f.Chat(message); f.Call("AdvanceFreelancerScan");
            f.Assert(f.Commands.Count(c => c == "/pdr pjob Phantom Freelancer") == 2 && !(bool)f.Get("waitingForScan")!, "did not retry denied job change");
        });
        Test("entry sync supports English, Japanese and both Chinese scripts", f => {
            foreach (string message in new[] { "Your item level has been synced to 700.", "「アイテムレベル：700」にアイテムレベルシンクされました。", "当前任务设有品级同步限制。", "當前任務設有品級同步限制。" }) {
                f.Set("waitingForEntry", true); f.Set("entrySyncMessageSeen", false);
                f.Chat(message);
                f.Assert((bool)f.Get("entrySyncMessageSeen")!, "localized entry handshake ignored");
            }
        });
        Test("Freelancer acknowledgement checks the local player and canonical job", f => {
            f.Assert((bool)GameCall("IsFreelancerChange", "K. T.はサポートジョブを 「サポートすっぴん」にチェンジした。", "K. T.", "Phantom Freelancer")!, "Japanese Freelancer acknowledgement ignored");
            f.Assert((bool)GameCall("IsFreelancerChange", "K. T.切换为了辅助自由人。", "K. T.", "Phantom Freelancer")!, "patch Freelancer acknowledgement ignored");
            f.Assert(!(bool)GameCall("IsFreelancerChange", "Other Personはサポートジョブを 「サポートすっぴん」にチェンジした。", "K. T.", "Phantom Freelancer")!, "accepted another player's job change");
        });
        foreach (var example in new[] {
            ("Japanese currency loot", "十二都市金貨を16枚手に入れた。", "十二都市金貨", 16),
            ("Japanese item loot", "「十二都市金貨」×16を手に入れた。", "十二都市金貨", 16),
            ("Japanese single item loot", "「テストアイテム」を入手した。", "テストアイテム", 1),
            ("Traditional patch loot", "獲得了16枚十二城邦金幣。", "十二城邦金幣", 16),
            ("Simplified patch loot", "获得了16枚十二城邦金币。", "十二城邦金币", 16),
            ("English thousands loot", "You obtain 1,000 Enlightenment gold obols.", "Enlightenment gold obols", 1000)
        }) Test(example.Item1, f => {
            f.Phase = "InnerReturn"; f.Call("CaptureTreasureLoot", example.Item2);
            f.Assert(f.Loot.Contains(example.Item3) && (int)f.Loot[example.Item3]! == example.Item4, "localized loot name or quantity lost");
        });
        Test("Japanese bare item requires verified link and loot capture phase", f => {
            f.Phase = "InnerReturn";
            f.Call("CaptureTreasureLoot", "十二都市金貨×16");
            f.Assert(f.Loot.Count == 0, "bare text accepted without a verified item name");
            f.Call("RecordTreasureLoot", "十二都市金貨×16", "十二都市金貨");
            f.Assert((int)f.Loot["十二都市金貨"]! == 16, "verified bare Japanese item not recorded");
            f.Phase = "None"; f.Call("RecordTreasureLoot", "十二都市金貨×16", "十二都市金貨");
            f.Assert((int)f.Loot["十二都市金貨"]! == 16, "captured loot outside treasure route");
        });
        Test("another player's Japanese loot does not affect our records", f => {
            f.Phase = "OuterReturn";
            f.Call("CaptureTreasureLoot", "Other Personは「十二都市金貨」×16を手に入れた。");
            f.Assert(f.Loot.Count == 0, "captured another player's loot");
        });
        Test("full active scan starts exactly one treasure procedure", f => {
            f.Set("waitingForScan", true);
            f.Chat("There are 8 silver coffers and 30 bronze coffers in this area.");
            f.Assert(f.Logs.Count(s => s.Contains("宝箱达到上限")) == 1, "full scan restarted procedure twice");
        });
        Test("debug force-full follows the real scan-completion path", f => {
            f.Set("waitingForScan", true); f.Set("debugForceFull", true);
            f.Chat("There are 0 silver coffers and 0 bronze coffers in this area.");
            f.Assert(f.Phase == "FirstMove", "force-full did not start after scan");
        });
        Test("English treasure loot records quantity", f => {
            f.Phase = "InnerReturn";
            f.Call("CaptureTreasureLoot", "You obtain 16 Enlightenment gold obols.");
            f.Assert((int)f.Loot["Enlightenment gold obols"]! == 16, "English loot quantity was lost");
        });
        Test("passive bronze threshold starts treasure and cancels delayed combat", f => {
            f.Set("pendingBocchiAt", DateTime.UtcNow.AddSeconds(5));
            f.Chat("There are 7 silver coffers and 30 bronze coffers in this area.");
            f.Assert(f.Phase == "FirstMove", "bronze threshold ignored");
            f.Assert((DateTime)f.Get("pendingBocchiAt")! == DateTime.MinValue, "combat would restart during treasure route");
        });
        Test("unrelated chat cannot start treasure from stale full count", f => {
            f.Set("silver", 8); f.Set("copper", 30);
            f.Chat("Hello", 10);
            f.Assert(f.Phase == "None", "unrelated chat started treasure");
        });
        Test("stopped plugin updates count without starting treasure", f => {
            f.Set("running", false);
            f.Chat("There are 8 silver coffers and 30 bronze coffers in this area.");
            f.Assert(f.Phase == "None" && (int)f.Get("silver")! == 8, "stopped plugin acted or ignored display count");
        });
        Test("below-threshold passive count does not start treasure", f => {
            f.Chat("There are 7 silver coffers and 29 bronze coffers in this area.");
            f.Assert(f.Phase == "None", "started below threshold");
        });
        Test("live singular bronze message completes scan without retry", f => {
            f.Set("waitingForScan", true);
            f.Chat("You sense the presence of 0 silver coffers and 1 bronze coffer in the area!");
            f.Assert((int)f.Get("silver")! == 0 && (int)f.Get("copper")! == 1, "singular bronze count not parsed");
            f.Assert(!(bool)f.Get("waitingForScan")!, "scan still waiting despite count message");
        });
        Test("singular silver and plural bronze start passive treasure at bronze threshold", f => {
            f.Chat("You sense the presence of 1 silver coffer and 30 bronze coffers in the area!");
            f.Assert(f.Phase == "FirstMove" && (int)f.Get("silver")! == 1, "singular silver blocked bronze threshold");
        });
        Test("scan after mounting waits then requests Freelancer before casting", f => {
            f.Flags.Add(64);
            f.Call("RequestFreelancerScan", "test");
            f.Assert(!f.Commands.Any(c => c.StartsWith("/pdr pjob")), "changed job during mounting");
            f.Flags.Clear();
            f.Call("AdvanceFreelancerScan");
            f.Assert(f.Commands.Contains("/pdr pjob Phantom Freelancer"), "skipped Freelancer after dismount");
            f.Assert(!(bool)f.Get("waitingForScan")!, "cast before requesting Freelancer");
        });
        Test("scan preparation cancels delayed combat and stops navigation", f => {
            f.Set("pendingBocchiAt", DateTime.UtcNow.AddSeconds(1)); f.Set("bocchiEnabled", true);
            f.Call("RequestFreelancerScan", "test");
            f.Assert((DateTime)f.Get("pendingBocchiAt")! == DateTime.MinValue && !(bool)f.Get("bocchiEnabled")!, "combat can start while scanning");
            f.Assert(f.Commands.Contains("/bocchiillegal off") && f.Commands.Contains("/vnav stop"), "did not stop movement before scan");
        });
        Test("unanswered scan retries without starting combat or losing initial scan", f => {
            f.Set("waitingForScan", true);
            f.Call("RetryUnansweredTreasureScan");
            f.Assert((bool)f.Get("initialScan")!, "marked initial scan complete without counts");
            f.Assert((DateTime)f.Get("pendingScanAt")! > DateTime.UtcNow, "missing delayed retry");
            f.Assert((DateTime)f.Get("pendingBocchiAt")! == DateTime.MinValue && !f.Commands.Contains("/bocchiillegal on"), "combat resumed without scan result");
        });
        Test("rejected job change retries the job command before casting", f => {
            f.Call("RequestFreelancerScan", "test");
            f.Chat("You are unable to change phantom jobs at this time.");
            f.Call("AdvanceFreelancerScan");
            f.Assert(f.Commands.Count(c => c == "/pdr pjob Phantom Freelancer") == 2, "did not retry rejected job command");
            f.Assert(!(bool)f.Get("waitingForScan")!, "cast despite rejected change");
        });
        Test("position return does not advance outside target island", f => {
            f.Territory = 1278;
            f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "InnerReturn", "advanced outer ring while outside target island");
        });
        Test("position return waits through zone transition", f => {
            f.Flags.Add(45);
            f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "InnerReturn", "advanced during BetweenAreas");
        });
        Test("position return waits until return cast finishes", f => {
            f.Flags.Add(27); f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "InnerReturn", "advanced while casting");
        });
        Test("inner return advances at base", f => {
            f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "SecondMove", "did not advance to outer ring");
        });
        Test("inner return keeps waiting away from base", f => {
            f.Position += new Vector3(1000, 0, 0);
            f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "InnerReturn", "finished before reaching base");
        });
        Test("mounted player at shard starts inner route when area is clear", f => {
            var guard = f.Get("treasurePlayerGuard")!;
            guard.GetType().GetMethod("Begin")!.Invoke(guard, new object[] { f.Position, DateTime.UtcNow, true });
            f.Position += new Vector3(1000, 0, 0); f.Flags.Add(4); f.Set("innerLeg", true); f.Phase = "FirstWaitPlayers";
            f.Call("CheckCrystalPlayers");
            f.Assert(f.Commands.Contains("/pdr ptreasure 内环") && f.Phase == "InnerReturn", "clear mounted shard did not start route");
        });
        Test("casting player at shard waits before route", f => {
            var guard = f.Get("treasurePlayerGuard")!;
            guard.GetType().GetMethod("Begin")!.Invoke(guard, new object[] { f.Position, DateTime.UtcNow, true });
            f.Position += new Vector3(1000, 0, 0); f.Flags.Add(4); f.Flags.Add(27); f.Set("innerLeg", true); f.Phase = "FirstWaitPlayers";
            f.Call("CheckCrystalPlayers");
            f.Assert(f.Phase == "FirstWaitPlayers" && !f.Commands.Any(c => c.StartsWith("/pdr ptreasure")), "started while casting");
        });
        Test("outer position return persists loot once", f => {
            f.Phase = "OuterReturn";
            f.Loot["Enlightenment gold obols"] = 16;
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "LeaveDuty", "did not schedule leave");
            f.Assert(f.Records.Count == 1 && File.Exists(f.RecordPath), "outer position completion discarded treasure history");
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Records.Count == 1, "duplicated treasure history");
        });
        Test("outer return in no-leave test mode still persists loot exactly once", f => {
            f.Set("debugNoLeaveDuty", true); f.Phase = "OuterReturn";
            f.Loot["Enlightenment silver obols"] = 10;
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "None" && f.Records.Count == 1 && File.Exists(f.RecordPath), "test-mode completion did not save history");
            f.Chat("K. T. uses Occult Return.", 43);
            f.Assert(f.Records.Count == 1 && !f.Commands.Contains("/pdr leaveduty"), "late chat duplicated completion or left test duty");
        });
        Test("reentry cancels previous pending scan and combat timers", f => {
            f.Phase = "InnerReturn";
            foreach (var name in new[] { "pendingScanAt", "pendingBocchiAt", "pendingReturnScanAt", "pendingCurrencyCheckAt", "pendingPurchaseAt" }) f.Set(name, DateTime.UtcNow);
            f.Call("BeginEntryWait", "test");
            f.Assert(f.Phase == "None", "stale treasure phase");
            foreach (var name in new[] { "pendingScanAt", "pendingBocchiAt", "pendingReturnScanAt", "pendingCurrencyCheckAt", "pendingPurchaseAt" })
                f.Assert((DateTime)f.Get(name)! == DateTime.MinValue, "stale timer: " + name);
        });
        Test("unexpected exit while waiting for return starts fresh entry", f => {
            f.Territory = 1278; f.Position += new Vector3(1000, 0, 0); f.Phase = "InnerReturn";
            f.Call("OnUpdate", f.Get("framework"));
            f.Assert(f.Phase == "None" && (bool)f.Get("waitingForEntry")!, "remained stuck in old inner route outside island");
            f.Assert(f.Commands.Contains("/pdrfe ocn"), "did not request reentry");
        });
        Test("logout still stops active work after verification removal", f => {
            f.LoggedIn = false; f.Phase = "InnerReturn";
            f.Call("OnUpdate", f.Get("framework"));
            f.Assert(!(bool)f.Get("running")! && f.Phase == "None", "logout left active route state");
            f.Assert(f.Commands.Contains("/pdr ptreasure abort") && f.Commands.Contains("/vnav stop"), "logout did not stop external route");
        });
        Test("South return uses South territory and base coordinates", f => {
            f.SelectIsland("South"); f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "SecondMove", "did not advance at South base");
        });
        Test("South outer return records loot at South base", f => {
            f.SelectIsland("South"); f.Phase = "OuterReturn"; f.Loot["Enlightenment gold obols"] = 16;
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "LeaveDuty" && f.Records.Count == 1, "South outer completion failed");
        });
        Test("South selection rejects North territory and reenters South after exit", f => {
            f.SelectIsland("South"); f.Territory = 1346; f.Phase = "InnerReturn";
            f.Call("UpdateTreasureProcedure");
            f.Assert(f.Phase == "InnerReturn", "accepted wrong island");
            f.Territory = 1278;
            f.Call("OnUpdate", f.Get("framework"));
            f.Assert(f.Commands.Contains("/pdrfe ocs") && !f.Commands.Contains("/pdrfe ocn"), "reentry used wrong profile");
        });
        Console.WriteLine($"Managed regression failures: {failures}");
        return failures;
    }

    internal sealed class Fixture
    {
        readonly Assembly assembly;
        readonly Type pluginType;
        readonly object plugin;
        public readonly List<string> Logs = new();
        public readonly List<string> Commands = new();
        public readonly HashSet<int> Flags = new();
        public readonly List<object> WorldObjects = new();
        readonly List<IntPtr> nativeCoffers = new();
        public uint Territory = 1346;
        public bool LoggedIn = true;
        public Vector3 Position = new(882, 258.5f, 882);
        static readonly string TestRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts", "regression-tmp"));
        public string RecordPath = Path.Combine(TestRoot, "OCNFarmer-tests-" + Guid.NewGuid(), "treasure-records.json");
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public Fixture(Assembly assembly) {
            this.assembly = assembly;
            pluginType = assembly.GetType("NorthIslandChestPlugin.Plugin", true)!;
            plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
            // Construct only small managed state objects, not the plugin ctor.
            foreach (var name in new[] { "treasureMovementWait", "islandSwitchMovementWait", "treasurePlayerGuard", "treasureLoot", "treasureRecords", "config", "cofferOpeningCounter", "countedCofferTypes", "workflowProgressWatchdog" }) {
                var field = pluginType.GetField(name, Members)!;
                field.SetValue(plugin, Activator.CreateInstance(field.FieldType, true));
            }
            Set("activeProfile", assembly.GetType("NorthIslandChestPlugin.IslandProfile")!.GetProperty("North", Members)!.GetValue(null));
            Set("running", true); Set("initialScan", true); Set("silver", -1); Set("copper", -1);
            Set("freelancerJobName", "Phantom Freelancer");
            Set("treasureRecordPath", RecordPath);
            Set("currencyBuyer", RuntimeHelpers.GetUninitializedObject(assembly.GetType("NorthIslandChestPlugin.CurrencyBuyer", true)!));
            var playerType = Find("Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter");
            var player = Proxy(playerType, (m, a) => m.Name switch {
                "get_Position" => Position, "get_Name" => Text("K. T."), _ => Default(m.ReturnType)
            });
            Service("objects", (m, a) => {
                if (m.Name == "get_LocalPlayer") return player;
                if (m.Name == "GetEnumerator") {
                    var element = Find("Dalamud.Game.ClientState.Objects.Types.IGameObject");
                    var array = Array.CreateInstance(element, WorldObjects.Count);
                    for (int i = 0; i < WorldObjects.Count; i++) array.SetValue(WorldObjects[i], i);
                    return m.ReturnType.IsGenericType
                        ? typeof(IEnumerable<>).MakeGenericType(element).GetMethod("GetEnumerator")!.Invoke(array, null)
                        : array.GetEnumerator();
                }
                return Default(m.ReturnType);
            });
            Service("clientState", (m, a) => m.Name switch { "get_TerritoryType" => Territory, "get_IsLoggedIn" => LoggedIn, _ => Default(m.ReturnType) });
            Service("condition", (m, a) => m.Name switch {
                "get_Item" => Flags.Contains(Convert.ToInt32(a![0])),
                "Any" => a!.SelectMany(x => x is Array values ? values.Cast<object>() : new[] { x! }).Any(x => Flags.Contains(Convert.ToInt32(x))),
                _ => Default(m.ReturnType)
            });
            Service("gameGui", (m, a) => Default(m.ReturnType));
            Service("playerState", (m, a) => m.Name == "get_IsLoaded" ? true : Default(m.ReturnType));
            Service("framework", (m, a) => Default(m.ReturnType));
            Service("commands", (m, a) => { if (m.Name == "ProcessCommand") Commands.Add((string)a![0]!); return Default(m.ReturnType); });
            Service("log", (m, a) => {
                foreach (var value in a ?? Array.Empty<object?>()) {
                    if (value is string s) Logs.Add(s);
                    else if (value is Exception error) Logs.Add(error.ToString());
                }
                return Default(m.ReturnType);
            });
        }
        public IDictionary Loot => (IDictionary)Get("treasureLoot")!;
        public Action<bool> AddCoffer(ulong instanceId, uint baseId, bool eligible) {
            var nativeType = Find("FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure");
            var field = nativeType.GetField("Flags")!;
            int offset = field.GetCustomAttribute<System.Runtime.InteropServices.FieldOffsetAttribute>()!.Value;
            var address = System.Runtime.InteropServices.Marshal.AllocHGlobal(nativeType.StructLayoutAttribute!.Size);
            nativeCoffers.Add(address);
            long opened = Convert.ToInt64(Enum.Parse(field.FieldType, "Opened"));
            void SetOpened(bool value) {
                byte flag = (byte)(value ? opened : 0);
                System.Runtime.InteropServices.Marshal.WriteByte(address, offset, flag);
            }
            SetOpened(false);
            WorldObjects.Add(Proxy(Find("Dalamud.Game.ClientState.Objects.Types.IGameObject"), (m, a) => m.Name switch {
                "get_ObjectKind" => Enum.Parse(m.ReturnType, "Treasure"), "get_Address" => address,
                "get_BaseId" => baseId, "get_GameObjectId" => instanceId, _ => Default(m.ReturnType)
            }));
            ((IDictionary)Get("countedCofferTypes")!)[baseId] = eligible;
            return SetOpened;
        }
        public IList Records => (IList)Get("treasureRecords")!;
        public string Phase { get => Get("treasurePhase")!.ToString()!; set => Set("treasurePhase", Enum.Parse(pluginType.GetField("treasurePhase", Members)!.FieldType, value)); }
        public void Set(string name, object? value) => pluginType.GetField(name, Members)!.SetValue(plugin, value);
        public void SetEnum(string name, string value) => Set(name, Enum.Parse(pluginType.GetField(name, Members)!.FieldType, value));
        public object? Get(string name) => pluginType.GetField(name, Members)!.GetValue(plugin);
        public void SelectIsland(string name) {
            var profileType = assembly.GetType("NorthIslandChestPlugin.IslandProfile", true)!;
            var profile = profileType.GetProperty(name, Members)!.GetValue(null)!;
            Set("activeProfile", profile);
            Territory = (uint)profileType.GetProperty("TerritoryId")!.GetValue(profile)!;
            Position = (Vector3)profileType.GetProperty("CrystalMoveTarget")!.GetValue(profile)!;
        }
        public object? Call(string name, params object?[] args) => pluginType.GetMethod(name, Members)!.Invoke(plugin, args);
        public void Assert(bool condition, string message) {
            if (!condition) throw new Exception(message + " | " + string.Join(" | ", Logs.Where(s => s.Contains("Exception") || s.Contains("失败"))));
        }
        public void Chat(string text, int kind = 57) {
            var messageType = pluginType.GetMethod("OnChatMessage", Members)!.GetParameters()[0].ParameterType;
            var message = Proxy(messageType, (m, a) => m.Name switch {
                "get_Message" => Text(text), "get_LogKind" => Enum.ToObject(m.ReturnType, kind), _ => Default(m.ReturnType)
            });
            Call("OnChatMessage", message);
        }
        void Service(string name, Func<MethodInfo, object?[]?, object?> handler) => Set(name, Proxy(pluginType.GetField(name, Members)!.FieldType, handler));
        static object Proxy(Type type, Func<MethodInfo, object?[]?, object?> handler) {
            var proxy = DispatchProxy.Create(type, typeof(ServiceProxy));
            ((ServiceProxy)proxy).Handler = handler;
            return proxy;
        }
        Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null)!;
        object Text(string text) {
            var payload = Activator.CreateInstance(Find("Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload"), text)!;
            var list = Array.CreateInstance(Find("Dalamud.Game.Text.SeStringHandling.Payload"), 1);
            list.SetValue(payload, 0);
            return Activator.CreateInstance(Find("Dalamud.Game.Text.SeStringHandling.SeString"), new object[] { list })!;
        }
        static object? Default(Type type) => type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
        public void Cleanup() {
            foreach (var address in nativeCoffers) System.Runtime.InteropServices.Marshal.FreeHGlobal(address);
            var directory = Path.GetFullPath(Path.GetDirectoryName(RecordPath)!);
            if (!directory.StartsWith(TestRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup path escaped its workspace output directory");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

public class ServiceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
}
