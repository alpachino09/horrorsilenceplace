using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class ScopeChecks
{
    private static readonly string[] Codes = { "CS0136", "CS0128", "CS0100" };
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
        .Split(Path.PathSeparator).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();

    // Ask the real C# compiler to bind lexical declaration spaces. Missing Unity
    // references are deliberately NOT treated as a successful Unity compilation:
    // only these three independent name-conflict diagnostics are inspected here.
    public static Diagnostic[] Find(IEnumerable<SyntaxTree> trees)
    {
        var compilation = CSharpCompilation.Create("Silence.ScopeInspection", trees, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && Codes.Contains(d.Id)).ToArray();
    }

    public static int SelfTest()
    {
        var cases = new[] {
            ("reported CS0136 with unresolved Unity type", "using UnityEngine; class Example { void Build(Animator target) { foreach (string target in new[] { \"Crouch\" }) { } } }", "CS0136"),
            ("corrected crouch destination", "using UnityEngine; class Example { void Build(Animator target) { foreach (string destinationState in new[] { \"Crouch\" }) { } } }", ""),
            ("duplicate variable in one block", "class Example { void Test() { int value = 1; int value = 2; } }", "CS0128"),
            ("duplicate method parameter", "class Example { void Test(int value, int value) { } }", "CS0100"),
            ("field shadowing is allowed", "class Example { int value; void Test() { int value = 1; } }", ""),
            ("siblings may reuse a local", "class Example { void Test(bool flag) { if (flag) { int value = 1; } else { int value = 2; } } }", ""),
            ("local-function parameters may reuse outer names in C# 9", "class Example { void Test(int value) { void Inner(int value) { } } }", ""),
            ("later outer declaration also conflicts", "class Example { void Test(bool flag) { if (flag) { int value = 1; } int value = 2; } }", "CS0136")
        };
        int failed = 0;
        foreach (var (name, source, expected) in cases)
        {
            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9));
            var diagnostics = Find(new[] { tree });
            bool passed = expected.Length == 0 ? diagnostics.Length == 0 : diagnostics.Any(d => d.Id == expected);
            if (!passed) { Console.Error.WriteLine("Scope regression failed: " + name + " | " + string.Join("; ", diagnostics.Select(d => d.ToString()))); failed++; }
        }
        Console.WriteLine($"C# scope regressions: {cases.Length - failed} passed, {failed} failed. No Unity API compilation was performed.");
        return failed == 0 ? 0 : 1;
    }
}
