using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length < 3)
    throw new ArgumentException("Usage: Verify <plugin.dll> <dependencies-directory> <Dalamud-directory>");
var paths = args.Select(Path.GetFullPath).ToArray();
if (args.Contains("--references")) {
    foreach (var file in new[] { paths[0] }.Concat(Directory.GetFiles(paths[1], "*.dll")).Distinct()) {
        using var stream = File.OpenRead(file);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        var names = metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name));
        Console.WriteLine(Path.GetFileName(file) + ": " + string.Join(", ", names));
    }
    return;
}
AssemblyLoadContext.Default.Resolving += (_, name) => {
    foreach (var directory in new[] { Path.GetDirectoryName(paths[0])!, paths[1], paths[2] }) {
        var candidate = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(candidate)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(paths[0]);
var types = assembly.GetTypes();
int prepared = 0, skipped = 0, failed = 0;
foreach (var type in types) {
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
        .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))) {
        if (method.ContainsGenericParameters || method.IsAbstract || method.GetMethodBody() == null) { skipped++; continue; }
        try { RuntimeHelpers.PrepareMethod(method.MethodHandle); prepared++; }
        catch (Exception error) {
            failed++;
            Console.WriteLine($"FAIL JIT {type.FullName}.{method.Name}: {error.GetType().Name}: {error.Message}");
        }
    }
}
Console.WriteLine($"Assembly: {assembly.GetName().Name} {assembly.GetName().Version}; types={types.Length}, JIT prepared={prepared}, skipped={skipped}, failed={failed}");
if (args.Contains("--regressions")) {
    failed += RegressionScenarios.Run(assembly);
    failed += HistoryScenarios.Run(assembly);
    failed += ShopEventScenarios.Run(assembly);
}
Environment.ExitCode = failed == 0 ? 0 : 1;
