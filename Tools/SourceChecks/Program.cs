using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Any(path => !Directory.Exists(path)))
        {
            Console.Error.WriteLine("Usage: SourceChecks <source-directory> [additional-source-directory ...]");
            return 2;
        }
        string[] files = args.SelectMany(path => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)).Distinct().ToArray();
        if (files.Length == 0) return 2;
        int errors = 0;
        int guardErrors = 0;
        string[][] configurations =
        {
            Array.Empty<string>(),
            new[] { "UNITY_EDITOR", "ENABLE_INPUT_SYSTEM" },
            new[] { "UNITY_EDITOR", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
            new[] { "DEBUG", "UNITY_ENABLE_CHECKS", "UNITY_INCLUDE_INSTRUMENTATION", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
            new[] { "UNITY_ENABLE_CHECKS", "UNITY_INCLUDE_INSTRUMENTATION", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
            new[] { "UNITY_INCLUDE_INSTRUMENTATION", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
            new[] { "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" }
        };
        foreach (string file in files.OrderBy(p => p))
        {
            foreach (string[] symbols in configurations)
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                    new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: symbols), file);
                foreach (var diagnostic in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.Error.WriteLine(diagnostic);
                    errors++;
                }
                guardErrors += CheckDiagnosticGuards(Path.GetFileName(file), tree, symbols);
            }
        }
        Console.WriteLine($"C# 9 syntax: {files.Length} files, {configurations.Length} preprocessor configurations, {errors} errors. Unity API binding is NOT checked.");
        Console.WriteLine($"Diagnostic build guards: {configurations.Length} configurations, {guardErrors} contract errors. Editor/instrumented code is present; release automation is excluded.");
        return errors == 0 && guardErrors == 0 ? 0 : 1;
    }

    private static int CheckDiagnosticGuards(string file, SyntaxTree tree, string[] symbols)
    {
        if (!new[] { "InteractionDiagnostics.cs", "NetworkPlayer.cs", "NetworkSmokeProbe.cs", "GameplaySmokeProbe.cs", "NetworkSession.cs" }.Contains(file)) return 0;
        bool included = symbols.Contains("UNITY_EDITOR") || symbols.Contains("UNITY_INCLUDE_INSTRUMENTATION");
        int errors = 0;
        var root = tree.GetRoot(); var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        void Check(bool ok, string contract)
        {
            if (ok) return;
            Console.Error.WriteLine($"{file}: diagnostic guard contract failed ({string.Join(",", symbols)}): {contract}"); errors++;
        }
        void Method(string name) => Check(methods.Any(m => m.Identifier.ValueText == name) == included, name + " compilation matches instrumentation");
        switch (file)
        {
            case "NetworkPlayer.cs": Method("SetAutomationInput"); Method("WarpForSmoke"); break;
            case "NetworkSmokeProbe.cs": Method("Abort"); Method("Start"); break;
            case "GameplaySmokeProbe.cs": Method("Start"); break;
            case "NetworkSession.cs":
                bool abort = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(n => n.Expression is MemberBindingExpressionSyntax member && member.Name.Identifier.ValueText == "Abort");
                Check(abort == included, "Abort caller and smoke API have the same build guard"); break;
            case "InteractionDiagnostics.cs":
                Method("Reset");
                foreach (string name in new[] { "Trace", "ProbeKey" })
                {
                    var method = methods.Single(m => m.Identifier.ValueText == name);
                    Check((method.Body != null && method.Body.Statements.Count > 0) == included, name + " body has no release instrumentation");
                    var conditional = method.AttributeLists.SelectMany(a => a.Attributes).Where(a => a.Name.ToString() == "Conditional")
                        .SelectMany(a => a.ArgumentList.Arguments).OfType<AttributeArgumentSyntax>().Select(a => a.Expression).OfType<LiteralExpressionSyntax>().Select(a => a.Token.ValueText).ToArray();
                    Check(conditional.Contains("UNITY_EDITOR") && conditional.Contains("UNITY_INCLUDE_INSTRUMENTATION") && !conditional.Contains("DEVELOPMENT_BUILD"), name + " Conditional attributes use supported managed-variant symbols");
                }
                break;
        }
        return errors;
    }
}
