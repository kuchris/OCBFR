using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

var path = args[0];
var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
if (args.Contains("--compare-flows")) {
    var other = CSharpSyntaxTree.ParseText(File.ReadAllText(args[1])).GetRoot();
    var names = new HashSet<string> { "Start", "Stop", "EmergencyStop", "ChangeToCombatJob", "RequestFreelancerScan", "AdvanceFreelancerScan", "CompleteTreasureScan", "BeginTreasureProcedure", "UpdateTreasureProcedure", "TryCompleteTreasureReturn", "TryCompleteEntryHandshake", "ResetForIslandEntry", "OnTerritoryChanged", "TryDismount", "InvokeTreasureScan", "SummonRandomMount", "UpdateTowerProcedure", "UpdateCurrencyPurchaseMove", "ApplySelectedProfile", "Send", "ResetIslandEntryState" };
    int compared = 0;
    foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.Text))) {
        var candidate = other.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == method.Identifier.Text && m.ParameterList.Parameters.Count == method.ParameterList.Parameters.Count);
        string Tokens(SyntaxNode node) => string.Join("\n", node.DescendantTokens().Select(t => t.Text));
        if (Tokens(method) != Tokens(candidate)) throw new Exception("Flow source changed: " + method.Identifier.Text);
        Console.WriteLine("UNCHANGED " + method.Identifier.Text); compared++;
    }
    Console.WriteLine("Unchanged gameplay methods: " + compared);
} else if (args.Contains("--inventory")) {
    var strings = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
        .Where(n => n.IsKind(SyntaxKind.StringLiteralExpression) && n.Token.ValueText.Any(c => c >= '\u4e00' && c <= '\u9fff'))
        .Select(n => new { Line = n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, Text = n.Token.ValueText, Context = n.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "field", Calls = n.Ancestors().OfType<InvocationExpressionSyntax>().Select(c => c.Expression.ToString()).ToArray() });
    Console.WriteLine(JsonSerializer.Serialize(strings, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true }));
} else if (args.Contains("--fragments")) {
    Console.WriteLine(JsonSerializer.Serialize(root.DescendantNodes().OfType<InterpolatedStringTextSyntax>().Select(n => new { Line = n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, Text = n.TextToken.ValueText }), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true }));
} else {
    File.WriteAllText(path, new RenderRewriter().Visit(root)!.ToFullString(), new System.Text.UTF8Encoding(false));
}

sealed class RenderRewriter : CSharpSyntaxRewriter {
    static readonly HashSet<string> Labels = new() { "Button", "SmallButton", "Checkbox", "RadioButton", "Selectable", "CollapsingHeader", "BeginCombo", "BeginTabItem", "InputText", "InputTextWithHint", "InputInt", "TableSetupColumn" };
    static readonly HashSet<string> Text = new() { "Text", "TextWrapped", "TextDisabled", "TextUnformatted", "SetTooltip", "CalcTextSize" };
    static ExpressionSyntax Wrap(ExpressionSyntax expr, bool label = false) => SyntaxFactory.ParseExpression($"UiText.{(label ? "Label" : "Render")}({expr.WithoutTrivia()})").WithTriviaFrom(expr);
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        // Never changes commands, chat parsing, configuration values, or state strings.
        if (node.Expression is MemberAccessExpressionSyntax member) {
            string method = member.Name.Identifier.Text;
            if (member.Expression.ToString() == "ImGui") {
                var indices = new List<(int Index, bool Label)>();
                if (Labels.Contains(method)) indices.Add((0, true));
                if (Text.Contains(method)) indices.Add((0, false));
                if (method == "TextColored") indices.Add((1, false));
                if (method == "BeginCombo" || method == "InputTextWithHint") indices.Add((1, false));
                var arguments = node.ArgumentList.Arguments;
                foreach (var (index, label) in indices) {
                    if (index >= arguments.Count) continue;
                    var expr = arguments[index].Expression;
                    // ImU8String is a ref struct. Translate its fragments where built.
                    var scope = node.Ancestors().FirstOrDefault(n => n is MethodDeclarationSyntax || n is ConstructorDeclarationSyntax);
                    if (expr is IdentifierNameSyntax id && scope?.DescendantNodes().OfType<VariableDeclarationSyntax>()
                        .Any(v => v.Type.ToString() == "ImU8String" && v.Variables.Any(x => x.Identifier.Text == id.Identifier.Text)) == true) continue;
                    if (expr.ToString().StartsWith("UiText.")) continue;
                    arguments = arguments.Replace(arguments[index], arguments[index].WithExpression(Wrap(expr, label)));
                }
                return node.WithArgumentList(node.ArgumentList.WithArguments(arguments));
            }
            if (method == "AppendLiteral" || member.Name.ToString() == "AppendFormatted<string>") {
                var scope = node.Ancestors().FirstOrDefault(n => n is MethodDeclarationSyntax);
                if (scope?.DescendantNodes().OfType<VariableDeclarationSyntax>().Any(v => v.Type.ToString() == "ImU8String" && v.Variables.Any(x => x.Identifier.Text == member.Expression.ToString())) == true) {
                    var argument = node.ArgumentList.Arguments[0];
                    if (!argument.Expression.ToString().StartsWith("UiText."))
                        return node.WithArgumentList(node.ArgumentList.WithArguments(node.ArgumentList.Arguments.Replace(argument, argument.WithExpression(Wrap(argument.Expression)))));
                }
            }
        }
        return base.VisitInvocationExpression(node);
    }
}
