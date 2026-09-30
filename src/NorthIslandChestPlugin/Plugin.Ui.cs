using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    private ISharedImmediateTexture brandIcon;
    private int selectedUiPage;
    private static readonly Vector4 UiWhite = new(0.94f, 0.94f, 0.94f, 1f);
    private static readonly Vector4 UiMuted = new(0.57f, 0.57f, 0.57f, 1f);

    internal static ProcessStartInfo CreateSponsorStartInfo() => new(SponsorUrl) { UseShellExecute = true };

    private void TryLoadBrandIcon(ITextureProvider textures)
    {
        try { brandIcon = textures.GetFromManifestResource(typeof(Plugin).Assembly, "OCBFR.icon.png"); }
        catch (Exception error) { log.Warning(error, "OCBFR icon unavailable; using text header", Array.Empty<object>()); }
    }

    public void DrawStatus()
    {
        using var theme = new MonochromeUiScope();
        DrawDashboardHeader();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawDashboardCards();
        ImGui.Spacing();
        if (!config.SimplifiedUi)
        {
            float reserved = ImGui.GetFrameHeightWithSpacing() + 30f * ImGuiHelpers.GlobalScale;
            if (!string.IsNullOrEmpty(treasureError)) reserved += 42f * ImGuiHelpers.GlobalScale;
            ImGui.BeginChild("BWorkspace", new Vector2(0f, -reserved), false, ImGuiWindowFlags.None);
            try { DrawDashboardWorkspace(); }
            finally { ImGui.EndChild(); }
        }
        else
        {
            ImGui.TextDisabled(UiText.Render(activeProfile.ChapterName));
            ImGui.TextDisabled(UiText.Render(combatJob));
            ImGui.Spacing();
        }
        if (!string.IsNullOrEmpty(treasureError))
            ImGui.TextWrapped(UiText.Render("错误：") + UiText.Render(treasureError));
        DrawDashboardActions();
    }

    private void DrawDashboardHeader()
    {
        if (!ImGui.BeginTable("OCBFRBrandHeader", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings)) return;
        try
        {
            ImGui.TableSetupColumn("Brand", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableNextColumn();
            var texture = brandIcon?.GetWrapOrDefault();
            if (texture != null)
            {
                ImGui.Image(texture.Handle, new Vector2(42f, 42f) * ImGuiHelpers.GlobalScale);
                ImGui.SameLine();
            }
            ImGui.BeginGroup();
            ImGui.TextColored(in UiWhite, "OCBFR");
            ImGui.TextDisabled(config.SimplifiedUi ? "v" + PluginVersion + "  /  kuchris" : "OCCULT CRESCENT  /  v" + PluginVersion);
            ImGui.EndGroup();
            ImGui.TableNextColumn();
            DrawLanguageSelector();
            ImGui.SameLine();
            string view = config.SimplifiedUi ? "完整界面" : "简化界面";
            if (ImGui.Button(UiText.Label(view + "###OCBFRViewToggle"))) ToggleMainUi();
        }
        finally { ImGui.EndTable(); }
    }

    private void DrawDashboardCards()
    {
        if (!ImGui.BeginTable("OCBFRDashboardCards", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings)) return;
        try
        {
            ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthStretch, config.SimplifiedUi ? 1.4f : 2.2f);
            ImGui.TableSetupColumn("Silver", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Bronze", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableNextColumn();
            ImGui.BeginChild("OCBFRCurrentCard", new Vector2(0f, 90f * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar);
            try
            {
                ImGui.TextDisabled(UiText.Render("当前任务"));
                ImGui.TextWrapped(UiText.Render(status));
                if (!config.SimplifiedUi) ImGui.TextDisabled(UiText.Render(running ? "运行中" : "已停止"));
            }
            finally { ImGui.EndChild(); }
            ImGui.TableNextColumn();
            DrawCofferCard("OCBFRSilverCard", "银箱", silver, MaxSilver);
            ImGui.TableNextColumn();
            DrawCofferCard("OCBFRBronzeCard", "铜箱", copper, MaxCopper);
        }
        finally { ImGui.EndTable(); }
    }

    private static void DrawCofferCard(string id, string caption, int count, int capacity)
    {
        ImGui.BeginChild(id, new Vector2(0f, 90f * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar);
        try
        {
            ImGui.TextDisabled(UiText.Render(caption));
            ImGui.TextColored(in UiWhite, count < 0 ? UiText.Render("未检测") : $"{count:00} / {capacity:00}");
            ImGui.Spacing();
            ImGui.ProgressBar(count < 0 ? 0f : Math.Clamp((float)count / capacity, 0f, 1f), new Vector2(-1f, 3f * ImGuiHelpers.GlobalScale), "");
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawDashboardWorkspace()
    {
        float navigationWidth = 142f * ImGuiHelpers.GlobalScale;
        ImGui.BeginChild("OCBFRNavigation", new Vector2(navigationWidth, 0f), false, ImGuiWindowFlags.None);
        try
        {
            ImGui.Spacing();
            ImGui.TextDisabled(UiText.Render("控制台"));
            ImGui.Spacing();
            string[] pages = { "总览", "自动购买", "魔之塔", "测试工具" };
            for (int i = 0; i < pages.Length; i++)
                if (ImGui.Selectable(UiText.Label(pages[i] + "###OCBFRPage" + i), selectedUiPage == i, ImGuiSelectableFlags.None, new Vector2(0f, 28f * ImGuiHelpers.GlobalScale))) selectedUiPage = i;
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextDisabled(UiText.Render("自动流程"));
            string[] steps = { "进入", "检测", "战斗", "寻宝", "重进" };
            int step = ResolveBProgressStep();
            if (running && (treasurePhase == TreasurePhase.LeaveDuty || treasurePhase == TreasurePhase.Reentry)) step = 5;
            for (int i = 0; i < steps.Length; i++)
            {
                Vector4 color = step == i + 1 ? UiWhite : UiMuted;
                ImGui.TextColored(in color, $"{i + 1:00}  " + UiText.Render(steps[i]));
            }
            ImGui.Spacing();
            ImGui.TextDisabled("kuchris");
        }
        finally { ImGui.EndChild(); }
        ImGui.SameLine();
        ImGui.BeginChild("BSettings", new Vector2(0f, 0f), true, ImGuiWindowFlags.None);
        try
        {
            switch (selectedUiPage)
            {
                case 0:
                    DrawBSectionTitle("副本与战斗配置", "副本设置");
                    DrawIslandTargetConfig();
                    ImGui.Spacing();
                    ImGui.TextDisabled(UiText.Render(activeProfile.ChapterName));
                    ImGui.Separator();
                    ImGui.Spacing();
                    DrawBProfileConfig();
                    ImGui.Spacing();
                    DrawRequiredModulesButton();
                    break;
                case 1: DrawAutomaticPurchaseConfig(); break;
                case 2:
                    if (activeProfile.SupportsTower) DrawBTowerConfig();
                    else ImGui.TextWrapped(UiText.Render("南岛不支持魔之塔功能"));
                    break;
                case 3:
                    ImGui.TextWrapped(UiText.Render("测试完成后请紧急停止，恢复正常运行前关闭模拟选项。"));
                    DrawBDebug();
                    break;
            }
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawDashboardActions()
    {
        ImGui.Separator();
        ImGui.Spacing();
        string primary = running ? "停止运行" : "开始运行";
        ImGui.BeginDisabled(!running && currencyBuyer.IsBusy);
        ImGui.PushStyleColor(ImGuiCol.Button, UiWhite);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.80f, 0.80f, 0.80f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.68f, 0.68f, 0.68f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.06f, 0.06f, 0.06f, 1f));
        try
        {
            if (ImGui.Button(UiText.Label(primary), new Vector2(90f, 0f) * ImGuiHelpers.GlobalScale))
            {
                if (running) StopFromUser(); else Start();
            }
        }
        finally { ImGui.PopStyleColor(4); ImGui.EndDisabled(); }
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label("紧急停止"), new Vector2(135f, 0f) * ImGuiHelpers.GlobalScale)) StopFromUser(emergency: true);
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label("寻宝记录###OCBFRHistoryFooter"), new Vector2(125f, 0f) * ImGuiHelpers.GlobalScale)) treasureHistoryWindow.IsOpen = true;
        ImGui.SameLine();
        if (ImGui.Button("Ko-fi###OCBFRSupport", new Vector2(65f, 0f) * ImGuiHelpers.GlobalScale)) OpenSponsorPage();
    }
}

