using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace NorthIslandChestPlugin;

// Only the two events used by the currency shop. No packet hooks, global service
// registry, time sync or transliteration library is needed. Signatures identify
// instructions in the installed Global client; opcodes are never hard-coded.
internal sealed unsafe class ShopEventBridge
{
    private delegate void* SendEventPacket(NetworkModuleProxy* network, byte* packet, uint flags, uint source);
    private readonly SendEventPacket send;
    private readonly int startOpcode;
    private readonly int completeOpcode;

    internal bool IsReady => send != null && startOpcode > 0 && completeOpcode > 0;

    internal ShopEventBridge(ISigScanner scanner, IPluginLog log)
    {
        try
        {
            var sender = scanner.ScanText("E8 ?? ?? ?? ?? 48 8B D6 48 8B CF E8 ?? ?? ?? ?? 48 8B 8C 24");
            var start = scanner.ScanText("C7 44 24 ?? ?? ?? ?? ?? 48 C7 44 24 ?? ?? ?? ?? ?? 89 5C 24 ?? 0F 85");
            var complete = scanner.ScanText("E8 ?? ?? ?? ?? EB 10 48 8B 0D ?? ?? ?? ??");
            // ISigScanner resolves an initial relative call to its target.
            if (sender == 0 || start == 0 || complete == 0) throw new InvalidOperationException("Currency shop event addresses are unavailable.");
            int startValue = Marshal.ReadInt32(start + 4);
            int completeValue = Marshal.ReadInt32(complete + 279);
            if (sender == 0 || start == 0 || complete == 0 || startValue <= 0 || startValue > ushort.MaxValue || completeValue <= 0 || completeValue > ushort.MaxValue)
                throw new InvalidOperationException("Currency shop event signatures are unavailable or invalid.");
            startOpcode = startValue;
            completeOpcode = completeValue;
            send = Marshal.GetDelegateForFunctionPointer<SendEventPacket>(sender);
            log.Information($"[OCBFR] Independent currency shop events ready (start={startOpcode}, complete={completeOpcode})");
        }
        catch (Exception error)
        {
            // Scans and treasure routes remain available if a future game update
            // changes these signatures. Purchases refuse to start in that case.
            log.Error(error, "[OCBFR] Currency shop events unavailable; automatic purchases disabled");
        }
    }

    internal bool Start(uint entityId, uint eventId) => Send(CreateStartPacket(startOpcode, entityId, eventId));
    internal bool Complete(uint eventId) => Send(CreateCompletePacket(completeOpcode, eventId));

    private bool Send(byte[] packet)
    {
        if (!IsReady) return false;
        var framework = Framework.Instance();
        if (framework == null || framework->NetworkModuleProxy == null) return false;
        fixed (byte* pointer = packet) send(framework->NetworkModuleProxy, pointer, 0, 159868227);
        return true;
    }

    // The game's event envelope reserves 32 bytes before its 20-byte payload.
    // Preserve the existing shop protocol, including the zero scene/parameters.
    internal static byte[] CreateStartPacket(int opcode, uint entityId, uint eventId)
    {
        var packet = CreateEnvelope(opcode);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(32), entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), eventId);
        return packet;
    }

    internal static byte[] CreateCompletePacket(int opcode, uint eventId)
    {
        var packet = CreateEnvelope(opcode);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), eventId);
        return packet;
    }

    private static byte[] CreateEnvelope(int opcode)
    {
        var packet = new byte[52];
        BinaryPrimitives.WriteInt32LittleEndian(packet, opcode);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 32);
        return packet;
    }
}
