using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvSoft.Analyzers.TestUtil;

/// <summary>
/// Represents the result of running the generator on a compilation.
/// </summary>
public sealed record RunResultInfo(
    Compilation Compilation,
    ImmutableArray<GeneratedSourceResult> GeneratedSources,
    ImmutableArray<TrackedSourceInfo> TrackedSources,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<Diagnostic> GeneratorDiagnostics
);

[DebuggerDisplay("{HintName}, {Reason}")]
public sealed record TrackedSourceInfo(
    string HintName,
    string? SourceText,
    IncrementalStepRunReason? Reason);
