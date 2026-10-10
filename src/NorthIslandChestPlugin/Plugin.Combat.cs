using System;
using System.Text.RegularExpressions;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    private const string SelectRsrCommand = "/bocchi config set combat_autorotation RotationSolverReborn";
    private static readonly TimeSpan RsrSelectionTimeout = TimeSpan.FromSeconds(5);
    private bool awaitingRsrSelection;
    private bool rsrSelectionAcknowledged;
    private DateTime rsrSelectionDeadline;

    private bool HasModernBocchiCommand() => commands.Commands?.ContainsKey("/bocchi") == true;

    private string BocchiModeCommand(bool enabled) =>
        (commands.Commands?.ContainsKey("/bocchiillegal") == true || !HasModernBocchiCommand()
            ? "/bocchiillegal " : "/bocchi illegal ") + (enabled ? "on" : "off");

    private void SendBocchiEnabled(bool enabled)
    {
        if (!enabled) CancelBocchiCombatStart();
        Send(BocchiModeCommand(enabled));
    }

    private bool TrySendBocchiCommand(string command)
    {
        try
        {
            log.Debug("执行命令：" + command, Array.Empty<object>());
            return commands.ProcessCommand(command);
        }
        catch (Exception error)
        {
            log.Error(error, "执行命令失败：" + command, Array.Empty<object>());
            return false;
        }
    }

    private bool IsRsrAvailable()
    {
        try
        {
            return pluginInterface.GetIpcSubscriber<byte, object>("RotationSolverReborn.ChangeOperatingMode").HasAction;
        }
        catch (Exception error)
        {
            log.Warning(error, "无法检测 RSR 接口", Array.Empty<object>());
            return false;
        }
    }

    private void PrepareBocchiCombatStart()
    {
        if (!running || awaitingRsrSelection) return;
        if (!config.AutoSelectRsr)
        {
            EnableBocchiCombat();
            return;
        }
        if (!HasModernBocchiCommand() || !IsRsrAvailable())
        {
            Stop("自动选用 RSR 需要已加载 BOCCHI 和 RSR，已停止");
            return;
        }

        awaitingRsrSelection = true;
        rsrSelectionAcknowledged = false;
        rsrSelectionDeadline = DateTime.UtcNow + RsrSelectionTimeout;
        status = "等待 BOCCHI 选用 RSR...";
        if (!TrySendBocchiCommand(SelectRsrCommand))
            Stop("BOCCHI 的 RSR 设置指令失败，已停止");
    }

    private void AcceptBocchiRsrSelection(string text, string sender)
    {
        // BOCCHI prints after changing the in-memory setting. Dalamud's plugin chat
        // channel is configurable; plugin-generated messages have an empty sender.
        // Display paths can include a root prefix even when the command used a suffix key.
        if (!running || !awaitingRsrSelection || !string.IsNullOrEmpty(sender)) return;
        if (Regex.IsMatch(text.Trim(), @"(?:^|[\]\s])Set (?:\w+\.)*combat_autorotation = RotationSolverReborn$"))
            rsrSelectionAcknowledged = true;
    }

    private void AdvanceBocchiCombatStart()
    {
        if (!running || !awaitingRsrSelection) return;
        if (rsrSelectionAcknowledged)
        {
            CancelBocchiCombatStart();
            EnableBocchiCombat();
        }
        else if (DateTime.UtcNow >= rsrSelectionDeadline)
        {
            Stop("BOCCHI 未确认选用 RSR，已停止");
        }
    }

    private void EnableBocchiCombat()
    {
        if (!TrySendBocchiCommand(BocchiModeCommand(true)))
        {
            Stop("BOCCHI 启动指令失败，已停止");
            return;
        }
        if (!string.IsNullOrWhiteSpace(discardPreset))
            Send("/pdrdiscard " + discardPreset.Trim());
        bocchiEnabled = true;
        status = "自动战斗中";
    }

    private void CancelBocchiCombatStart()
    {
        awaitingRsrSelection = false;
        rsrSelectionAcknowledged = false;
        rsrSelectionDeadline = DateTime.MinValue;
    }
}
