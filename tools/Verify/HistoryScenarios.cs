using System.Collections;
using System.Reflection;

internal static class HistoryScenarios
{
    internal static int Run(Assembly assembly)
    {
        var recordType = assembly.GetType("NorthIslandChestPlugin.TreasureRecord", true)!;
        var view = assembly.GetType("NorthIslandChestPlugin.TreasureHistoryView", true)!;
        object? Call(string name, params object[] args) => view.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
        var records = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(recordType))!;
        var now = new DateTime(2026, 9, 30, 12, 0, 0);
        object Add(DateTime at, string island, Dictionary<string, int>? loot)
        {
            var r = Activator.CreateInstance(recordType)!;
            recordType.GetProperty("CompletedAt")!.SetValue(r, at);
            var p = recordType.GetProperty("Island")!;
            p.SetValue(r, Enum.Parse(p.PropertyType, island));
            recordType.GetProperty("Loot")!.SetValue(r, loot);
            records.Add(r); return r;
        }
        var first = Add(now, "NorthHorn", new() { ["Enlightenment gold obols"] = 16, ["好运胡萝卜"] = 1 });
        Add(now.AddDays(-2), "SouthHorn", new() { ["Enlightenment gold obols"] = 20 });
        var empty = Add(now.AddMinutes(-10), "NorthHorn", null);
        Add(now.AddMonths(-1), "NorthHorn", new() { ["Old item"] = 3 });
        int failures = 0;
        void Check(string name, Func<bool> test)
        {
            try { if (!test()) throw new Exception("incorrect history projection"); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.GetBaseException().Message); }
        }
        Check("history date and island filters preserve run boundaries", () => {
            var today = (IList)Call("Filter", records, 1, 1, now)!;
            return today.Count == 2 && ReferenceEquals(today[0], first) && ReferenceEquals(today[1], empty)
                && ((IList)Call("Filter", records, 3, 0, now)!).Count == 3
                && ((IList)Call("Filter", records, 0, 2, now)!).Count == 1;
        });
        Check("history item search aggregates quantities without mutating stored loot", () => {
            var totals = (IList)Call("Totals", records, "  GOLD OBOLS  ", false)!;
            var item = totals[0]!;
            var loot = (Dictionary<string, int>)recordType.GetProperty("Loot")!.GetValue(first)!;
            return totals.Count == 1 && (long)item.GetType().GetProperty("Count")!.GetValue(item)! == 36
                && loot["Enlightenment gold obols"] == 16 && recordType.GetProperty("Loot")!.GetValue(empty) == null;
        });
        Check("history empty runs, rare filter and time ordering remain available", () => {
            var all = (IList)Call("Details", records, "", false, true)!;
            var rare = (IList)Call("Details", records, "", true, false)!;
            return all.Count == 4 && ReferenceEquals(all[0], first) && all.Contains(empty)
                && rare.Count == 1 && ReferenceEquals(rare[0], first)
                && ((IList)Call("Details", records, "nonexistent", false, true)!).Count == 0;
        });
        return failures;
    }
}
