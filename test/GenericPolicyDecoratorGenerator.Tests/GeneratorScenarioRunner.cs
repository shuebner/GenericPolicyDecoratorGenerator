using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SvSoft.Analyzers.TestUtil;

sealed class GeneratorScenarioRunner(IIncrementalGenerator generator)
{
    private GeneratorDriver _driver = CSharpGeneratorDriver.Create(
        [generator.AsSourceGenerator()],
        driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: true));

    /// <summary>
    /// Runs the generator on the given sources and returns the results.
    /// </summary>
    public RunResultInfo Run(params (string fileName, string source)[] sources)
    {
        // Create compilation
        var syntaxTrees = Array.ConvertAll(sources, s => CSharpSyntaxTree.ParseText(SourceText.From(s.source), path: s.fileName));
        Compilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

        // Run generator and produce updated compilation including generated sources
        _driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var generatorDiagnostics);

        var runResult = _driver.GetRunResult();

        // Collect compiler diagnostics from updated compilation (includes generated sources)
        var allDiagnostics = updatedCompilation.GetDiagnostics();
        var generatedSources = runResult.Results.SelectMany(r => r.GeneratedSources);
        var trackedOutputs = runResult.Results
            .SelectMany(r => r.TrackedSteps.GetValueOrDefault(WellKnownGeneratorOutputs.SourceOutput, []))
            .SelectMany(s => s.Outputs)
            .SelectMany(o => GetHintNames(o.Value).Select(hintName => (HintName: hintName, Reason: o.Reason)));

        return new RunResultInfo(
            Compilation: updatedCompilation,
            GeneratedSources: [.. generatedSources],
            TrackedSources: [.. trackedOutputs.Select(o => new TrackedSourceInfo(o.HintName, generatedSources.SingleOrDefault(s => s.HintName == o.HintName).SourceText?.ToString(), o.Reason))],
            Diagnostics: allDiagnostics,
            GeneratorDiagnostics: generatorDiagnostics
        );

        /// <summary>
        /// Workaround to get the HintName of the internal GeneratedSourceText type.
        /// </summary>
        /// <param name="generator"></param>
        static IEnumerable<string> GetHintNames(object value)
        {
            if (value is ITuple { Length: 2 } pair && pair[0] is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item.GetType().GetProperty("HintName") is PropertyInfo hintNameProperty)
                    {
                        if (hintNameProperty.GetValue(item) is string hintName)
                        {
                            yield return hintName;
                        }
                    }
                }
            }
        }
    }
}