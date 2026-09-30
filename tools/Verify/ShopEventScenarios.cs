using System.Reflection;

internal static class ShopEventScenarios
{
    internal static int Run(Assembly assembly)
    {
        int failures = 0;
        void Check(string name, Func<bool> test)
        {
            try { if (!test()) throw new Exception("shop event check failed"); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.GetBaseException().Message); }
        }
        Check("plugin assembly has no OmenTools, GuerrillaNtp or TinyPinyin reference", () => !assembly.GetReferencedAssemblies().Any(r => new[] { "OmenTools", "GuerrillaNtp", "TinyPinyin" }.Contains(r.Name)));
        var bridge = assembly.GetType("NorthIslandChestPlugin.ShopEventBridge", true)!;
        byte[] Packet(string name, params object[] args) => (byte[])bridge.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        // Independent protocol fixtures: the old shop implementation used a
        // 32-byte envelope and 20-byte payload, with unused fields zeroed.
        Check("shop start packet preserves entity, event and zeroed protocol fields", () => {
            var packet = Packet("CreateStartPacket", 0x1234, 0xDEADBEEFu, 0x001B0600u);
            return packet.Length == 52 && BitConverter.ToUInt32(packet, 0) == 0x1234
                && BitConverter.ToUInt32(packet, 8) == 32 && BitConverter.ToUInt64(packet, 32) == 0xDEADBEEF
                && BitConverter.ToUInt32(packet, 40) == 0x001B0600 && packet[44..].All(b => b == 0)
                && packet[12..32].All(b => b == 0);
        });
        Check("shop completion packet preserves event and zeroed scene parameters", () => {
            var packet = Packet("CreateCompletePacket", 0x5678, 0x001B0600u);
            return packet.Length == 52 && BitConverter.ToUInt32(packet, 0) == 0x5678
                && BitConverter.ToUInt32(packet, 8) == 32 && BitConverter.ToUInt32(packet, 32) == 0x001B0600
                && packet[36..].All(b => b == 0);
        });
        Check("unavailable shop signatures disable purchases without dereferencing zero", () => {
            var ctor = bridge.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var parameters = ctor.GetParameters();
            object Service(Type type) => DispatchProxy.Create(type, typeof(EmptyService));
            var instance = ctor.Invoke(new[] { Service(parameters[0].ParameterType), Service(parameters[1].ParameterType) });
            return !(bool)bridge.GetProperty("IsReady", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
        });
        return failures;
    }

    public class EmptyService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.ReturnType == typeof(void) ? null
            : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
