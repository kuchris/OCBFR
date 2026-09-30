using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NorthIslandChestPlugin;

// Client message parsing is independent of the UI language. Japanese templates
// were checked against local LogMessage rows 10958, 10965-10970, 9606, 657,
// 1053-1054 and 10872-10873. Chinese covers both scripts used by Global patches.
internal static class GameText
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex SilverBefore = new(@"(?<count>\d+)\s*(?:[个個]\s*)?(?:silver coffers?\b|(?:白[银銀]|[银銀])(?:色|的)?[宝寶]箱)", Options);
    private static readonly Regex BronzeBefore = new(@"(?<count>\d+)\s*(?:[个個]\s*)?(?:bronze coffers?\b|(?:青[铜銅]|[铜銅])(?:色|的)?[宝寶]箱)", Options);
    private static readonly Regex SilverAfter = new(@"(?:銀の宝箱|(?:白[银銀]|[银銀])(?:色|的)?[宝寶]箱)\s*(?<count>\d+)\s*[个個]", Options);
    private static readonly Regex BronzeAfter = new(@"(?:銅の宝箱|(?:青[铜銅]|[铜銅])(?:色|的)?[宝寶]箱)\s*(?<count>\d+)\s*[个個]", Options);

    internal static bool TryGetCofferCounts(string text, out int silver, out int bronze)
    {
        silver = bronze = 0;
        text = (text ?? string.Empty).Normalize(NormalizationForm.FormKC);
        if (text.Contains("There appear to be no treasure coffers", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("このエリアには、今は宝箱はなさそうだ", StringComparison.Ordinal) ||
            Regex.IsMatch(text, @"[当當]前(?:区域|區域).{0,12}(?:没有|沒有)[宝寶]箱", Options)) return true;
        Match s = SilverBefore.Match(text), b = BronzeBefore.Match(text);
        if (!s.Success) s = SilverAfter.Match(text);
        if (!b.Success) b = BronzeAfter.Match(text);
        return s.Success && b.Success &&
            int.TryParse(s.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out silver) &&
            int.TryParse(b.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out bronze);
    }

    internal static bool IsEntrySync(string text) =>
        text.Contains("item level has been synced", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("アイテムレベルシンク", StringComparison.Ordinal) ||
        Regex.IsMatch(text, @"品[级級]同步|物品等[级級]同步", Options);

    internal static bool IsJobChangeDenied(string text) =>
        (text.Contains("unable to change", StringComparison.OrdinalIgnoreCase) && text.Contains("phantom jobs", StringComparison.OrdinalIgnoreCase)) ||
        text.Contains("サポートジョブを変更できません", StringComparison.Ordinal) ||
        Regex.IsMatch(text, @"(?:无法|無法|不能).{0,16}(?:切[换換]|[变變]更).{0,8}(?:辅助|輔助|幻境|幻影)职业|(?:无法|無法|不能).{0,16}(?:切[换換]|[变變]更).{0,8}(?:輔助|幻境|幻影)職業", Options);

    internal static bool IsFreelancerChange(string text, string localName, string englishName)
    {
        if (string.IsNullOrWhiteSpace(localName)) return false;
        if (text.Contains(localName + " changes to " + englishName, StringComparison.Ordinal) ||
            text.StartsWith("You change to " + englishName, StringComparison.OrdinalIgnoreCase)) return true;
        if (!text.StartsWith(localName, StringComparison.Ordinal)) return false;
        return Regex.IsMatch(text.Substring(localName.Length), @"^はサポートジョブを\s*「サポートすっぴん」にチェンジした", Options) ||
            Regex.IsMatch(text.Substring(localName.Length), @"^.{0,4}(?:切[换換]|[变變]更).{0,8}(?:辅助|輔助|幻境|幻影)?自由人", Options);
    }

    internal static bool IsOwnOccultReturn(string text, string localName)
    {
        if (string.IsNullOrWhiteSpace(localName) || !text.StartsWith(localName, StringComparison.Ordinal)) return false;
        string action = text.Substring(localName.Length);
        return action.StartsWith(" uses Occult Return", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(action, @"^(?:[发發][动動]了[“「""'](?:[亚亞]返回)[”」""']|は[「“]デミデジョン[」”]を(?:実行|使用)した)", Options);
    }

    internal static bool TryParseLoot(string text, string localName, out string name, out int quantity, string bareItemName = null)
    {
        name = string.Empty;
        quantity = 1;
        text = Clean(text).TrimEnd('。', '！', '!', '.', ' ');
        string marker = null;
        foreach (string candidate in new[] { "获得了", "獲得了", "You obtain", "You receive" })
            if (text.Contains(candidate, StringComparison.OrdinalIgnoreCase)) { marker = candidate; break; }
        if (marker != null)
        {
            name = text.Substring(text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length).Trim();
            Match leading = Regex.Match(name, @"^(?<count>\d[\d,]*)\s*(?:枚|[个個]|件|[块塊]|[颗顆]|瓶|[张張]|本|[只隻]|[组組])?\s*(?<name>.+)$", Options);
            if (leading.Success)
            {
                if (!TryQuantity(leading.Groups["count"].Value, out quantity)) return false;
                name = leading.Groups["name"].Value;
            }
        }
        else
        {
            // Some loot rows contain just the linked item and ×quantity. Accept
            // those only when the caller verified a LootNotice ItemPayload name.
            if (!string.IsNullOrEmpty(bareItemName) && text.StartsWith(bareItemName, StringComparison.Ordinal))
            {
                string tail = text.Substring(bareItemName.Length).Trim();
                if (tail.Length == 0) { name = bareItemName; return true; }
                Match bare = Regex.Match(tail, @"^[×x]\s*(?<count>\d[\d,]*)$", Options);
                if (bare.Success && TryQuantity(bare.Groups["count"].Value, out quantity)) { name = bareItemName; return true; }
            }
            if (!string.IsNullOrWhiteSpace(localName) && text.StartsWith(localName + "は", StringComparison.Ordinal))
                text = text.Substring(localName.Length + 1).TrimStart('、', ' ');
            else if (Regex.IsMatch(text, @"^[A-Za-z .'-]+は", Options)) return false; // another player's loot
            Match japanese = Regex.Match(text, @"^(?<name>.+?)(?:(?:[×x]\s*|を\s*)(?<count>\d[\d,]*)(?:\([+]\d+%?\))?\s*(?:個|枚|本|匹|個分)?)?を?(?:手に入れた|入手した|入手しました|受け取りました)$", Options);
            if (!japanese.Success) return false;
            name = japanese.Groups["name"].Value;
            if (japanese.Groups["count"].Success && !TryQuantity(japanese.Groups["count"].Value, out quantity)) return false;
        }
        name = Clean(name).Trim('“', '”', '"', '\'', '「', '」', '『', '』');
        return name.Length > 0;
    }

    private static bool TryQuantity(string text, out int quantity) =>
        int.TryParse(text.Replace(",", string.Empty), NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0;

    private static string Clean(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text ?? string.Empty)
            if (char.GetUnicodeCategory(c) != UnicodeCategory.PrivateUse && !char.IsControl(c)) result.Append(c);
        return result.ToString().Trim();
    }
}