internal readonly struct MonochromeUiScope : IDisposable
{
    private static readonly (ImGuiCol Id, Vector4 Color)[] Colors = {
        (ImGuiCol.Text, new(0.94f,0.94f,0.94f,1f)), (ImGuiCol.TextDisabled, new(0.57f,0.57f,0.57f,1f)),
        (ImGuiCol.WindowBg, new(0.045f,0.045f,0.045f,0.98f)), (ImGuiCol.ChildBg, new(0.075f,0.075f,0.075f,1f)),
        (ImGuiCol.PopupBg, new(0.07f,0.07f,0.07f,1f)), (ImGuiCol.Border, new(0.22f,0.22f,0.22f,1f)),
        (ImGuiCol.FrameBg, new(0.13f,0.13f,0.13f,1f)), (ImGuiCol.FrameBgHovered, new(0.20f,0.20f,0.20f,1f)),
        (ImGuiCol.FrameBgActive, new(0.27f,0.27f,0.27f,1f)), (ImGuiCol.CheckMark, new(0.92f,0.92f,0.92f,1f)),
        (ImGuiCol.Button, new(0.14f,0.14f,0.14f,1f)), (ImGuiCol.ButtonHovered, new(0.24f,0.24f,0.24f,1f)),
        (ImGuiCol.ButtonActive, new(0.31f,0.31f,0.31f,1f)), (ImGuiCol.Header, new(0.18f,0.18f,0.18f,1f)),
        (ImGuiCol.HeaderHovered, new(0.24f,0.24f,0.24f,1f)), (ImGuiCol.HeaderActive, new(0.30f,0.30f,0.30f,1f)),
        (ImGuiCol.Separator, new(0.23f,0.23f,0.23f,1f)), (ImGuiCol.PlotHistogram, new(0.84f,0.84f,0.84f,1f)),
        (ImGuiCol.Tab, new(0.12f,0.12f,0.12f,1f)), (ImGuiCol.TabHovered, new(0.26f,0.26f,0.26f,1f)),
        (ImGuiCol.TabActive, new(0.23f,0.23f,0.23f,1f))
    };
    public MonochromeUiScope()
    {
        foreach (var (id, color) in Colors) ImGui.PushStyleColor(id, color);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(9f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
    }
    public void Dispose() { ImGui.PopStyleVar(6); ImGui.PopStyleColor(Colors.Length); }
}
