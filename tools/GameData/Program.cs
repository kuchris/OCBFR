using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

using var data = new GameData(args[0]);
if (args.Contains("--messages")) {
    var english = data.Excel.GetSheet<LogMessage>(Language.English);
    var japanese = data.Excel.GetSheet<LogMessage>(Language.Japanese);
    foreach (uint id in new uint[] { 10957, 10958, 10959, 10960, 10961, 10962, 10963, 10964, 10965, 10966, 10967, 10968, 10969, 10970, 10971 })
        Console.WriteLine($"RAW {id}\tEN {english.GetRow(id).Text.ToMacroString()}\n\tJA {japanese.GetRow(id).Text.ToMacroString()}");
    foreach (var row in english) {
        string text = row.Text.ToMacroString();
        if (text.Contains("silver coffer", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("bronze coffer", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("no treasure coffer", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("item level has been synced", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("phantom", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("You obtain", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("You receive", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("changes to", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("uses ", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"{row.RowId}\tEN {row.Text.ToMacroString()}\n\tJA {japanese.GetRow(row.RowId).Text.ToMacroString()}");
    }
    foreach (var action in data.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Language.English))
        if (action.Name.ToString() == "Occult Return")
            Console.WriteLine($"ACTION {action.RowId}\tEN {action.Name}\tJA {data.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Language.Japanese).GetRow(action.RowId).Name}");
    Console.WriteLine($"JOB0 EN {data.Excel.GetSheet<MKDSupportJob>(Language.English).GetRow(0).Name}\tJA {data.Excel.GetSheet<MKDSupportJob>(Language.Japanese).GetRow(0).Name}");
    return;
}
foreach (var job in data.Excel.GetSheet<MKDSupportJob>(Language.English))
    Console.WriteLine($"{job.RowId}\t{job.NameEnglish}");
