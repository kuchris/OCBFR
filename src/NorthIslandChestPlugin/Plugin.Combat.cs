using System;

namespace NorthIslandChestPlugin;

public sealed partial class Plugin
{
    // RotationSolver.Basic.Data.StateCommandType: byte Off=0, Auto=1.
    // Dalamud converts byte arguments to the publisher's enum.
    private const byte RsrOffMode = 0;
    private const byte RsrAutoMode = 1;
    private static readonly TimeSpan RsrCheckInterval = TimeSpan.FromSeconds(1);
    private bool rsrControlOwned;
    private bool rsrStopRequested;
    private int rsrStopRetriesRemaining;
    private DateTime nextRsrCheckAt;
    private DateTime nextRsrStopRetryAt;
    private string rsrControlWarning = "";

    private bool HasBocchiRootCommand() => commands.Commands?.ContainsKey("/bocchi") == true;

    private string BocchiModeCommand(bool enabled) =>
        (commands.Commands?.ContainsKey("/bocchiillegal") == true || !HasBocchiRootCommand()
            ? "/bocchiillegal " : "/bocchi illegal ") + (enabled ? "on" : "off");

    private void SendBocchiEnabled(bool enabled)
    {
        Send(BocchiModeCommand(enabled));
        if (!enabled) StopDirectRsrControl();
    }

    private bool TrySendControlCommand(string command)
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
            if (pluginInterface.GetIpcSubscriber<byte, object>("RotationSolverReborn.ChangeOperatingMode").HasAction)
                return true;
        }
        catch (Exception error)
        {
            log.Warning(error, "无法检测 RSR 接口", Array.Empty<object>());
        }
        return commands.Commands?.ContainsKey("/rotation") == true;
    }

    private bool TryGetRsrActive(out bool active)
    {
        active = false;
        try
        {
            var gate = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
            if (!gate.HasFunction) return false;
            active = gate.InvokeFunc();
            return true;
        }
        catch (Exception error)
        {
            log.Warning(error, "无法读取 RSR 状态", Array.Empty<object>());
            return false;
        }
    }

    private bool TrySetRsrMode(byte mode)
    {
        bool dispatched = false;
        string name = mode == RsrAutoMode ? "Auto" : "Off";
        try
        {
            var gate = pluginInterface.GetIpcSubscriber<byte, object>("RotationSolverReborn.ChangeOperatingMode");
            if (gate.HasAction)
            {
                gate.InvokeAction(mode);
                dispatched = true;
            }
        }
        catch (Exception error)
        {
            log.Warning(error, "RSR 模式 IPC 调用失败：" + name, Array.Empty<object>());
        }
        if (dispatched && (!TryGetRsrActive(out bool active) || active == (mode == RsrAutoMode)))
        {
            log.Information("OCBFR 直接控制 RSR：" + name, Array.Empty<object>());
            return true;
        }
        if (commands.Commands?.ContainsKey("/rotation") == true &&
            TrySendControlCommand(mode == RsrAutoMode ? "/rotation auto" : "/rotation off") &&
            (!TryGetRsrActive(out bool commandActive) || commandActive == (mode == RsrAutoMode)))
        {
            log.Information("OCBFR 通过 RSR 指令控制模式：" + name, Array.Empty<object>());
            return true;
        }
        return false;
    }

    private void PrepareBocchiCombatStart()
    {
        if (!running) return;
        if (config.AutoSelectRsr && !IsRsrAvailable())
        {
            Stop("直接控制 RSR 需要已加载 RSR，已停止");
            return;
        }
        if (!TrySendControlCommand(BocchiModeCommand(true)))
        {
            Stop("BOCCHI 启动指令失败，已停止");
            return;
        }
        bocchiEnabled = true;
        if (config.AutoSelectRsr)
        {
            rsrControlOwned = true;
            rsrStopRequested = false;
            rsrStopRetriesRemaining = 0;
            if (!TrySetRsrMode(RsrAutoMode))
            {
                Stop("RSR 未确认启用，已停止");
                return;
            }
            rsrControlWarning = "";
            nextRsrCheckAt = DateTime.UtcNow + RsrCheckInterval;
        }
        if (!string.IsNullOrWhiteSpace(discardPreset))
            Send("/pdrdiscard " + discardPreset.Trim());
        status = "自动战斗中";
    }

    private void RefreshDirectRsrControl()
    {
        if (!running || !bocchiEnabled || !rsrControlOwned || rsrStopRequested ||
            !config.AutoSelectRsr || DateTime.UtcNow < nextRsrCheckAt) return;
        nextRsrCheckAt = DateTime.UtcNow + RsrCheckInterval;
        if (!IsRsrAvailable() || (TryGetRsrActive(out bool active) && !active && !TrySetRsrMode(RsrAutoMode)))
            Stop("RSR 无法继续自动战斗，已停止");
    }

    private void ClearDirectRsrControl()
    {
        rsrControlOwned = rsrStopRequested = false;
        rsrStopRetriesRemaining = 0;
        nextRsrCheckAt = nextRsrStopRetryAt = DateTime.MinValue;
        rsrControlWarning = "";
    }

    private void StopDirectRsrControl()
    {
        if (!rsrControlOwned || rsrStopRequested) return;
        rsrStopRequested = true;
        if (TrySetRsrMode(RsrOffMode))
        {
            ClearDirectRsrControl();
            return;
        }
        rsrStopRetriesRemaining = 2;
        nextRsrStopRetryAt = DateTime.UtcNow + RsrCheckInterval;
        rsrControlWarning = "RSR 关闭未确认，正在重试；如仍在战斗，请手动输入 /rotation off。";
        log.Error(rsrControlWarning, Array.Empty<object>());
    }

    private void RetryDirectRsrStop()
    {
        if (!rsrStopRequested || DateTime.UtcNow < nextRsrStopRetryAt) return;
        nextRsrStopRetryAt = DateTime.UtcNow + RsrCheckInterval;
        if (rsrStopRetriesRemaining <= 0)
        {
            // RSR reports inactive while its player is unavailable, even if its mode
            // is still enabled. Only accept manual-off confirmation once ready again.
            if (clientState.IsLoggedIn && playerState.IsLoaded && objects.LocalPlayer != null &&
                !condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] &&
                !condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51] &&
                TryGetRsrActive(out bool active) && !active) ClearDirectRsrControl();
            return;
        }
        rsrStopRetriesRemaining--;
        if (TrySetRsrMode(RsrOffMode)) ClearDirectRsrControl();
        else if (rsrStopRetriesRemaining == 0)
        {
            rsrControlWarning = "RSR 关闭失败，请手动输入 /rotation off。";
            log.Error(rsrControlWarning, Array.Empty<object>());
        }
    }
}
