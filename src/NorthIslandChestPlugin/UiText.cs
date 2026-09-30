using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NorthIslandChestPlugin;

public enum UiLanguage { TraditionalChinese, English }

// Presentation only: stored settings, commands, item keys and chat parsing never
// consume the translated result. Unknown text (including game item names) stays intact.
internal static class UiText
{
    private sealed class Entry
    {
        public string Source { get; set; }
        public string TraditionalChinese { get; set; }
        public string English { get; set; }
    }

    internal static UiLanguage Language { get; set; }
    private static readonly Entry[] Entries = Load();
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static UiLanguage cachedLanguage;

    private static Entry[] Load()
    {
        using Stream stream = typeof(UiText).Assembly.GetManifestResourceStream("OCBFR.UiTranslations.json");
        return JsonSerializer.Deserialize<Entry[]>(stream).OrderByDescending(e => e.Source.Length).ToArray();
    }

    internal static string Render(string source)
    {
        if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
        if (cachedLanguage != Language) { Cache.Clear(); cachedLanguage = Language; }
        if (Cache.TryGetValue(source, out string cached)) return cached;
        var result = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length;)
        {
            Entry entry = Entries.FirstOrDefault(e => source.AsSpan(i).StartsWith(e.Source, StringComparison.Ordinal));
            if (entry == null) { result.Append(source[i++]); continue; }
            result.Append(Language == UiLanguage.English ? entry.English : entry.TraditionalChinese);
            i += entry.Source.Length;
        }
        string text = result.ToString();
        // Bounded cache: values can include changing quantities and user text.
        if (Cache.Count >= 1024) Cache.Clear();
        Cache[source] = text;
        return text;
    }

    internal static string Label(string original)
    {
        if (string.IsNullOrEmpty(original) || original.StartsWith("##", StringComparison.Ordinal)) return original;
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original.Substring(0, separator);
        string translated = Render(visible);
        if (translated == visible) return original;
        // ImGui resets its hash at ###. Hashing the original full label preserves
        // the old ID, even for legacy labels using ##, when switching languages.
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return translated + "###" + (stable < 0 ? original : original.Substring(stable + 3));
    }
}
