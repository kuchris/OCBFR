using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    private sealed class TreasureHistoryWindow : PersistentWindow
    {
        private readonly Plugin plugin;
        private int filter;
        private int islandFilter;
        private int detailSort;
        private bool rareOnly;
        private bool showTotals = true;
        private string search = string.Empty;

        public TreasureHistoryWindow(Plugin plugin)
            : base(plugin, "寻宝战利品###OCNFarmerTreasureHistory", "TreasureHistory", new Vector2(900f, 640f), new Vector2(650f, 460f))
        {
            this.plugin = plugin;
            IsOpen = false;
        }

        public override void PreDraw()
        {
            base.PreDraw();
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.045f, 0.045f, 0.045f, 0.98f));
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.035f, 0.035f, 0.035f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.075f, 0.075f, 0.075f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.22f, 0.22f, 0.22f, 1f));
        }

        public override void PostDraw()
        {
            base.PostDraw();
            ImGui.PopStyleColor(4);
        }

        protected override void DrawContents()
        {
            using var theme = new MonochromeUiScope();
            ImGui.TextColored(in UiWhite, "OCBFR  /  " + UiText.Render("寻宝记录"));
            ImGui.TextDisabled(UiText.Render("浏览已完成的寻宝与获得物品"));
            ImGui.Spacing();
            DrawPeriodFilters();
            ImGui.Spacing();
            DrawSearchFilters();

            var records = TreasureHistoryView.Filter(plugin.treasureRecords, filter, islandFilter, DateTime.Now);
            var totals = TreasureHistoryView.Totals(records, search, rareOnly);
            var details = TreasureHistoryView.Details(records, search, rareOnly, detailSort == 0);
            ImGui.Spacing();
            DrawSummary(records);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button(UiText.Label("物品统计###OCBFRHistoryTotals"))) showTotals = true;
            ImGui.SameLine();
            if (ImGui.Button(UiText.Label("每次寻宝###OCBFRHistoryRuns"))) showTotals = false;
            ImGui.SameLine();
            ImGui.TextDisabled(UiText.Render(showTotals ? "物品获得统计" : "寻宝记录明细"));
            ImGui.Spacing();
            if (showTotals) DrawTotals(totals, records.Count);
            else DrawDetails(details);
        }

        private void DrawPeriodFilters()
        {
            string[] periods = { "全部", "今日", "本周", "本月" };
            for (int i = 0; i < periods.Length; i++)
            {
                if (i != 0) ImGui.SameLine();
                bool selected = filter == i;
                if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.30f, 0.30f, 0.30f, 1f));
                try
                {
                    if (ImGui.Button(UiText.Label(periods[i] + "###OCBFRHistoryPeriod" + i), new Vector2(72f, 0f) * ImGuiHelpers.GlobalScale)) filter = i;
                }
                finally { if (selected) ImGui.PopStyleColor(); }
            }
        }

        private void DrawSearchFilters()
        {
            ImGui.SetNextItemWidth(MathF.Max(150f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.42f));
            ImGui.InputTextWithHint("##OCBFRLootSearch", UiText.Render("搜索物品名称"), ref search, 128);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(135f * ImGuiHelpers.GlobalScale);
            string[] islands = { "全部副本", "北征之章", "南征之章" };
            if (ImGui.BeginCombo("##OCBFRHistoryIsland", UiText.Render(islands[islandFilter])))
            {
                try
                {
                    for (int i = 0; i < islands.Length; i++)
                        if (ImGui.Selectable(UiText.Label(islands[i]), islandFilter == i)) islandFilter = i;
                }
                finally { ImGui.EndCombo(); }
            }
            ImGui.SameLine();
            ImGui.Checkbox(UiText.Label("仅展示稀有物品##OCBFRHistoryRare"), ref rareOnly);
        }

        private static void DrawSummary(IReadOnlyList<TreasureRecord> records)
        {
            if (!ImGui.BeginTable("OCBFRHistorySummary", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings)) return;
            try
            {
                DrawMetric("完成寻宝", records.Count.ToString(), "Runs");
                DrawMetric("获得物品", records.Sum(r => (r.Loot ?? new()).Values.Sum(v => (long)v)).ToString("N0"), "Items");
                DrawMetric("有物品记录", records.Count(r => r.Loot?.Count > 0).ToString() + " / " + records.Count, "Captured");
            }
            finally { ImGui.EndTable(); }
        }

        private static void DrawMetric(string caption, string value, string id)
        {
            ImGui.TableNextColumn();
            ImGui.BeginChild("OCBFRHistoryMetric" + id, new Vector2(0f, 72f * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar);
            try { ImGui.TextDisabled(UiText.Render(caption)); ImGui.TextColored(in UiWhite, value); }
            finally { ImGui.EndChild(); }
        }

        private static void DrawTotals(IReadOnlyList<TreasureLootTotal> totals, int runCount)
        {
            if (totals.Count == 0)
            {
                DrawEmpty(runCount == 0 ? "尚未有已完成的寻宝记录" : "当前筛选范围内暂无战利品记录");
                return;
            }
            if (!ImGui.BeginTable("OCBFRHistoryTotalsTable", 2, HistoryTableFlags, new Vector2(0f, MathF.Max(60f, ImGui.GetContentRegionAvail().Y)))) return;
            try
            {
                ImGui.TableSetupColumn(UiText.Label("物品"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(UiText.Label("累计获得"), ImGuiTableColumnFlags.WidthFixed, 110f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();
                foreach (var item in totals)
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    ImGui.TextWrapped(FormatLootName(item.Name));
                    ImGui.TableNextColumn(); ImGui.Text(item.Count.ToString("N0"));
                }
            }
            finally { ImGui.EndTable(); }
        }

        private void DrawDetails(IReadOnlyList<TreasureRecord> records)
        {
            ImGui.SetNextItemWidth(135f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("##OCBFRHistorySort", UiText.Render(detailSort == 0 ? "最新在前" : "最早在前")))
            {
                try
                {
                    if (ImGui.Selectable(UiText.Label("最新在前"), detailSort == 0)) detailSort = 0;
                    if (ImGui.Selectable(UiText.Label("最早在前"), detailSort == 1)) detailSort = 1;
                }
                finally { ImGui.EndCombo(); }
            }
            if (records.Count == 0) { DrawEmpty("没有符合筛选条件的寻宝记录"); return; }
            if (!ImGui.BeginTable("OCBFRHistoryRunsTable", 3, HistoryTableFlags, new Vector2(0f, MathF.Max(60f, ImGui.GetContentRegionAvail().Y)))) return;
            try
            {
                ImGui.TableSetupColumn(UiText.Label("完成时间"), ImGuiTableColumnFlags.WidthFixed, 140f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn(UiText.Label("副本"), ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn(UiText.Label("战利品"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();
                foreach (var record in records)
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    ImGui.Text(record.CompletedAt.ToString("yyyy-MM-dd"));
                    ImGui.TextDisabled(record.CompletedAt.ToString("HH:mm"));
                    ImGui.TableNextColumn(); ImGui.Text(UiText.Render(record.Island == IslandTarget.SouthHorn ? "南征之章" : "北征之章"));
                    ImGui.TableNextColumn();
                    var loot = TreasureHistoryView.VisibleLoot(record, search, rareOnly);
                    if (loot.Count == 0) ImGui.TextDisabled(UiText.Render("未检测到获得物品消息"));
                    foreach (var item in loot) ImGui.TextWrapped(FormatLootName(item.Key) + "  ×" + item.Value.ToString("N0"));
                }
            }
            finally { ImGui.EndTable(); }
        }

        private const ImGuiTableFlags HistoryTableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.SizingStretchProp;

        private static void DrawEmpty(string message)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(UiText.Render(message));
            ImGui.TextWrapped(UiText.Render("记录会在寻宝流程完成后保存；未检测到物品的记录仍可在每次寻宝中查看。"));
        }
    }
}

internal sealed record TreasureLootTotal(string Name, long Count);

// Read-only projection. Existing loot keys and stored records are never rewritten.
internal static class TreasureHistoryView
{
    internal static List<TreasureRecord> Filter(IEnumerable<TreasureRecord> records, int period, int island, DateTime now)
    {
        var today = now.Date;
        var start = period switch { 1 => today, 2 => today.AddDays(-(int)today.DayOfWeek), 3 => new DateTime(today.Year, today.Month, 1), _ => DateTime.MinValue };
        return records.Where(r => r.CompletedAt >= start && (island == 0 || r.Island == (island == 1 ? IslandTarget.NorthHorn : IslandTarget.SouthHorn))).ToList();
    }

    internal static List<KeyValuePair<string, int>> VisibleLoot(TreasureRecord record, string search, bool rareOnly) => Plugin.OrderLoot(record.Loot ?? new())
        .Where(x => Matches(x.Key, search, rareOnly)).ToList();

    private static bool Matches(string name, string search, bool rareOnly) => (!rareOnly || Plugin.GetLootStarLevel(name) > 0)
        && (string.IsNullOrWhiteSpace(search) || name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static List<TreasureLootTotal> Totals(IEnumerable<TreasureRecord> records, string search, bool rareOnly) => records
        .SelectMany(r => r.Loot ?? new()).Where(x => Matches(x.Key, search, rareOnly)).GroupBy(x => x.Key, StringComparer.Ordinal)
        .Select(g => new TreasureLootTotal(g.Key, g.Sum(x => (long)x.Value)))
        .OrderByDescending(x => Plugin.GetLootStarLevel(x.Name)).ThenByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();

    internal static List<TreasureRecord> Details(IEnumerable<TreasureRecord> records, string search, bool rareOnly, bool newestFirst)
    {
        var filtered = records.Where(r => (!rareOnly && string.IsNullOrWhiteSpace(search)) || VisibleLoot(r, search, rareOnly).Count > 0);
        return (newestFirst ? filtered.OrderByDescending(r => r.CompletedAt) : filtered.OrderBy(r => r.CompletedAt)).ToList();
    }
}
