using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        string[][] configurations =
        {
            Array.Empty<string>(),
            new[] { "UNITY_EDITOR", "ENABLE_INPUT_SYSTEM" },
            new[] { "UNITY_EDITOR", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
            new[] { "DEVELOPMENT_BUILD", "UNITY_6000_6_OR_NEWER", "ENABLE_INPUT_SYSTEM" },
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
            }
        }
        Console.WriteLine($"C# 9 syntax: {files.Length} files, {configurations.Length} preprocessor configurations, {errors} errors. Unity API binding is NOT checked.");
        return errors == 0 ? 0 : 1;
    }
}
